# Contribution API maintainer guide

> [!NOTE]
> This is project infrastructure for maintainers. People installing eATS Voice
> Companion do not need a Cloudflare account, their own Worker, or their own
> database. The released app uses the official hosted endpoint.

This Cloudflare Worker receives sanitized schema-v2 correction cases from the
eATS Voice Companion contribution outbox. It stores JSON only: the accepted
schema contains no audio, device details, user identity, or local file paths.

The source remains in the public repository so the collection behavior and its
privacy controls can be audited, tested, maintained, and reproduced by forks.

## Maintainer deployment

Requirements: a free Cloudflare account and Node.js 20 or newer.

```powershell
cd cloudflare/contribution-api
npm install
npx wrangler login
npm run db:create
```

For the existing production project, the D1 identifier is already recorded in
`wrangler.jsonc`. When creating a replacement database, update that identifier
with the value returned by Wrangler. Then initialize the remote database:

```powershell
npm run db:migrate:remote
```

Create a unique administrator token of at least 32 characters and enter it only
when Wrangler prompts. Do not commit it:

```powershell
npx wrangler secret put ADMIN_TOKEN
npm run deploy
```

The deployment output provides the `workers.dev` base URL. Verify it with:

```powershell
Invoke-RestMethod https://YOUR-WORKER.workers.dev/health
```

## Endpoints

- `GET /health` reports endpoint availability and schema version.
- `POST /v1/contributions` validates, deduplicates, and queues a correction.
- `GET /v1/admin/contributions?status=pending&limit=50` lists review items.
- `PATCH /v1/admin/contributions/{receiptId}` accepts or rejects an item with
  `{ "status": "accepted" }` or `{ "status": "rejected" }`.

Administrative endpoints require `Authorization: Bearer <ADMIN_TOKEN>`. The
public submission endpoint is limited to 100 requests per anonymous installation
per minute. A desktop client secret is intentionally not used because secrets
embedded in distributed applications can be extracted.

## Local checks

```powershell
npm test
npm run check
npm run db:migrate:local
npm run dev
```
