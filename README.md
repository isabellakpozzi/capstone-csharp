# Digital Library Management System

A microservices-based backend API for digitizing library operations — self-service book reservations, catalog browsing, checkout/return processing, and waitlist management — built with ASP.NET Core, PostgreSQL, and JWT authentication, deployed on AWS Elastic Beanstalk.

## Architecture

Three independent ASP.NET Core services, each owning its own database:

| Service | Responsibility | Local Port |
|---|---|---|
| **UserService** | Registration, authentication (JWT), profile management | 5001 |
| **CatalogService** | Book inventory, search/filter, availability tracking | 5002 |
| **ReservationService** | Reservation lifecycle, checkout/return, borrowing history, waitlist | 5003 |

Services communicate over HTTP/REST. ReservationService orchestrates the core workflow — validating users via UserService and checking/updating book availability via CatalogService.

```
┌──────────────┐        ┌─────────────────────┐        ┌────────────────┐
│ UserService  │◄──────►│ ReservationService   │◄──────►│ CatalogService │
│ (auth, users)│        │ (reservations, waitlist) │     │ (book catalog) │
└──────────────┘        └─────────────────────┘        └────────────────┘
```

## Tech Stack

- **ASP.NET Core 9.0** / C# 12
- **Entity Framework Core 9.0** — PostgreSQL (production) / In-Memory (development)
- **JWT Bearer authentication** — `Microsoft.AspNetCore.Authentication.JwtBearer`
- **BCrypt.Net-Next** — password hashing
- **FluentValidation** — request validation
- **xUnit + Moq + FluentAssertions** — testing (80%+ coverage across all services)
- **Swashbuckle** — Swagger/OpenAPI documentation
- **AWS Elastic Beanstalk + RDS PostgreSQL** — production deployment

## Business Rules

- Maximum **5 active reservations** per user
- Reservations expire after **7 days** if not picked up
- Checkout period: **14 days**
- Late fees: **$1.00/day**
- Waitlist claim window: **48 hours**, enforced via a background job that runs hourly

## Key Features

**Reservation lifecycle**: reserve → checkout → return, with automatic availability tracking and late fee calculation.

**Waitlist system**: when a book with an active waitlist is returned, the copy is automatically offered to the longest-waiting *eligible* patron (under their 5-reservation limit at claim time) rather than becoming generally available. Ineligible patrons are skipped and their entries expire; unclaimed offers cascade to the next person after 48 hours via a background `BackgroundService`.

**Role-based access**: Patrons can browse, reserve, and manage their own waitlist entries. Librarians can additionally process checkouts and returns.

## Project Structure

```
capstone-week-3/
├── UserService/              # Auth, users, JWT
├── CatalogService/            # Book catalog, search/filter
├── ReservationService/        # Reservations, waitlist, background job
├── UserService.Tests/
├── CatalogService.Tests/
├── ReservationService.Tests/
├── Procfile                   # Defines the 3 processes for deployment
├── .platform/nginx/...        # nginx routing config (deployment)
└── global.json                # Pins the .NET SDK version
```

## Running Locally

Each service uses an in-memory database in Development — no local Postgres required.

```bash
cd UserService && dotnet run        # http://localhost:5001/swagger
cd CatalogService && dotnet run     # http://localhost:5002/swagger
cd ReservationService && dotnet run # http://localhost:5003/swagger
```

CatalogService seeds sample books on startup in Development mode.

### Quick smoke test

```bash
# Register
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#","firstName":"Test","lastName":"User","phoneNumber":"+1-555-0123"}'

# Login
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#"}'

# Browse catalog
curl http://localhost:5002/api/catalog/books

# Reserve a book (use token from login, bookId from catalog)
curl -X POST http://localhost:5003/api/reservations \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"bookId":"<BOOK_ID>"}'
```

## Running Tests

```bash
cd UserService.Tests && dotnet test
cd CatalogService.Tests && dotnet test
cd ReservationService.Tests && dotnet test
```

Coverage reports (with boilerplate exclusions for `Program.cs` and DTOs):

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

All three services exceed 80% line coverage across services, controllers, HTTP clients, middleware, filters, and validators.

## API Overview

| Method | Endpoint | Access |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| GET | `/api/users/profile` | Authenticated |
| GET | `/api/catalog/books` | Public |
| GET | `/api/catalog/books/{bookId}` | Public |
| POST | `/api/reservations` | Authenticated |
| GET | `/api/reservations` | Authenticated |
| POST | `/api/reservations/{id}/checkout` | Librarian |
| POST | `/api/reservations/{id}/return` | Librarian |
| GET | `/api/reservations/history` | Authenticated |
| POST | `/api/reservations/waitlist` | Authenticated |
| GET | `/api/reservations/waitlist` | Authenticated |
| DELETE | `/api/reservations/waitlist/{id}` | Authenticated |

Full request/response contracts are documented via Swagger UI on each service (`/swagger`).

## Deployment

Deployed as a **single AWS Elastic Beanstalk environment** running all three services as separate processes (via `Procfile`) on one EC2 instance, with nginx routing `/catalog/*` and `/reservations/*` to their respective processes. All three connect to separate databases on one shared RDS PostgreSQL instance.

```
$EB/health               → UserService health + migration status
$EB/catalog/health       → CatalogService health + migration status
$EB/reservations/health  → ReservationService health + migration status
```

Migrations are applied automatically on startup via `Database.Migrate()`, which also creates the `catalogservicedb` and `reservationservicedb` databases on first boot.

### Environment variables

| Variable | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__UserDb` | UserService's Postgres connection |
| `ConnectionStrings__CatalogDb` | CatalogService's Postgres connection |
| `ConnectionStrings__ReservationDb` | ReservationService's Postgres connection |
| `Jwt__Secret` / `Jwt__Issuer` / `Jwt__Audience` | Shared across all three for token validation |
| `ServiceUrls__*` | Inter-service URLs (`localhost` ports, since all processes share one instance) |

## Notable Design Decisions

- **Cached book title/author on Reservation and Waitlist records** — avoids a live Catalog Service call on every history/active-reservations read.
- **Graceful degradation** — if ReservationService is unreachable, UserService's profile endpoint still returns successfully with zeroed stats rather than failing the whole request.
- **Waitlist eligibility is checked at claim time, not join time** — a patron under the limit when they join can still be skipped later if they've since hit it.
- **`IDesignTimeDbContextFactory`** implementations decouple EF Core migration generation from runtime environment detection, avoiding tooling failures when `Program.cs` branches on `IsDevelopment()`.

## Endpoint Screenshots
**Hitting the /health endpoint**
<img width="714" height="169" alt="image" src="https://github.com/user-attachments/assets/d4fefe92-4438-44c4-9160-c0b79bdb3367" />

**Listing the seeded books**
<img width="830" height="990" alt="image" src="https://github.com/user-attachments/assets/64cba7d1-7797-4211-ba0b-b03eebfc3f18" />




