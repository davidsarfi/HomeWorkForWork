# Unit Test Plan — Betsson Online Wallet Service

## Overview

These are isolated unit tests for `OnlineWalletService`. The repository layer is replaced by
a `Moq` strict mock so every test controls exactly what the database "returns" and can verify
exactly what gets written back — without touching a real database.

**Framework:** xUnit + Moq
**Class under test:** `Betsson.OnlineWallets.Services.OnlineWalletService`
**Test file:** `Services/OnlineWalletServiceTests.cs`

---

## What Is Being Tested

`OnlineWalletService` exposes three methods:

| Method | Behaviour |
|--------|-----------|
| `GetBalanceAsync()` | Returns `BalanceBefore + Amount` from the most recent wallet entry, or `0` if no entries exist |
| `DepositFundsAsync(Deposit)` | Reads current balance, inserts a new entry with the deposit amount, returns new balance |
| `WithdrawFundsAsync(Withdrawal)` | Reads current balance, throws `InsufficientBalanceException` if amount > balance, otherwise inserts a negative entry and returns new balance |

---

## Test Cases

### GetBalanceAsync

| # | Test Name | Arrange | Expected Result |
|---|-----------|---------|-----------------|
| 1 | `GetBalance_NewWalletWithNoHistory_ReturnsZero` | Repository returns `null` (no entries) | `Balance.Amount = 0`, repository queried once |
| 2 | `GetBalance_WalletWithPreviousTransactions_ReturnsSumOfLastEntryFields` | Last entry has `BalanceBefore = 125.50`, `Amount = -25.25` | `Balance.Amount = 100.25`, repository queried once |

---

### DepositFundsAsync

| # | Test Name | Arrange | Expected Result |
|---|-----------|---------|-----------------|
| 3 | `Deposit_WalletHasExistingBalance_AddsMoneyAndSavesTransaction` | Existing balance of $100 (80+20), deposit $40 | Returns $140; saved entry has `Amount = 40`, `BalanceBefore = 100`, timestamp within test window |
| 4 | `Deposit_EmptyWalletFirstEverDeposit_StartsFromZeroBalance` | No history, deposit $25 | Returns $25; saved entry has `BalanceBefore = 0`, `Amount = 25` |
| 5 | `Deposit_ZeroAmount_ProcessesNormallyAndBalanceStaysTheSame` | Existing balance of $50 (30+20), deposit $0 | Returns $50; entry still saved with `Amount = 0`, `BalanceBefore = 50` |
| 6 | `Deposit_TinyPennyAmount_HandlesSmallDecimalsCorrectly` | Existing balance of $99.99 (50.50+49.49), deposit $0.01 | Returns exactly $100.00; saved entry has `Amount = 0.01`, `BalanceBefore = 99.99` |
| 7 | `Deposit_MaxDecimalAmount_OnZeroBalance_ReturnsMaxValue` | No history, deposit `decimal.MaxValue` | Returns `decimal.MaxValue`; 0 + Max does not overflow |
| 8 | `Deposit_MaxDecimalAmount_OnNonZeroBalance_ThrowsOverflowException` | Balance = $1 (1+0), deposit `decimal.MaxValue` | Throws `OverflowException`; `1 + decimal.MaxValue` overflows |

---

### WithdrawFundsAsync

| # | Test Name | Arrange | Expected Result |
|---|-----------|---------|-----------------|
| 9 | `Withdraw_EnoughMoneyInWallet_SubtractsAndSavesNegativeAmount` | Balance $150 (120+30), withdraw $20 | Returns $130; saved entry has `Amount = -20`, `BalanceBefore = 150`; insert called once |
| 10 | `Withdraw_MoreThanWalletHas_ThrowsErrorAndSavesNothing` | Balance $90 (60+30), withdraw $100 | Throws `InsufficientBalanceException`; insert never called |
| 11 | `Withdraw_ExactlyAllTheMoney_AllowsItAndReturnsZeroBalance` | Balance exactly $40 (10+30), withdraw $40 | Returns $0; insert called once |
| 12 | `Withdraw_WalletIsEmptyNoHistory_ThrowsErrorEvenForSmallAmount` | No history (balance = $0), withdraw $1 | Throws `InsufficientBalanceException`; insert never called |
| 13 | `Withdraw_ZeroAmount_ProcessesNormallyAndBalanceStaysTheSame` | Balance $50 (30+20), withdraw $0 | Returns $50; entry saved with `Amount = 0`, `BalanceBefore = 50` |
| 14 | `Withdraw_WithSufficientFunds_SavesTransactionWithCorrectTimestamp` | Balance $100 (70+30), withdraw $25 | Returns $75; saved entry timestamp falls within the test execution window |

---

## Coverage Summary

| Area | Cases Covered |
|------|--------------|
| Balance calculation — empty wallet | 1, 10 |
| Balance calculation — existing history | 2 |
| Deposit — normal flow | 3 |
| Deposit — first ever (zero starting balance) | 4 |
| Deposit — boundary: $0 amount | 5 |
| Deposit — boundary: penny precision | 6 |
| Deposit — overflow guard (zero balance) | 7 |
| Deposit — overflow guard (non-zero balance) | 8 |
| Withdrawal — normal flow | 9 |
| Withdrawal — exact balance (boundary) | 11 |
| Withdrawal — boundary: $0 amount | 13 |
| Withdrawal — overdraft (balance insufficient) | 10 |
| Withdrawal — overdraft on empty wallet | 12 |
| Timestamp correctness | 3, 14 |
| Repository call verification (Times.Once / Times.Never) | 1, 2, 3, 9, 10, 11, 12, 14 |

---

## Notes

- All tests use `MockBehavior.Strict` — any unexpected repository call will fail the test
  immediately, making it obvious when the service calls the database more times than expected.
- Negative withdrawal amounts (e.g. `-$5`) are **not** tested here because validation is handled
  by the API layer (FluentValidation). The service itself does not re-validate input sign.
- The `InsufficientBalanceException` is expected to propagate up to the controller, where it is
  caught by the global exception handler and converted to an HTTP 400 response.
- Timestamps are verified using `Assert.InRange(eventTime, beforeCall, afterCall)` to avoid
  brittle exact-time comparisons.
