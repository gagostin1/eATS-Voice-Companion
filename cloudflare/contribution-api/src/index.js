const MAX_BODY_BYTES = 32 * 1024;
const MAX_ADMIN_PAGE_SIZE = 100;
const DEFAULT_ADMIN_PAGE_SIZE = 50;

const REQUIRED_FIELDS = [
  "SchemaVersion",
  "AppVersion",
  "RecordedAtUtc",
  "Callsign",
  "AirlineAliases",
  "OriginalTranscript",
  "WasBestEffort",
  "CorrectedTranscript",
  "ExpectedCommand",
  "ControllerPosition",
  "RouteFixes"
];

const OPTIONAL_FIELDS = ["GeneratedCommand", "ActiveStar"];
const ALLOWED_FIELDS = new Set([...REQUIRED_FIELDS, ...OPTIONAL_FIELDS]);
const TOKEN_PATTERN = /^[A-Z0-9]+$/;
const INSTALLATION_ID_PATTERN = /^[a-f0-9]{8}-[a-f0-9]{4}-[1-8][a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/i;
const REVIEW_STATUSES = new Set(["pending", "accepted", "rejected"]);

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);

      if (request.method === "GET" && url.pathname === "/health") {
        return json({ status: "ok", schemaVersion: 2 });
      }

      if (request.method === "POST" && url.pathname === "/v1/contributions") {
        return await submitContribution(request, env);
      }

      if (request.method === "GET" && url.pathname === "/v1/admin/contributions") {
        return await listContributions(request, env, url);
      }

      const reviewMatch = url.pathname.match(
        /^\/v1\/admin\/contributions\/([a-f0-9]{64})$/
      );
      if (request.method === "PATCH" && reviewMatch) {
        return await reviewContribution(request, env, reviewMatch[1]);
      }

      return error(404, "not_found", "The requested endpoint does not exist.");
    } catch (exception) {
      console.error("Unhandled contribution API error", exception);
      return error(500, "server_error", "The contribution could not be processed.");
    }
  }
};

async function submitContribution(request, env) {
  const contentType = request.headers.get("content-type")?.toLowerCase() ?? "";
  if (!contentType.startsWith("application/json")) {
    return error(415, "unsupported_media_type", "Content-Type must be application/json.");
  }

  const declaredLength = Number(request.headers.get("content-length") ?? 0);
  if (Number.isFinite(declaredLength) && declaredLength > MAX_BODY_BYTES) {
    return error(413, "payload_too_large", "The contribution exceeds 32 KiB.");
  }

  const rateLimitKey = getRateLimitKey(request);
  const rateLimitResult = await env.SUBMISSION_RATE_LIMITER.limit({ key: rateLimitKey });
  if (!rateLimitResult.success) {
    return error(429, "rate_limited", "Too many contributions were submitted. Try again shortly.", {
      "Retry-After": "60"
    });
  }

  const bodyText = await request.text();
  if (new TextEncoder().encode(bodyText).byteLength > MAX_BODY_BYTES) {
    return error(413, "payload_too_large", "The contribution exceeds 32 KiB.");
  }

  let input;
  try {
    input = JSON.parse(bodyText);
  } catch {
    return error(400, "invalid_json", "The request body is not valid JSON.");
  }

  const validation = validateContribution(input);
  if (!validation.ok) {
    return error(422, "invalid_contribution", validation.message);
  }

  const normalized = normalizeContribution(input);
  const payloadJson = JSON.stringify(normalized);
  const id = await sha256(payloadJson);
  const receivedAt = new Date().toISOString();

  const result = await env.DB.prepare(
    `INSERT OR IGNORE INTO contributions
      (id, received_at, schema_version, app_version, callsign,
       expected_command, payload_json, review_status)
     VALUES (?, ?, ?, ?, ?, ?, ?, 'pending')`
  )
    .bind(
      id,
      receivedAt,
      normalized.SchemaVersion,
      normalized.AppVersion,
      normalized.Callsign,
      normalized.ExpectedCommand,
      payloadJson
    )
    .run();

  const created = Number(result.meta?.changes ?? 0) > 0;
  return json(
    {
      receiptId: id,
      status: created ? "accepted" : "duplicate"
    },
    created ? 202 : 200
  );
}

