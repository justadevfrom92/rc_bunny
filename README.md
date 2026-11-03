# RC Bunny — C# version of ebunny

The ebunny website and admin tools (originally Laravel/PHP, [`justadevfrom92/ebunny`](https://github.com/justadevfrom92/ebunny)) rebuilt on **.NET 10** as two projects that share one database:

| Project | What it is | Stack | Runs on |
| --- | --- | --- | --- |
| [`src/RcBunny.Frontend`](src/RcBunny.Frontend) | The public website and the customer **My Account** portal | ASP.NET Core **Razor Pages** reading the database directly, with small **React + TypeScript islands** | http://localhost:5100 |
| [`src/RcBunny.Backend`](src/RcBunny.Backend) | The employee admin (Corral, Sheriff, Ranger, Astro, … launcher) | ASP.NET Core **Web API** + a **React + TypeScript** single-page app (`ClientApp/`) | http://localhost:5200 |

There is no third "shared" project: the Backend owns the EF Core model (`Data/`), migrations and sample data, and the Frontend compiles the same entity files in (see `RcBunny.Frontend.csproj`). Both always agree on the schema.

## Run it

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node.js 20+.

```bash
dotnet run --project src/RcBunny.Backend
```

```bash
dotnet run --project src/RcBunny.Frontend
```

Start the Backend first: it creates the database and loads the sample data. In Development both use a local SQLite file (`rcbunny-dev.db` in the repo root), so there's nothing to set up. The first build runs `npm install` and `npm run build` for each client.

- Website: http://localhost:5100 — My Account sign-in `demo.customer` / `password`. In Development, Forgot Password shows the reset link on the page instead of emailing it.
- Admin: http://localhost:5200 — the sign-in page lists every sample employee for one-click sign-in (Development only). All passwords are `password`.

| Email | Role | Can open |
| --- | --- | --- |
| admin@example.com | Administrator | everything, including refunds |
| csr@example.com | Customer Service | Corral, Ranger, Bounty |
| pricing@example.com | Pricing (+ Walker added for them) | Lando, Astro, Walker |
| finance@example.com | Finance | Corral, Walker, Caboose, refunds |
| lead@example.com | Customer Service Lead (+ Caboose) | Corral, Bounty, Rodeo, Deputy, Ranger, Caboose |
| paige@example.com | Content Editor (+ Corral, Walker, Astro) | Corral, Lando, Astro, Walker, Rodeo |
| 3p.csr@example.com | 3rd-party rep (Brightline), Bounty taken away | Corral, Ranger |
| 3p.manager@example.com / 3p.admin@example.com | 3rd-party leads (Brightline), Deputy taken away | Corral, Ranger, Bounty, Rodeo |

### SQL Server

SQL Server is the real target. Outside Development the apps use the `ConnectionStrings:Default` in `appsettings.json` (Windows auth to `localhost`, database `RcBunny`). Point both projects at the same database; the Backend applies EF Core migrations on start.

```bash
dotnet run --project src/RcBunny.Backend --launch-profile SqlServer
```

To try it with Docker:

```bash
docker run -d --name rcbunny-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

Then set `Database__Provider=SqlServer` and `ConnectionStrings__Default=Server=localhost,1433;Database=RcBunny;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True` for both projects.

After changing an entity, add a migration (the `dotnet-ef` tool is pinned in `.config/dotnet-tools.json`):

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/RcBunny.Backend --output-dir Data/Migrations --namespace RcBunny.Data.Migrations
```

### Working on the React code

Use each client's dev server for instant reloads:

- Admin: `npm run dev` in `src/RcBunny.Backend/ClientApp`, then open http://localhost:5173. It forwards `/api` to the Backend on :5200.
- Website islands: `npm run dev` in `src/RcBunny.Frontend/ClientIslands`, then run the Frontend with `--launch-profile IslandsHotReload`. The Razor pages then load the islands from Vite.

`npm run typecheck` runs the TypeScript compiler in either folder. `dotnet publish` builds both clients and includes them.

## The website (Frontend): fast pages, no waiting

Pages are complete HTML from the server. Visitors never wait on a spinner to see the content.

- **Server-rendered with the data.** Razor Pages read plans, rates, TDSP fees and products from the database through `Services/Catalog.cs`, which keeps one in-memory snapshot (refreshed every 5 minutes, `Website:CatalogCacheSeconds`).
- **Whole-page output cache.** Public pages are cached for 60 seconds (`Website:PageCacheSeconds`), varied by `zip`, `filter`, `topic` and `plan`. A cached page comes back in a few milliseconds. My Account pages are never cached.
- **Brotli/gzip** compression. Hashed island bundles and `?v=` versioned CSS and images are cached by browsers for a year.
- **React islands** (`ClientIslands/src/islands`) only where interactivity helps: `ZipCheck` (home page zip box), `PlanBrowser` (instant plan filters, URL kept in sync), `ByopBuilder` (Build Your Own Plan with live price), `ContactForm` (sends without a reload), `UsageChart` (My Account), `ChatBubble` (My Account chat), `CookieBanner`.
  - `<island name="…" props="…">server HTML</island>` renders the full HTML version inside, plus the island's data as inline JSON. The page works and reads correctly before any JavaScript runs.
  - When the island's code arrives, it renders straight from that JSON: no API call and no loading state.
  - The page script is about 1 KB. React (~70 KB gzipped) is only downloaded on pages that have an island, and each island is its own chunk.
- ebunny's addresses still work (`/plans.html`, `/contact-us.html`, …). The Contact Us form lands in the admin under **Ranger → Web Messages**.

## The admin (Backend): caching and per-employee access checks

### Who can do what

- **Access = role + per-person changes.** A role holds app keys (`corral`, `sheriff`, …) and rights (`refunds`). Each person can also have apps or rights **added** or **taken away** individually, with a note saying why (Sheriff → Staff Directory → Edit).
- **Sign-in limits.** People can be disabled. 3rd-party company people have a `Company` and an **access end date**; after that date their sign-in stops working.
- **The server decides.** Every endpoint is guarded with `[RequirePerm("corral")]` (or `"refunds"`, …). It checks a cached *access snapshot* (`Access/AccessService.cs`), never the cookie's claims. Removing someone's access takes effect on their very next request.
- **Signing someone out.** Disabling a person or changing their password changes their security stamp, which ends every existing sign-in on its next request.
- **The client mirrors the server.** `ClientApp/src/auth.tsx` provides `useCan('refunds')` and `<Can perm>` for buttons, and `<RequirePerm perm>` for routes. The launcher greys out apps you can't open. Each app's screens are a separate code chunk, so a CSR never downloads Sheriff's code.
- **Changes reach open screens quickly.** Every API response carries `X-Access-Version`. When Sheriff changes someone's role or access, their open admin notices on its next call and refreshes their permissions: menus, buttons and guarded pages update without a reload. "Me" is also re-checked every 30 seconds and when the tab regains focus. Cached data for apps someone loses is dropped from memory.
- **Log in as** (Sheriff) only works for someone whose access is a subset of yours, shows a "Back to me" banner, and is recorded in the Timeline.
- **Timeline** (Sheriff) records sign-ins, access and role changes, log-in-as, payment reversals and cache clears.
- **Hardening.** Login is rate limited. The cookie is HttpOnly and SameSite=Strict. Every non-GET API call must carry an `X-RCB: 1` header (CSRF protection for a cookie-authenticated JSON API).

### Caching (lots of it)

| Layer | What | How long |
| --- | --- | --- |
| Browser memory (TanStack Query) | every screen's data | fresh 30 s, kept 10 min; reference data 1 h; previous page stays visible while the next loads; accounts prefetched on row hover |
| HTTP | every `GET /api` answer has an ETag | a background refresh of unchanged data is a `304` with no body |
| Server (`HybridCache`) | each person's access, customer search and stats, account detail, staff directory, plans | 10 s–10 min, removed **by tag** the moment something is saved |
| Static files | hashed JS/CSS bundles | 1 year (immutable) |

`HybridCache` is in-memory by default. On more than one server, add a Redis distributed cache (`AddStackExchangeRedisCache`); it becomes the second level automatically, and tag removals then reach every server. Sheriff → Caches explains the layers and can clear server caches by tag.

## What's ported

Everything in ebunny: all 98 tables, every admin app and menu, the whole website, checkout, shop, surveys, CMS pages and every My Account feature. Outside services (card processing, Utilibill, ERCOT/EDI, Experian, mail and SMS providers, Ollama/hosted AI) are wired at the same points ebunny calls them: with their keys unset the app records what would have been sent and says so, rather than failing.

### Database

- **All 98 ebunny tables**, column for column (snake_case names kept), generated from ebunny's Laravel migrations by `tools/schema` into `Data/Entities/Generated.cs` and `Data/AppDbContext.Generated.cs`. JSON columns are `JsonNode` / `List<string>`.
- ebunny's PHP config files (`config/*.php`: shop, cms, walker, items, history, deputy, corral, myaccount, records, admin, brand) are JSON in `Data/Config`, read through `AppConfig`.
- **Sample data for every table** (`Data/Seed`), including ebunny's plans, rates, pages, content blocks and email templates.
- **SQL Server**: one `Initial` migration in `Data/Migrations`. MySQL lets deletes cascade along several routes to the same table; SQL Server doesn't, so for SQL Server `AppDbContext.LimitCascadesForSqlServer` keeps every database cascade it can (customers' and users' first) and makes the rest client-side cascades.
- **SQLite** for local development (created straight from the model, no migrations).

Regenerate the entities after changing ebunny's migrations:

```bash
cd tools/schema && python3 parse.py ../../../ebunny/database/migrations && python3 gen.py
```

```bash
mv tools/schema/Generated.cs src/RcBunny.Backend/Data/Entities/ && mv tools/schema/AppDbContext.Generated.cs src/RcBunny.Backend/Data/
```

### Admin apps (Backend)

| App | What's there |
| --- | --- |
| **Corral** | Customer search, account pages (service, plan terms, payments with reverse/refund, bills, ledger, notes, products, ERCOT, history, contact log, queue log, documents), create order (resi and biz), ESIID lookup, exception queues and work items, SMS, phone calls, CRM pipeline (leads, follow-ups, partners), shop products and product orders |
| **Ranger** | Ticket dashboard, ticket queue with replies, internal notes, attachments, take over / join in, the ticket team (in-office and 3rd party), approval ratings, web messages |
| **Lando** | Site dashboard and health, visitors, blocked areas and IPs, cookie consents, pages, page templates, content blocks and categories, sitemap, style guide |
| **Astro** | Plans, plan groups, rates (view, update, upload, history, future-dated rates), TDSP fees, term discounts, ETFs, Build Your Own Plan products |
| **Walker** | Report status, run reports (queued and run by the scheduler, downloads as CSV/TSV/JSON/XLSX), saved reports, files, ETL feeds, legal documents, customer templates |
| **Caboose** | Money: dashboard, payments, credits and debits, refunds, deposits, receivables aging, journal, MSIDs, promo codes |
| **Bounty** | Rewards: dashboard, offers, earning rules, redemptions, members, the monthly drawing |
| **Rodeo** | Campaigns and sent emails, email templates with headers/footers and categories, suppression list, test sends, PDF templates and PDFs made, surveys (question bank, answer sets, categories, responses, survey emails) |
| **Deputy** | AI dashboard, conversations (and the ones waiting for a person: take over and reply), agents, models with a connection check, automation runs |
| **Sheriff** | Heartbeat (checks, controls such as taking the website down, control log), staff directory, add a person, roles matrix, per-person access, log in as, custom admin apps, timeline, crons, cron automations and job runs, sitemap, cached tables and caches, audit log, history, reference data and markets, integrations, connection tests and API calls |
| **+ New Admin App** | Custom apps made in Sheriff (`admin_apps`): their own menus and permission keys |

Most list/edit screens come from one metadata-driven engine: `Resources/ResourceRegistry*.cs` describes each table's columns, filters, forms and **record actions** (workflow buttons such as "Approve", "Send", "Reverse"), served by `/api/r/{key}` and drawn by the generic React screens in `ClientApp/src/resource`. Dashboards and special workflows are their own pages in `ClientApp/src/pages`.

**Scheduler** (`Domain/Jobs.cs`): a background service that runs ebunny's `et:*` commands on their cron schedules (send orders, ERCOT responses, AutoPay, scheduled payments, campaigns, survey emails, welcome packets, reward stars, rate export, sitemap), queued Walker reports, Deputy automations and heartbeat pulses. Every run is recorded in Sheriff → Crons.

### Website and My Account (Frontend)

| Area | What's there |
| --- | --- |
| Marketing | Home, Plans (zip pricing and filters), plan pages, Business, Build Your Own Plan, Contact Us |
| CMS | Every page built in Lando, with its template, content blocks, zones, `[[block\|slug\|name=value]]` includes and `{{name\|default}}` variables; the "404" page for anything not found; redirects; `sitemap.xml` and `robots.txt` |
| Sign-up | `/checkout`: address and ESIID, plan, about you (credit check, optional My Account login), review, save and finish later (resume link), frozen credit, deposit and alternatives |
| Shop | Products by category, product pages, cart (encrypted cookie, no sign-in needed), checkout in My Account paying by card or on the electricity bill, earning stars |
| Surveys | `/survey/{slug}` with categories; signed links from emails tie answers to the account |
| My Account | Dashboard, bills (PDF), pay now or schedule (and cancel), payment methods and AutoPay, products, usage, plan and renewal, moving (transfer of service), rewards (redeem, monthly drawing), refer a friend, messages, support tickets with files and ratings, shop orders, profile (contact, password, authorized users, linked accounts and switching), chat with the virtual assistant (hands over to a person) |
| Without signing in | QuickPay, create a login, forgot password or username, password reset |
| Site rules | Blocked IPs/areas and maintenance mode (Sheriff → Heartbeat) checked on every page from a 30-second cache; visits logged in the background for Lando → Visitors; cookie banner answers recorded in Lando → Cookie Consents |

The cookie banner and chat bubble are islands too: the banner's code only downloads for visitors who haven't answered yet, and the chat only talks to the server while it's open.

## Adding the next screen

1. **API**: add a controller in `src/RcBunny.Backend/Api/` with `[RequirePerm("<app>")]`. Cache reads with `HybridCache` and a tag, and remove the tag when you save.
2. **Client**: add a page in `ClientApp/src/pages/<app>/` and a lazy route under that app's `<Guard perm>` in `main.tsx`. Use query keys that start with the app key.
3. **Menu**: add the item to the app's menu in `Access/AdminApps.cs` (`P(...)` for a page, `R(...)` for a generic resource screen).

A new table usually needs no page at all: add it to ebunny's migrations, regenerate the entities, add a `ResourceDef` in `Resources/ResourceRegistry*.cs`, and link it with `R(...)`.
