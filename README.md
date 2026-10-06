# Ordex

Pre-order, delivery, stock and profit management for import / pre-order shops.
Built mobile-first, because most users work from their phones.

**Stack:** .NET 10 · ASP.NET Core MVC · Dapper · SQL Server · Bootstrap 5 (self-hosted, no CDN)

---

## 1. Quick start

**You need:** .NET 10 SDK and SQL Server (2019+, Express or LocalDB are fine).

1. Create your settings file (it is **not** in Git – it holds passwords):
   ```bash
   cp src/Ordex.Web/appsettings.example.json src/Ordex.Web/appsettings.json
   ```
   *(Windows: `copy src\Ordex.Web\appsettings.example.json src\Ordex.Web\appsettings.json`)*

   Then set the connection string in `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "Default": "Server=.;Database=OrdexDb;Trusted_Connection=True;TrustServerCertificate=True"
   }
   ```
2. Run:
   ```bash
   cd src/Ordex.Web
   dotnet run
   ```
3. Open http://localhost:5150 and sign in with:

   | Username | Password |
   |---|---|
   | `superadmin` | the `Seed:SuperAdminPassword` you set (`Change-Me-Now!1` in the example) |

   **Change this password right away** (avatar menu → Change password), or set
   `Seed:SuperAdminPassword` before the first run.

On first start the app **creates the database**, runs every script in `/database`,
adds a starter company (*My Company* / *Main Shop*) and creates the SuperAdmin.
Nothing to run by hand. (To run the scripts yourself instead, set
`Database:AutoMigrate` to `false` and execute `database/001…006` in order in SSMS.)

### Settings you may want to change (`appsettings.json`)
| Key | What it does |
|---|---|
| `ConnectionStrings:Default` | SQL Server connection |
| `App:PublicBaseUrl` | Address used in customer tracking links, e.g. `https://ordex.yourshop.com`. Leave empty to use the address the site is opened with. |
| `App:TimeZone` | Business time zone (default `Asia/Dhaka`) |
| `Seed:SuperAdmin*` | First SuperAdmin account (created only on an empty database) |
| `Security:*` | Rate limits, upload size, session length |

> `appsettings.json` and `appsettings.*.json` are git-ignored. Only `appsettings.example.json`
> (no secrets) is committed. On the server you can also use environment variables,
> e.g. `ConnectionStrings__Default`.

---

## 2. What the app does

| Your old way | In Ordex |
|---|---|
| Pre-order Excel rows | **Orders**: customer, social link, mobile, address, photo, size, SGD price, BDT cost, selling price, advance. **Due** and **Profit** are calculated. |
| Colouring delivered rows | One tap: **Go for delivery → Delivered** (card turns green) or **Return** (with reason). |
| Returns Excel | A return **moves the item to Stock automatically** with its reason. |
| Extra items sheet (weight balancing) | **Stock → Add extra item** (name, image, link, SGD, BDT). |
| Paper calculation | **Profit & loss**: purchase SGD + BDT, collection, expenses, net profit for any period. |
| "How much SGD do we pay this month?" | **SGD payable**: month-by-month, batch-by-batch, with a *Paid* tick. Shown on the dashboard. |

### New order screen (built for the phone)
1. **Customer** – search by mobile number *or* name. Returning customers show their order
   count and last order date; tap one and their details fill in (only the delivery address
   stays open for editing). No match? **New customer** opens the form with what you typed.
   The mobile number is the customer's key inside a business unit.
2. **Order** – order date and purchase batch. **New batch** creates a batch on the spot.
3. **Products** – add several products for the same customer at once (up to 15): copy a card
   for another size, remove a card, add a photo per product. Each product becomes its own
   order (own number and delivery status); all are saved in one transaction. A sticky bar
   shows the running total and due.

### Customer tracking link (no login, no OTP)
Every order has a private link: `https://your-site/t/{token}`.
* The **order page** has a *Customer link* card: copy, **Send on WhatsApp** (message ready,
  `880` added to BD numbers) or the phone's share sheet. The **customer page** sends the links
  of all open orders in one WhatsApp message.
* The customer sees: shop name and call button, status with a 3-step progress bar
  (placed → on the way → delivered, or returned), product photo/size, price, advance,
  amount to pay on delivery, and their other open orders. **Never** cost, profit or notes.