async function listContributions(request, env, url) {
  const unauthorized = authorizeAdmin(request, env);
  if (unauthorized) {
    return unauthorized;
  }

  const requestedLimit = Number.parseInt(url.searchParams.get("limit") ?? "", 10);
  const limit = Number.isFinite(requestedLimit)
    ? Math.min(Math.max(requestedLimit, 1), MAX_ADMIN_PAGE_SIZE)
    : DEFAULT_ADMIN_PAGE_SIZE;
  const status = url.searchParams.get("status") ?? "pending";

  if (!REVIEW_STATUSES.has(status)) {
    return error(400, "invalid_status", "Status must be pending, accepted, or rejected.");
  }

  const result = await env.DB.prepare(
    `SELECT id, received_at, payload_json, review_status, reviewed_at
       FROM contributions
      WHERE review_status = ?
      ORDER BY received_at ASC
      LIMIT ?`
  )
    .bind(status, limit)
    .all();

  const contributions = (result.results ?? []).map((row) => ({
    receiptId: row.id,
    receivedAtUtc: row.received_at,
    reviewStatus: row.review_status,
    reviewedAtUtc: row.reviewed_at,
    contribution: JSON.parse(row.payload_json)
  }));

  return json({ contributions, count: contributions.length });
}

async function reviewContribution(request, env, id) {
  const unauthorized = authorizeAdmin(request, env);
  if (unauthorized) {
    return unauthorized;
  }

  let input;
  try {
    input = await request.json();
  } catch {
    return error(400, "invalid_json", "The request body is not valid JSON.");
  }

  if (!isPlainObject(input) ||
      Object.keys(input).length !== 1 ||
      !["accepted", "rejected"].includes(input.status)) {
    return error(422, "invalid_review", "Specify status as accepted or rejected.");
  }

  const result = await env.DB.prepare(
    `UPDATE contributions
        SET review_status = ?, reviewed_at = ?
      WHERE id = ?`
  )
    .bind(input.status, new Date().toISOString(), id)
    .run();

  if (Number(result.meta?.changes ?? 0) === 0) {
    return error(404, "not_found", "No contribution has that receipt ID.");
  }

  return json({ receiptId: id, status: input.status });
}

function authorizeAdmin(request, env) {
  if (typeof env.ADMIN_TOKEN !== "string" || env.ADMIN_TOKEN.length < 32) {
    return error(503, "admin_unavailable", "Administrative access is not configured.");
  }

  if (request.headers.get("authorization") !== `Bearer ${env.ADMIN_TOKEN}`) {
    return error(401, "unauthorized", "A valid administrator token is required.", {
      "WWW-Authenticate": "Bearer"
    });
  }

  return null;
}

function getRateLimitKey(request) {
  const installationId = request.headers.get("x-installation-id") ?? "";
  if (INSTALLATION_ID_PATTERN.test(installationId)) {
    return `installation:${installationId.toLowerCase()}`;
  }

  const connectingIp = request.headers.get("cf-connecting-ip") ?? "unknown";
  return `fallback:${connectingIp}`;
}

