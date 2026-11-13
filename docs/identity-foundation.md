<!-- Role: Documents local identity configuration, assumed permission policy and remaining production integration requirements. -->
# Identity foundation

## Configuration and setup
The API uses ASP.NET Core Identity with SQL Server persistence separate from the selectable ERP repository. Set `NBJEWEL_IDENTITY_CONNECTION` through backend environment configuration or a secrets manager, never frontend configuration. The original `NBJEWEL_SQL_CONNECTION` continues to select ERP data when the SQL repository is enabled. A sample ERP repository does not imply sample authentication or an automatic login bypass.

An unconfigured identity database leaves protected operations unavailable. It must not fall back to a privileged in-memory account. Public try-on remains accessible. An unavailable configured SQL server is an operational error, not permission to skip authorization.

Schema preparation and account setup require separate approval under repository rules. No setup or migration is run automatically on API startup. Review the generated schema artifact before applying it to an explicitly designated database. This implementation session does not validate SQL persistence against a running SQL server.

The explicit development bootstrap is `dotnet run --project backend/NbJewel.Api -- --bootstrap-identity`, with Development environment and `NBJEWEL_BOOTSTRAP_EMAIL` / `NBJEWEL_BOOTSTRAP_PASSWORD` supplied securely. It creates an all-store ERP development account with explicit permissions only when the schema exists. It refuses to elevate an existing account. This is an assumed demo administration mechanism, not a verified NB Jewel owner role. Do not run it against production or commit a password.

## Authentication contract
| Operation | Contract |
| --- | --- |
| CSRF | `GET /api/auth/csrf` returns `{ token }` and an antiforgery cookie |
| Session | `GET /api/auth/me` returns identity, account type, explicit permissions, store IDs and all-store flag, or 401 |
| Sign in | `POST /api/auth/login` with email/password; returns session identity |
| Customer registration | `POST /api/auth/register` with email/password; cannot assign ERP account type, roles or grants |
| Sign out | `POST /api/auth/logout`; clears authentication cookie |
| Verify email | `POST /api/auth/confirm-email` with userId/token; CSRF protected |
| Resend | `POST /api/auth/resend-verification` with email; generic response and throttling |

Mutations require the matching `X-CSRF-TOKEN`. Authentication uses HttpOnly cookies; the UI stores no bearer token or password in browser storage. Production cookies require HTTPS. SameSite Strict assumes same-site browser/API deployment; a different cross-site deployment needs explicit review rather than merely enabling credentials or broadening CORS. Login and registration are throttled; account lockout is additional protection.

## Assumed permission matrix
Permissions are capabilities, not business roles. No actual NB Jewel role hierarchy has been established.

| Capability | Intended permission |
| --- | --- |
| Read catalog | `items.read` |
| Create/update catalog | `items.write` |
| Read selling prices | `prices.read` |
| Read stock / enter stock | `stock.read` / `stock.write` |
| Read status / change status | `status.read` / `status.write` |
| Read ERP reference data | `reference.read` |
| Invoke legacy ERP assistant | `chat.read`, plus every read permission exposed by its tools |
| Manage uploaded jewelry | `jewelry.manage` |

Every ERP permission requires an ERP account. Customers cannot acquire these permissions through registration. Permission grants are resolved server-side, not accepted from the model or browser. The adapter contract permits later integration with verified real ERP identity and grants.

Existing domain responses span capabilities, so reads require the relevant combined permissions. Scoped catalog queries filter visibility before pagination/counts, and stock rows and store lists are scoped before they are returned or aggregated. Tools independently resolve fresh grants on every invocation. Global item/status changes and owner image management still require all-store authority; stock entry also checks its destination store. Unknown controllers fail closed unless explicitly registered with their own access boundary.

## Remaining work
Both assistants, owned conversations, approved knowledge retrieval and usage/audit records are implemented. Production email verification is enabled by default; Development may opt in with `AiSecurity__RequireEmailVerification=true`. Registration returns 202 without a login cookie while verification is pending. SMTP_HOST, SMTP_FROM and HTTPS APP_PUBLIC_ORIGIN configure delivery; SMTP_PORT defaults to 587 with TLS, and SMTP_USERNAME/SMTP_PASSWORD are optional backend-only credentials. No sender configuration means required registration returns 503 before account creation. Links carry confirmation data in a fragment, the frontend clears that fragment, and confirmation requires an explicit click and CSRF-protected POST. Unknown-email login performs dummy password verification and returns the same generic error as other failed logins. This reduces one timing distinction without claiming constant network response times.

Real ERP permission mapping, production ERP account provisioning, password recovery, mail delivery verification, TLS/proxy deployment and real SQL tests still require the actual deployment environment. No customer order service is invented. See the deployment checklist and key-protection guide; no real email or SQL setup was performed in this session.
