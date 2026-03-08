# API Test Plan — Betsson Online Wallet

## Overview

These are integration tests that spin up the real ASP.NET Core web application in memory
(`WebApplicationFactory`) and talk to it over HTTP. No mocks are used — the full middleware
pipeline, validators, controllers, service layer, and in-memory database are all active.

**Framework:** xUnit + `Microsoft.AspNetCore.Mvc.Testing`
**Base URL:** `/onlinewallet`
**Test file:** `OnlineWalletApiTests.cs`

---

## Endpoints Under Test

| Method | Route                      | Description             |
|--------|----------------------------|-------------------------|
| GET    | `/onlinewallet/balance`    | Retrieve current balance |
| POST   | `/onlinewallet/deposit`    | Deposit funds           |
| POST   | `/onlinewallet/withdraw`   | Withdraw funds          |

---

## Test Cases

### GET /onlinewallet/balance

| # | Test Name | Description | Expected Result |
|---|-----------|-------------|-----------------|
| 1 | `GetBalance_BrandNewWallet_ReturnsZero` | Call balance on a fresh wallet with no transactions | HTTP 200, `amount = 0` |
| 2 | `GetBalance_AfterDeposit_ReflectsDepositedAmount` | Deposit $75 then check balance | HTTP 200, `amount >= 75` |
| 3 | `GetBalance_ResponseIsJson_WithAmountProperty` | Verify response content type and shape | `Content-Type: application/json`, body contains `amount` field |

---

### POST /onlinewallet/deposit

| # | Test Name | Description | Expected Result |
|---|-----------|-------------|-----------------|
| 4 | `Deposit_ValidAmount_ReturnsOkWithNewBalance` | Deposit $50 to an existing wallet | HTTP 200, balance increases by exactly $50 |
| 5 | `Deposit_ZeroAmount_ReturnsOkButBalanceUnchanged` | Deposit $0 (boundary value) | HTTP 200, balance unchanged |
| 6 | `Deposit_NegativeAmount_ReturnsBadRequest` | Deposit -$10 (invalid input) | HTTP 400 — validator rejects negative amounts |
| 7 | `Deposit_SmallDecimalAmount_HandlesPenniesCorrectly` | Deposit $0.01 (smallest valid amount) | HTTP 200, balance increases by exactly $0.01 |
| 8 | `Deposit_LargeAmount_HandlesItWithoutProblems` | Deposit $1,000,000 (large amount) | HTTP 200, balance increases by exactly $1,000,000 |
| 9 | `Deposit_NullBody_ReturnsUnsupportedMediaType` | POST with null/missing body (no Content-Type) | HTTP 415 — server rejects request without content type |

---

### POST /onlinewallet/withdraw

| # | Test Name | Description | Expected Result |
|---|-----------|-------------|-----------------|
| 10 | `Withdraw_EnoughMoney_ReturnsOkWithReducedBalance` | Deposit $200 then withdraw $30 | HTTP 200, balance reduced by exactly $30 |
| 11 | `Withdraw_MoreThanBalance_ReturnsBadRequest` | Attempt to withdraw current balance + $999,999 | HTTP 400 — insufficient funds |
| 12 | `Withdraw_MoreThanBalance_ResponseContainsInsufficientFundsMessage` | Overdraft attempt error message | HTTP 400, response body contains "insufficient funds" |
| 13 | `Withdraw_NegativeAmount_ReturnsBadRequest` | Withdraw -$5 (invalid input) | HTTP 400 — validator rejects negative amounts |
| 14 | `Withdraw_ZeroAmount_ReturnsOkButBalanceUnchanged` | Withdraw $0 (boundary value) | HTTP 200, balance unchanged |
| 15 | `Withdraw_ExactlyAllTheMoney_ReturnsZeroBalance` | Deposit $100 then withdraw the full balance | HTTP 200, `amount = 0` |
| 16 | `Withdraw_NullBody_ReturnsUnsupportedMediaType` | POST with null/missing body (no Content-Type) | HTTP 415 — server rejects request without content type |

---

### Multi-step / Scenario Tests

| # | Test Name | Description | Expected Result |
|---|-----------|-------------|-----------------|
| 17 | `DepositThenWithdraw_MultipleRoundTrips_BalanceStaysConsistent` | Deposit $100, withdraw $25, deposit $50, withdraw $10 | Final balance = start + $115 |
| 18 | `Withdraw_AfterFailedWithdrawal_BalanceIsUnchanged` | Attempt an overdraft, then verify balance is unchanged | Balance after failed attempt equals balance before |

---

## Coverage Summary

| Area | Cases Covered |
|------|--------------|
| Happy path (deposit & withdraw) | 4, 7, 8, 10, 15 |
| Boundary values ($0, $0.01, exact balance) | 5, 7, 14, 15 |
| Negative / invalid input | 6, 13 |
| Missing body / content type | 9, 16 |
| Overdraft / insufficient funds | 11, 12 |
| Response format / content type | 3 |
| State consistency / multi-step | 1, 2, 17, 18 |

---

## Notes

- The in-memory database is **shared** across tests within the same class. Tests that need a
  clean database (e.g. `GetBalance_BrandNewWallet_ReturnsZero`) are placed in a separate class
  (`OnlineWalletFreshWalletTests`) to get their own `WebApplicationFactory` instance. Other tests
  read the current balance first and assert relative changes (e.g. `balance + 50`).
- Validation is enforced by FluentValidation configured in `Startup.cs`. Negative amounts are
  rejected at the API layer before reaching the service.
- Insufficient funds errors are thrown by `OnlineWalletService` and mapped to HTTP 400 by the
  global exception handler in `Startup.cs`.
- Sending a POST with null body (no `Content-Type` header) returns HTTP 415 Unsupported Media
  Type, not 400 Bad Request. An empty JSON object `{}` defaults `Amount` to `0` and passes
  validation, so it is not treated as an error.
