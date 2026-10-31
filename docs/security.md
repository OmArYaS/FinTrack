# FinTrack — Security & Identity

## Identity: Azure AD B2C (production)

- The **Angular UI** authenticates users with MSAL (`@azure/msal-angular`) against the B2C tenant’s
  **sign-up/sign-in user flow**; the SPA receives id/access tokens for the Web API audience.
- The **Web API** validates JWTs against the B2C authority/JWKS (`Auth:Mode=AzureAdB2C`, section
  `AzureAdB2C`), with `MapInboundClaims=false` so raw AAD claim types (`oid`, `roles`) are preserved.
- Roles are delivered in the `roles` app-role claim (Teller / Admin / Customer) mapped through B2C
  app roles; `ClaimsPrincipal.IsInRole` works out of the box because we set
  `TokenValidationParameters.RoleClaimType = "roles"`.

## Authorization model

| Caller | May do |
| --- | --- |
| Customer | List/review own accounts and ledger, transfer from **own** source account |
| Teller | Operation for any account on behalf of customers, full staff overview |
| Admin | Everything Teller can + directory/provisioning candidates |

Enforcement is layered:

1. Authentication → JWT bearer (validated).
2. Endpoint policies → `[Authorize(Policy = "Staff")]` for back-office actions.
3. Resource-based policy → `CustomerOwnerOrStaff` with a custom `AuthorizationHandler` — the handler
   compares the route resource (customer/account id) with the caller’s `oid` claim, or passes staff.
4. Service-level defense in depth → `TransferService` re-checks ownership (defense against route
   misuse) and staff delegation.

## Sealing the dev identity stub away

The repo runs out-of-the-box with `Auth:Mode=Development` — a local HS256 issuer (`JWTS`) so the whole
stack demos offline. Safeguards:

- `/api/auth/token` is flagged `[ApiExplorerSettings(IgnoreApi = true)]` and returns `404` whenever
  `Auth:Mode=AzureAdB2C`.
- The dev signing key is only for local secrets; production loadouts download none of it (outline in
  `pipeline/cd.yml` variables group pulls secrets from Key Vault).

## Operational hardening checklist

- [x] HTTPS everywhere (`UseHttpsRedirection`, App Service TLS 1.2+ enforcement)
- [x] Authorization tested end-to-end (contract tests assert 401/403 flows)
- [x] ProblemDetails errors; internal exception detail hidden (`INTERNAL_ERROR`)
- [x] Invariants in the database as defense in depth (CHECK constraints, THROW codes)
- [ ] WAF/Front Door in front of App Service (documented, infra at tenant discretion)
- [ ] SIEM forwarding of the audit log via Application Insights (documented pattern)