export function validateContribution(value) {
  if (!isPlainObject(value)) {
    return invalid("The contribution must be a JSON object.");
  }

  const keys = Object.keys(value);
  const unexpected = keys.find((key) => !ALLOWED_FIELDS.has(key));
  if (unexpected) {
    return invalid(`Unexpected field: ${unexpected}.`);
  }

  const missing = REQUIRED_FIELDS.find((field) => !Object.hasOwn(value, field));
  if (missing) {
    return invalid(`Missing required field: ${missing}.`);
  }

  if (value.SchemaVersion !== 2) {
    return invalid("SchemaVersion must be 2.");
  }
  if (!boundedString(value.AppVersion, 1, 64)) {
    return invalid("AppVersion must contain 1 to 64 characters.");
  }
  if (!isDateTime(value.RecordedAtUtc)) {
    return invalid("RecordedAtUtc must be an ISO 8601 date and time with a timezone.");
  }
  if (!token(value.Callsign, 1, 16)) {
    return invalid("Callsign must contain 1 to 16 uppercase letters or digits.");
  }
  if (!aliasMap(value.AirlineAliases)) {
    return invalid("AirlineAliases must contain at most 50 uppercase alphanumeric mappings.");
  }
  if (!boundedString(value.OriginalTranscript, 1, 2000)) {
    return invalid("OriginalTranscript must contain 1 to 2,000 characters.");
  }
  if (value.GeneratedCommand !== undefined && value.GeneratedCommand !== null &&
      !boundedString(value.GeneratedCommand, 0, 500)) {
    return invalid("GeneratedCommand must be null or contain at most 500 characters.");
  }
  if (typeof value.WasBestEffort !== "boolean") {
    return invalid("WasBestEffort must be true or false.");
  }
  if (!boundedString(value.CorrectedTranscript, 1, 2000)) {
    return invalid("CorrectedTranscript must contain 1 to 2,000 characters.");
  }
  if (!boundedString(value.ExpectedCommand, 1, 500)) {
    return invalid("ExpectedCommand must contain 1 to 500 characters.");
  }
  if (!boundedString(value.ControllerPosition, 0, 64)) {
    return invalid("ControllerPosition must contain at most 64 characters.");
  }
  if (value.ActiveStar !== undefined && value.ActiveStar !== null &&
      !token(value.ActiveStar, 1, 32)) {
    return invalid("ActiveStar must be null or an uppercase alphanumeric token.");
  }
  if (!tokenArray(value.RouteFixes, 100, 16)) {
    return invalid("RouteFixes must contain at most 100 unique uppercase alphanumeric tokens.");
  }

  return { ok: true };
}

function normalizeContribution(value) {
  const aliases = Object.fromEntries(
    Object.entries(value.AirlineAliases).sort(([left], [right]) =>
      left.localeCompare(right)
    )
  );
  const routeFixes = [...value.RouteFixes].sort();

  return {
    SchemaVersion: value.SchemaVersion,
    AppVersion: value.AppVersion,
    RecordedAtUtc: value.RecordedAtUtc,
    Callsign: value.Callsign,
    AirlineAliases: aliases,
    OriginalTranscript: value.OriginalTranscript,
    GeneratedCommand: value.GeneratedCommand ?? null,
    WasBestEffort: value.WasBestEffort,
    CorrectedTranscript: value.CorrectedTranscript,
    ExpectedCommand: value.ExpectedCommand,
    ControllerPosition: value.ControllerPosition,
    ActiveStar: value.ActiveStar ?? null,
    RouteFixes: routeFixes
  };
}

function aliasMap(value) {
  if (!isPlainObject(value)) {
    return false;
  }

  const entries = Object.entries(value);
  return entries.length <= 50 && entries.every(
    ([key, mappedValue]) =>
      boundedString(key, 1, 64) &&
      !/[\u0000-\u001f\u007f]/.test(key) &&
      token(mappedValue, 1, 16)
  );
}

function tokenArray(value, maximumItems, maximumLength) {
  return Array.isArray(value) &&
    value.length <= maximumItems &&
    new Set(value).size === value.length &&
    value.every((item) => token(item, 1, maximumLength));
}

function token(value, minimumLength, maximumLength) {
  return boundedString(value, minimumLength, maximumLength) && TOKEN_PATTERN.test(value);
}

function boundedString(value, minimumLength, maximumLength) {
  return typeof value === "string" &&
    value.length >= minimumLength &&
    value.length <= maximumLength;
}

function isDateTime(value) {
  return typeof value === "string" &&
    /T/.test(value) &&
    /(Z|[+-]\d{2}:\d{2})$/i.test(value) &&
    !Number.isNaN(Date.parse(value));
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function invalid(message) {
  return { ok: false, message };
}

async function sha256(value) {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return [...new Uint8Array(digest)]
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

function json(body, status = 200, extraHeaders = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      ...extraHeaders
    }
  });
}

function error(status, code, message, headers = {}) {
  return json({ error: code, message }, status, headers);
}
