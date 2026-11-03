# RC Bunny

A simple C# version of ebunny: two projects that share one database.

| Project | What it is | Address |
| --- | --- | --- |
| `src/RcBunny.Frontend` | Public website and customer My Account. Razor Pages, plus one small React island (the plan filter). | http://localhost:5100 |
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
| admin@example.com | Administrator | everything |
| csr@example.com | Customer Service | Corral, Web Messages |
| pricing@example.com | Pricing | Plans & Rates |
| finance@example.com | Finance | Corral, and reversing payments |

To start over with fresh sample data, stop both apps and delete `rcbunny-dev.db` in the repo root. Do this after pulling changes that add tables, too: the database is created once, on first start.

## What's in it

**Website**
- Home page with featured plans and a zip code box.
- Plans page: prices for your zip code (residential or business), with buttons to filter by contract length.
- Contact Us form. Messages show up in the admin under Web Messages.
- My Account: balance, plan, bills, payments, and paying the bill. No card processor is connected, so a payment is recorded but no card is charged.

**Admin**
- Corral (customer service and CRM), with the same menu as ebunny's:
  - Customer Search (by account, name, email, phone, address or ESIID; filter by type and status) and the account page: details, bills, payments, notes, texts, calls, ERCOT, exceptions and product orders. People with the "refunds" permission can reverse a payment.
  - ESIID Lookup: who's on a meter and what's been sent to ERCOT for it.
  - Create Order and Create Order - Biz: enter the zip, pick a plan, and the new account is created. With an ESIID the switch request goes to ERCOT; without one the account goes in the "No ESIID" queue.
  - SMS and Phone Calls. No text provider is connected, so texts are saved but not delivered.
  - CRM: Pipeline (open leads by stage, win rate), Leads, Follow-ups (overdue ones in red), Partners & Brokers.
  - Products and Product Orders.
  - ERCOT: every request and its answer. There's no ERCOT connection yet, so people record the answer; a rejection puts the account in the "Rejected by ERCOT" queue.
  - Exceptions: queues of accounts that need someone to look at them.
- Plans & Rates: today's rate for every plan in every market. Add a rate (today or a future date), hide or show a plan.
- Web Messages: messages from the website.
- Staff: add people, change their role, turn their sign-in off, and choose what each role can do.

## How access works

- Each employee has one role. A role is a list of permissions: `corral`, `refunds`, `plans`, `messages`, `staff`.
- The server checks every request with `[RequirePermission("...")]` (`Backend/Auth/Permissions.cs`). It reads the person's current role from the database, cached for 30 seconds. Changing someone's role or turning them off clears that cache, so it takes effect on their next click.
- The admin app hides menus, pages and buttons you can't use (`ClientApp/src/auth.tsx`). It re-checks who you are every minute, when the tab gets focus, and whenever the server says no.

## Caching

- Website: plan prices are kept in memory for a minute (`Frontend/Services/Catalog.cs`), and the finished Home and Plans pages are cached for a minute, so they load instantly. My Account is never cached.
- Admin: each screen remembers its last data, so going back to it shows instantly while it refreshes.

## Where things are

```
src/RcBunny.Backend/
  Program.cs              sign-in cookie, controllers, serves the React app
  Data/                   Entities.cs (every table), AppDbContext, Pricing, Passwords (the website uses these files too)
  Auth/Permissions.cs     the permission list and [RequirePermission]
  Controllers/            Auth, Plans, Messages, Staff, and Corral's: Customers, Orders, Communications (SMS, calls),
                          Crm, Products, Ercot, Queues
  Seed/                   Seeder.cs and ebunny's sample data (JSON)
  ClientApp/src/          main.tsx (menu and routes), api.ts, auth.tsx, pages/*.tsx, styles.css
                          pages/corral/Corral.tsx is Corral's sidebar and routes; its pages sit next to it
src/RcBunny.Frontend/
  Program.cs              Razor Pages, page caching, customer sign-in
  Services/Catalog.cs     markets and prices, cached
  Pages/                  Index, Plans, Contact, MyAccount/*, Shared/_Layout
  ClientIslands/src/      PlanFilter.tsx (the plan filter island)
  wwwroot/css/            ebunny's styles
```

## SQL Server

Development uses a SQLite file, so there's nothing to set up. Outside Development both apps use SQL Server with the `ConnectionStrings:Default` in `appsettings.json`. Point both at the same database. The admin creates the tables and sample data the first time it starts.

## Adding a screen

1. Add a controller in `Backend/Controllers` with `[RequirePermission("...")]`.
2. Add a page in `ClientApp/src/pages`, a route in `main.tsx`, and a menu entry in the `menu` list there.
3. For a new permission, add it to `Permissions.All` and tick it for the roles that need it under Staff.
