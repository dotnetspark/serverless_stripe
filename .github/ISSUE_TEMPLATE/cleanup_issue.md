---
name: Cleanup
description: Use this template for repository cleanup tasks (ignore files, remove logs, tidy CI)
---

- What was changed:
  - Removed checked-in `.func_host.log`.
  - Cleaned root `.gitignore` and added `*.log` and `.func_host.log`.
  - Stabilized unit tests for Stripe webhook signature verification by injecting a `ConstructEventDelegate` in tests that would otherwise rely on stripe.net deserialization.

- Files changed:
  - `azure-function/Shared/StripeWebhookService.cs`
  - `tests/unit_test/StripeWebhookFunctionTests.cs`
  - `.gitignore`

- Verification:
  - `dotnet test tests/unit_test/UnitTests.csproj --filter FullyQualifiedName~StripeWebhookFunctionTests` (7/7 passing locally)
