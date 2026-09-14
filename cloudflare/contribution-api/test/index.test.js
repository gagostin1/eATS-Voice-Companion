import assert from "node:assert/strict";
import test from "node:test";

import worker, { validateContribution } from "../src/index.js";

function validContribution(overrides = {}) {
  return {
    SchemaVersion: 2,
    AppVersion: "0.3.0",
    RecordedAtUtc: "2026-09-13T20:00:00.000Z",
    Callsign: "DAL1288",
    AirlineAliases: { "BLUE STREAK": "JIA", DELTA: "DAL" },
    OriginalTranscript: "Delta twelve eighty-eight cross Aussie at one three thousand.",
    GeneratedCommand: "DAL1288 R",
    WasBestEffort: true,
    CorrectedTranscript: "Delta 1288 cross OZZZI at and maintain 13000.",
    ExpectedCommand: "DAL1288 XOZZZI@130",
    ControllerPosition: "Atlanta Center",
    ActiveStar: null,
    RouteFixes: ["OZZZI"],
    ...overrides
  };
}

test("accepts a valid schema-v2 contribution", () => {
  assert.deepEqual(validateContribution(validContribution()), { ok: true });
});

test("rejects fields that could carry audio or local paths", () => {
  const result = validateContribution(validContribution({ AudioPath: "C:\\recording.wav" }));
  assert.equal(result.ok, false);
  assert.match(result.message, /Unexpected field/);
});

test("rejects unsupported schemas and oversized transcripts", () => {
  assert.equal(validateContribution(validContribution({ SchemaVersion: 1 })).ok, false);
  assert.equal(
    validateContribution(validContribution({ CorrectedTranscript: "x".repeat(2001) })).ok,
    false
  );
});

test("health endpoint does not require storage", async () => {
  const response = await worker.fetch(new Request("https://example.test/health"), {});
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { status: "ok", schemaVersion: 2 });
});

test("submission is stored once and returns a stable receipt", async () => {
  const database = new FakeDatabase();
  const env = {
    DB: database,
    SUBMISSION_RATE_LIMITER: { limit: async () => ({ success: true }) }
  };
  const makeRequest = () => new Request("https://example.test/v1/contributions", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Installation-Id": "63bd5c25-0c57-4f5d-8235-dcbf5ee0e44c"
    },
    body: JSON.stringify(validContribution())
  });

  const first = await worker.fetch(makeRequest(), env);
  const firstBody = await first.json();
  assert.equal(first.status, 202);
  assert.equal(firstBody.status, "accepted");
  assert.match(firstBody.receiptId, /^[a-f0-9]{64}$/);

  const second = await worker.fetch(makeRequest(), env);
  const secondBody = await second.json();
  assert.equal(second.status, 200);
  assert.equal(secondBody.status, "duplicate");
  assert.equal(secondBody.receiptId, firstBody.receiptId);
  assert.equal(database.rows.size, 1);
});

test("rate-limited submissions never reach D1", async () => {
  const database = new FakeDatabase();
  const response = await worker.fetch(
    new Request("https://example.test/v1/contributions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(validContribution())
    }),
    {
      DB: database,
      SUBMISSION_RATE_LIMITER: { limit: async () => ({ success: false }) }
    }
  );

  assert.equal(response.status, 429);
  assert.equal(database.rows.size, 0);
});

class FakeDatabase {
  constructor() {
    this.rows = new Map();
  }

  prepare(sql) {
    return new FakeStatement(this, sql);
  }
}

class FakeStatement {
  constructor(database, sql) {
    this.database = database;
    this.sql = sql;
    this.values = [];
  }

  bind(...values) {
    this.values = values;
    return this;
  }

  async run() {
    if (!this.sql.includes("INSERT OR IGNORE")) {
      throw new Error("Unexpected fake query");
    }

    const [id, receivedAt, schemaVersion, appVersion, callsign, expectedCommand, payloadJson] =
      this.values;
    if (this.database.rows.has(id)) {
      return { meta: { changes: 0 } };
    }

    this.database.rows.set(id, {
      id,
      receivedAt,
      schemaVersion,
      appVersion,
      callsign,
      expectedCommand,
      payloadJson
    });
    return { meta: { changes: 1 } };
  }
}