* The token is 128 random bits – it can't be guessed. *Replace with a new link* on the order
  page makes the old link stop working. Pages are `no-store`, `noindex` and send no referrer.
* Links stop working if the order is deleted or the company is switched off.

### Order status flow

```
Pre-order ──Go for delivery──► Out for delivery ──Delivered──► Delivered
    ▲                               │       │                      │
    └────────Back to pre-order──────┘       └──────Return──────────┴──► Returned ──► Stock
```
The rules live in one place: `Ordex.Core/Common/OrderWorkflow.cs`.

### Purchase batches & exchange rate
A **batch** is one buying trip with that day's rate (1 SGD = ? BDT). Pick the batch on an
order and the BDT cost fills itself (SGD × rate); you can still type over it.
Orders without a batch use the company's *default rate*.

### Money – two views (`dbo.fn_FinanceSummary`)
* **Cash (your paper method):**
  Collection = advance + collected on delivery + stock sales − refunds
  Net profit = Collection − purchase cost (orders + extra items) − expenses
* **Earned:** margin on delivered orders (money received − cost) + money kept on returns
  + stock-sale margin − expenses.

---

## 3. Users, companies & data visibility

Every business table has **`CompanyId`** and **`BusinessUnitId`**.

| Role | CompanyId | BusinessUnitId | Sees |
|---|---|---|---|
| SuperAdmin | 0 | 0 | everything |
| Admin | company | 0 *(or one unit)* | the whole company |
| Staff | company | unit | one business unit |

**Companies & business units**

| | SuperAdmin | Admin |
|---|---|---|
| Company | add, edit, active/inactive, delete | edit own company details (*Company profile*) – code locked |
| Business unit | add, edit, active/inactive, delete | edit only |

Delete only works while a company / unit has no users or records – otherwise switch it off.
Switching a company or unit off signs its users out within 5 minutes.

`0` means "all". Every query applies
`(@CompanyId = 0 OR CompanyId = @CompanyId) AND (@BusinessUnitId = 0 OR BusinessUnitId = @BusinessUnitId)`,
and every *get by id* goes through the same filter, so a user can't open someone else's
record by changing the URL. Users who can see more than one unit pick the company/unit on
create forms (reusable `ScopeFields` component).

---

## 4. Architecture

```
src/
├─ Ordex.Core            ← business layer (no SQL, no ASP.NET)
│   ├─ Entities/           tables as classes
│   ├─ Models/             inputs (with validation), list rows, report models
│   ├─ Abstractions/       repository + service interfaces, ICurrentUser, IClock, IUnitOfWork
│   ├─ Services/           ALL business rules and transactions
│   └─ Common/             TenantScope, PagedResult, ServiceResult, Messages, OrderWorkflow
├─ Ordex.Infrastructure  ← data access
│   ├─ Data/               DbSession (1 connection/request), UnitOfWork, RepositoryBase
│   ├─ Repositories/       Dapper SQL – the only place that talks to the database
│   ├─ Database/           DatabaseMigrator (runs /database/*.sql)
│   ├─ Files/              LocalFileStorage (safe image uploads)
│   └─ Security/           password hasher, business clock
└─ Ordex.Web             ← MVC
    ├─ Controllers/        thin: validate → call service → show result
    ├─ Middleware/         GlobalExceptionMiddleware, SecurityHeadersMiddleware
    ├─ Infrastructure/     security setup, current user, formatting, CSV
    ├─ ViewComponents/     ScopeFields, BatchSelect, CategorySelect
    └─ Views/, wwwroot/    mobile-first UI, light/dark theme
database/                ← 001 schema · 002 views/functions · 003 report procedures · 004 seed
                           005 currencies · 006 customer tracking links
```

**Rules of the codebase**
* Controller → Service → Repository. Controllers never touch SQL; repositories hold no rules.
* Writes use **Dapper with inline, parameterised SQL**. Reports use **stored procedures**
  (`usp_Dashboard_Summary`, `usp_Report_ProfitLoss`, `usp_Report_MonthlyPayable`).
* Multi-table saves run in **one transaction** via `IUnitOfWork.ExecuteAsync(...)`:
  order + customer + order number + history, or return + stock item. If any step fails, all of it rolls back.
