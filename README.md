# RC Bunny

A simple C# version of ebunny: two projects that share one database.

| Project | What it is | Address |
| --- | --- | --- |
| `src/RcBunny.Frontend` | Public website and customer My Account. Razor Pages with ebunny's markup, style.css and main.js. | http://localhost:5100 |
| `src/RcBunny.Backend` | Employee admin. A plain Web API plus a React + TypeScript app in `ClientApp`. | http://localhost:5200/admin |

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org) 20 or newer.

> Put the project in a folder whose path has no `#` in it (for example not under `C#\`). The React build tool (Vite) can't handle `#` in paths.

```bash
git clone -b main https://github.com/justadevfrom92/rc_bunny.git
cd rc_bunny
```

Start the admin first (it creates the database and fills it with sample data):

```bash
dotnet run --project src/RcBunny.Backend
```

Then, in a second terminal:

```bash
dotnet run --project src/RcBunny.Frontend
```

The first build of each project runs `npm install` and `npm run build` for its React code, so it takes a minute or two.

| Where | Sign in |
| --- | --- |
| Website: http://localhost:5100 | My Account: `demo.customer` / `password` |
| Admin: http://localhost:5200/admin | Click a sample employee on the sign-in page. Every password is `password`. |

Sample employees:

| Email | Role | Can use |
| --- | --- | --- |
| admin@example.com | Administrator (ticket team manager) | everything |
| csr@example.com | Customer Service (on the ticket team) | Corral, Ranger, Bounty |
| pricing@example.com | Pricing | Astro (plans and rates), Lando (the website) |
| marketing@example.com | Content Editor | Lando, Rodeo (campaigns, emails, PDFs, surveys) |
| finance@example.com | Finance | Corral, Caboose, Walker (reports and files), and approving refunds, credits and debits |

## Database commands

Run these from the repo root, with both apps stopped:

```bash
dotnet run --project src/RcBunny.Backend -- migrate:fresh
```

Or from `src/RcBunny.Backend`: `dotnet run -- migrate:fresh`.

| Command | What it does |
| --- | --- |
| `migrate` | Creates the database and fills it with the sample data, if it's empty. Starting the admin does this too. |
| `migrate:fresh` | Drops the database, creates it again and fills it: every plan and rate, customers with bills, payments, notes, calls and texts, tickets, CRM, shop, rewards and finance data. Run it after pulling changes that add tables. |
| `db:show` | Lists every plan with today's rate in each market, how many rows each table has, and the admin and My Account sign-ins. |

Both migrate commands finish by printing the `db:show` list. Every sample password is `password`. Admin: `admin@example.com` and the others listed. My Account: `demo.customer`, `test.business`, `test.pastdue`, `test.deposit` and `test.moved`, or use the Test Sign-Ins on each sign-in page.

## What's in it

**Website**
- Home page with featured plans and a zip code box.
- Plans page: prices for your zip code (residential or business), with buttons to filter by contract length.
- Contact Us form. Messages show up in the admin under Ranger → Web Messages.
- My Account: balance, plan, bills, payments, and paying the bill. No card processor is connected, so a payment is recorded but no card is charged.
- My Account → Rewards: the star balance, offers to redeem (a bill credit, a gift card, a product, or an entry in the monthly drawing) and the history.
- My Account → Support: open a ticket, read the replies, answer back, and rate it once it's solved.
- When Sheriff switches the website off, every page shows a "we'll be right back" notice.

**Admin**
- Corral (customer service and CRM), everything in ebunny's Corral:
  - Customer Search: counts, your bookmarks, search by name, email, account, ticket, ESIID, address, city, zip or phone digits, and filters by type, status and exception.
  - The account page, section by section like ebunny's: flags (some show a banner), balances with pending credits and debits, the Status and Customer Service action menus (all 34 actions: run credit, send to market, disconnect, move out, cancel, payments, payment plans, transfers, meter requests…), contact details, products, shop orders, service and billing address, stars (spend and recalculate), status, plan and marketing details, files (uploads, and welcome letter / EFL / terms / YRAC / invoice generated in English or Spanish as PDFs to view, print, email or regenerate), notes with category, action and priority, calls, texts, billing summary, usage by month, bill details, payments (reverse), payment methods (AutoPay, remove), plan change log (activate, welcome packet, edit rate), address change log, ERCOT transactions (unlink, cancel), order-flow queues, the warehouse row, and logs.
  - Every change to an account is recorded with who made it; the logs (Account Attributes, Products, Payments, Files, EDI, Emails, Notes, Calls, Logins, Queues) and the account ticket show it. Every ID in a table opens that row's page, where it can be edited or (with the "delete" right) deleted.
  - ESIID Lookup; Create Order (residential and business) and Renew / Change Plan; SMS (filters, waiting on a reply, conversation, send); Phone Calls (filters, transcript); New Contact; resolutions on calls and texts.
  - CRM: Pipeline (value, weighted value, win rate, by source, recent wins), Leads (stages, activities, follow-ups, lost reasons, won accounts), Follow-ups (overdue, today, coming up, done), Partners & Brokers (commission earned).
  - Products: the website Shop's catalog, product pages, orders and their status, and ordering for a customer by phone (cart, protection plans on the bill, stars).
  - ERCOT (filters by ESIID, account, tracking, type, purpose, status, dates) and the Exceptions work queues.
  - Nothing outside is connected yet (Stripe, Experian, Twilio, email, ERCOT). Those actions record what would have been sent and say so.
- Ranger (support tickets):
  - Dashboard (unassigned, mine, by status, approval) and Tickets (filter by status and by who has them).
  - A ticket: reply to the customer or add an internal note the customer never sees, change status and priority, pick it up. Ticket team managers can also assign it to anyone or take it over.
  - Web Messages from the website: reply (logged on the matching account), resolution, Mark Handled.
  - Ticket Team (in-office or 3rd party, managers), Approval Ratings, Employees.
- Lando (the website):
  - Dashboard: who is on the website now (pages check in every minute), views by hour, site health, top pages, who was turned away, the latest website changes.
  - Site Health, Visitors (every page view, by date, IP, page or visitor), Blocked IPs and Blocked Areas (the website shows "Not available" instead), Cookie Consents (every cookie banner choice and the cookies the browser had).
  - Pages: every website page. Built-in pages (home, plans, Build Your Own Plan, business, contact us) take components in their top zone; the rest are built from content blocks on a template and shown at their own path (e.g. /locations/houston), with a price grid, redirects and an AMP version. Each page has its Activity and Edits. Rebuild Sitemap writes /sitemap.xml.
  - Page Templates, Content Blocks (HTML with [[widgets]]; a block with {{variables}} is a module each page fills in), and the Style Guide (every CSS variable; try a color in your browser for 5 days, copy the changed CSS).
- Walker (files and reports):
  - Report Status: every report run, Running, Errors & Did Not Finish (still running after 60 minutes) and Completed; it updates live. A run's page shows its settings, its file and the first rows.
  - Run a Report: the report library (orders, notes, phonecalls, contacts, exceptions, payments, bills, receivables, ERCOT, stars). Reports run in the background and save Excel, CSV, tab-separated or JSON files under storage/reports. Each report's Export Columns pick the file's columns by model key (customer.name), or as a key array.
  - Saved Reports (a report's settings, kept to run again; yours and shared ones), and Orders, Notes and Phonecalls (the customer service reports with your own runs).
  - Files: upload and preview Excel, CSV, PDF, text and zip files in folders (ETL, Legal Documents, Customer Templates, Finance, Utility / ERCOT, Vendor…). ETL Feeds: the scheduled files in and out, with every run and its errors.
- Your Account (the menu under your name, top right): Notifications (the number on your avatar: new tickets, customers writing back, website emails, reports and files ready, system health), My Files (every report you ran, with its file; a popup says when one is ready) and Settings (which notifications you get; Switch User when testing).
- Rodeo (marketing):
  - Dashboard, and Campaigns: email or text campaigns with a goal, a live audience count (opted-in customers only; suppressed and recently contacted ones are skipped), a preview, Send Now or a schedule (the Scheduled campaigns job sends it), and results: the funnel, opens by day, recipients and who converted. Click heat maps for campaigns and templates.
  - Emails: the overview by template and category, every email sent (as the customer got it), templates with a live preview and Send Test (to you, addresses, accounts, bookmarks, a team or a customer category; with history and Send Again), headers and footers, categories, and the suppression list.
  - PDFs: PDF templates (simple markup with fill-ins) previewed page by page for any account, Make PDF for an account, test PDFs by email, PDFs made, and PDF headers and footers.
  - Surveys: the builder (questions from the question bank, by category), results (NPS, scales, charts, written answers, CSV export), responses, the question bank, answer sets, categories, and Survey Emails (the 90-day check-in and farewell survey, sent daily). Customers answer on the website at /survey/{slug}; links in emails are signed, so the answer is tied to that customer.
  - Email goes out by SMTP when `Integrations:Smtp:Host` (and Port, User, Password, From) is set; until then emails are written to storage/mail-log. Campaign and survey emails are logged on each account.
- Deputy (AI agents on open-weight models):
  - Dashboard (agents, their models, 30-day results), Conversations (texts and the My Account chat: Needs a Person and Live Now on top, then every conversation), and each conversation's transcript, live: Take Over, reply, Hand Back to Bot, Call, Close, and the resolution.
  - Agents (which model and fallback each runs on, its instructions; Test sends a real message to the model) and Models (downloaded or hosted; Check asks the server if it's there).
  - Models answer through Ollama (`Integrations:LocalModels:Url`, e.g. http://localhost:11434) or any OpenAI-compatible API (`Integrations:OpenModels:Url`, e.g. https://host/v1, and `Integrations:OpenModels:ApiKey`). Set them in appsettings or as environment variables (`Integrations__OpenModels__Url`) for both apps, since the website's chat bubble asks the bot too.
  - On the website, My Account has the chat bubble: the bot answers, "Talk to a person" (or asking for one) puts it in Needs a Person, and a person's replies show in the bubble.
- Caboose (finance):
  - Dashboard: money in over the last 30 days, what's waiting for approval, deposits, aging.
  - Payments: everything paid, by date, status or account. Record a payment taken by check, money order, cash or card; a "Deposit" payment goes toward the deposit instead of the balance.
  - Credits & Debits and Refunds: anyone with Caboose can ask; someone with the `refunds` permission approves, and nobody can approve their own request. A refund goes requested → approved → paid, and can't be more than the credit balance (or the deposit held).
  - Deposits (due and held), Receivables Aging (by days past due), and the Journal: the double-entry lines for the accounting system, on screen or as CSV.
  - MSIDs & Promo Codes, with how many customers used each.
- Bounty (rewards):
  - Dashboard: members, stars earned and spent, rewards waiting to be sent, this month's drawing.
  - Offers (what stars buy) and Earning Rules. "Pay a bill" is given out every night by the Reward stars job; staff give the others from a member's page.
  - Redemptions: gift cards and products waiting to be sent, with "Mark sent".
  - Members: look someone up, see their history, give or take stars, redeem for them on the phone.
  - Monthly Drawing (draw a random winner from this month's entries, one per month) and Drawing Winners.
- Sheriff (settings):
  - Heartbeat: checks (database, website, jobs) every 5 minutes or on demand, a switch to take the website down for maintenance, and the control log.
  - Staff Directory (add people, change roles, turn sign-ins off, set passwords) and Roles (tick what each role can do, add roles).
  - Timeline of sign-ins, access changes, heartbeat switches and job runs.
  - Crons: the scheduled jobs (heartbeat checks, AutoPay, closing solved tickets, reward stars) with "Run now", and their runs.
  - Cron Automations: AI agents that run on a schedule (cron times shown in plain words, with the next run), Pause/Resume, add and edit, and each one's runs (also in the Timeline).
  - Caches, Markets (delivery charges and zip codes), and Integrations (which outside services have settings; none is connected yet).

## How access works

- Each employee has one role. A role is a list of permissions: `corral`, `refunds`, `delete`, `plans`, `caboose`, `bounty`, `ranger`, `sheriff`.
- The server checks every request with `[RequirePermission("...")]` (`Backend/Auth/Permissions.cs`). It reads the person's current role from the database, cached for 30 seconds. Changing someone's role or turning them off clears that cache, so it takes effect on their next click.
- The admin app hides menus, pages and buttons you can't use (`ClientApp/src/auth.tsx`). It re-checks who you are every minute, when the tab gets focus, and whenever the server says no.

## Caching

- Website: plan prices are kept in memory for a minute (`Frontend/Services/Catalog.cs`), and the finished Home and Plans pages are cached for a minute, so they load instantly. My Account is never cached.
- Admin: each screen remembers its last data, so going back to it shows instantly while it refreshes.

## Where things are

```
src/RcBunny.Backend/
  Program.cs              sign-in cookie, controllers, serves the React app
  Data/                   Entities.cs (every table), AppDbContext, Pricing, Passwords, Rewards (the website uses these files too)
  Auth/Permissions.cs     the permission list and [RequirePermission]
  Controllers/            Auth, Plans; Corral's: Customers, Orders, Communications (SMS, calls), Crm, Products, Ercot, Queues;
                          Account (notifications, My Files, settings); Rodeo's: Rodeo, RodeoEmails, RodeoPdfs, RodeoSurveys; Deputy; Ranger's: Ranger, Messages; Lando; Astro; Walker; Caboose; Bounty; Sheriff's: Staff, Sheriff; Menu (the sidebar counts)
  Walker/Reports.cs       Walker's reports: their filters, queries and columns, and the background run that writes the file
  Jobs/                   Scheduler (runs the crons) and Heartbeat (the checks)
  Seed/                   Seeder.cs and ebunny's sample data (JSON)
  ClientApp/src/          main.tsx (routes), api.ts, auth.tsx, pages/*.tsx; admin.css is ebunny's admin.css unchanged,
                          styles.css gives our pages' own class names the same look
                          pages/corral, pages/ranger, pages/lando, pages/astro, pages/walker, pages/rodeo, pages/deputy, pages/caboose, pages/bounty, pages/sheriff: each app's menu and routes
                          (Corral.tsx, Ranger.tsx, Lando.tsx, Astro.tsx, Walker.tsx, Rodeo.tsx, Deputy.tsx, Caboose.tsx, Bounty.tsx, Sheriff.tsx) with its pages next to it; Sidebar.tsx is ebunny's shell (sidebar, top bar, user menu)
src/RcBunny.Frontend/
  Program.cs              customer sign-in, the site's pages and My Account
  Services/SiteEndpoints.cs  what main.js asks the server for: /shared/config/brand.js and catalog.js (plans, rates,
                          fees from the database), /site/session (the page view Lando counts, and blocking), /site/ping,
                          /site/consent, /site/components, /sitemap.xml, /amp/<page>, the Contact Us form
  Services/SiteContent.cs Lando content as HTML: [[block]] codes, widgets, zones, price grids
  Pages/LandoPage.cshtml  any other path: the Lando page there (redirects, drafts and unknown paths are 404)
  Services/Maintenance.cs the "we'll be right back" page when Sheriff switches the website off
  Pages/                  Index, Plans, BuildYourOwnPlan, Business, ContactUs (ebunny's pages, at the same addresses: /plans.html…),
                          MyAccount/* (Login, Dashboard, Pay, Rewards, Support), Shared/_Layout and _MyAccount (ebunny's frame)
  wwwroot/frontend/       ebunny's style.css and main.js, unchanged
  wwwroot/shared/         ebunny's boot.js, core.js, tokens.css and logos
```

## SQL Server

Development uses a SQLite file, so there's nothing to set up. Outside Development both apps use SQL Server with the `ConnectionStrings:Default` in `appsettings.json`. Point both at the same database. The admin creates the tables and sample data the first time it starts.

## Adding a screen

1. Add a controller in `Backend/Controllers` with `[RequirePermission("...")]`.
2. Add a page in `ClientApp/src/pages/<app>`, then add it to that app's menu and routes (for example `pages/ranger/Ranger.tsx`). A whole new app also needs an entry in the `menu` list and a route in `main.tsx`.
3. For a new permission, add it to `Permissions.All` and tick it for the roles that need it under Staff.
