# RC Bunny

A simple C# version of ebunny: two projects that share one database.

| Project | What it is | Address |
| --- | --- | --- |
| `src/RcBunny.Frontend` | Public website and customer My Account. ebunny's own website pages, style.css and main.js; My Account is Razor Pages with ebunny's markup. | http://localhost:5100 |
| `src/RcBunny.Backend` | Employee admin. A plain Web API plus a React + TypeScript app in `ClientApp`. | http://localhost:5200 |

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
| Admin: http://localhost:5200 | Click a sample employee on the sign-in page. Every password is `password`. |

Sample employees:

| Email | Role | Can use |
| --- | --- | --- |
| admin@example.com | Administrator (ticket team manager) | everything |
| csr@example.com | Customer Service (on the ticket team) | Corral, Ranger, Bounty |
| pricing@example.com | Pricing | Plans & Rates |
| finance@example.com | Finance | Corral, Caboose, and approving refunds, credits and debits |

To start over with fresh sample data, stop both apps and delete `rcbunny-dev.db` in the repo root. Do this after pulling changes that add tables, too: the database is created once, on first start.

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
  - Web Messages from the website, with "Make a ticket".
  - Ticket Team (in-office or 3rd party, managers), Approval Ratings, Employees.
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
                          Ranger's: Ranger, Messages; Caboose; Bounty; Sheriff's: Staff, Sheriff
  Jobs/                   Scheduler (runs the crons) and Heartbeat (the checks)
  Seed/                   Seeder.cs and ebunny's sample data (JSON)
  ClientApp/src/          main.tsx (routes), api.ts, auth.tsx, pages/*.tsx; admin.css is ebunny's admin.css unchanged,
                          styles.css gives our pages' own class names the same look
                          pages/corral, pages/ranger, pages/caboose, pages/bounty, pages/sheriff: each app's menu and routes (Corral.tsx, Ranger.tsx,
                          Caboose.tsx, Bounty.tsx, Sheriff.tsx) with its pages next to it; Sidebar.tsx is ebunny's shell (sidebar, top bar, user menu)
src/RcBunny.Frontend/
  Program.cs              customer sign-in, the site's pages and My Account
  Services/SiteEndpoints.cs  what ebunny's pages ask the server for: /shared/config/brand.js and catalog.js (plans, rates,
                          fees from the database), /site/session, the Contact Us form
  Services/Maintenance.cs the "we'll be right back" page when Sheriff switches the website off
  Pages/                  MyAccount/* (Login, Dashboard, Pay, Rewards, Support), Shared/_Layout and _MyAccount (ebunny's frame)
  wwwroot/frontend/       ebunny's index, plans, build-your-own-plan, business and contact-us pages, style.css and main.js, unchanged
  wwwroot/shared/         ebunny's boot.js, core.js, tokens.css and logos
```

## SQL Server

Development uses a SQLite file, so there's nothing to set up. Outside Development both apps use SQL Server with the `ConnectionStrings:Default` in `appsettings.json`. Point both at the same database. The admin creates the tables and sample data the first time it starts.

## Adding a screen

1. Add a controller in `Backend/Controllers` with `[RequirePermission("...")]`.
2. Add a page in `ClientApp/src/pages/<app>`, then add it to that app's menu and routes (for example `pages/ranger/Ranger.tsx`). A whole new app also needs an entry in the `menu` list and a route in `main.tsx`.
3. For a new permission, add it to `Permissions.All` and tick it for the roles that need it under Staff.