* Status changes use an **optimistic check** (`WHERE Status = @ExpectedStatus`), so a double
  tap or two phones can't apply the same change twice.
* Order numbers (`ORD-2610-0001`) come from `DocumentSequence` with `UPDLOCK, HOLDLOCK`, so
  two users can never get the same number.
* Services return `ServiceResult`. All user-facing text is in `Core/Common/Messages.cs`.

### Adding a new feature (recipe)
1. Entity in `Core/Entities`, input model in `Core/Models` (validation attributes).
2. Repository interface in `Core/Abstractions/Repositories` → Dapper implementation in
   `Infrastructure/Repositories` (inherit `RepositoryBase`, use `ScopeFilter("x")`).
3. Service interface + implementation in Core (rules, scope, transaction).
4. Register both in the two `DependencyInjection.cs` files.
5. Thin controller inheriting `AppController`; views reuse `_Pager`, `_EmptyState`,
   `_FormErrors`, `_ImagePicker`, `ScopeFields`.
6. Table change? Add a **new** numbered script (`007_...sql`) – the migrator runs it once.

---

## 5. Security built in

| Threat | Protection |
|---|---|
| Brute-force login | Account lockout (5 tries → 15 min) + login rate limit (10 / 5 min per IP) + equal-time response for unknown users |
| DoS / flooding | Per-user/per-IP token-bucket rate limiting, request size limits (25 MB), form value limits, Kestrel header/body timeouts (slow-loris) |
| CSRF | Antiforgery token required on every POST (global filter) |
| XSS | Razor encoding, strict Content-Security-Policy (`script-src 'self'`, no inline JS), only http(s) links rendered |
| SQL injection | 100% parameterised Dapper queries; sort orders are fixed strings in code |
| Data leaks between shops | Tenant filter on every query, incl. get-by-id (IDOR) |
| Malicious uploads | Size limit, file type checked from the file's bytes (JPG/PNG/WEBP), random names, path-traversal-safe delete |
| Clickjacking / sniffing | `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `nosniff`, referrer & permissions policies |
| Session abuse | HttpOnly/Secure/SameSite cookies, sliding expiry, re-validation every 5 min (deactivated users are signed out) |
| Open redirect | Only local return URLs are followed |
| Passwords | PBKDF2 (ASP.NET Identity hasher), auto-upgrade of old hashes |
| Errors | Global middleware: logs once with a trace id, shows one friendly message (or JSON for fetch calls), never leaks SQL/stack traces |
| CSV injection | Export cells starting with `= + - @` are neutralised |
| Tracking links | 128-bit random tokens, read-only page with no internal figures, resettable, `no-store` + `noindex` + no referrer |

Settings live under `Security` in `appsettings.json`.

---

## 6. Deploying (IIS on Windows)

1. `dotnet publish src/Ordex.Web -c Release -o publish`
2. Install the **.NET 10 Hosting Bundle** on the server, then create an IIS site pointing at `publish`.
3. Put the production connection string in `appsettings.Production.json` or an environment
   variable `ConnectionStrings__Default`. Change `Seed:SuperAdminPassword`.
4. Give the app pool identity **write access** to `wwwroot/uploads` (product images) and
   `App_Data` (login cookie keys).
5. Use HTTPS. HSTS and HTTPS redirection switch on automatically outside Development.

`web.config` already sets the 25 MB upload limit and removes server headers.
Set `App:PublicBaseUrl` if the site runs behind a proxy, so customer links show the public address.

---

## 7. Mobile UX notes
* Bottom navigation with a central **＋ New order** button; desktop gets a sidebar.
* Order cards instead of wide tables; tap the mobile number to call.
* New order: customer search by mobile or name, several products per customer in one save.
* Photos are resized on the phone before upload (saves data).
* 16 px inputs (no iOS zoom), numeric keypads for money, sticky Save bar, light/dark theme.

---

## 8. Git

The repository is <https://github.com/aliakramtushar/Ordex>.
Ignored on purpose (see `.gitignore`): `appsettings*.json` (except the example),
`wwwroot/uploads/` (customer photos), `App_Data/` (cookie keys), `bin/`, `obj/`, `.vs/`, `*.user`.

Daily flow:
```bash
git pull
# …work…
git add -A
git commit -m "Short description of the change"
git push
```
