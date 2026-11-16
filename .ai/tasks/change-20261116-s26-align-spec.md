# Task Record: S26 Backend Alignment with Treasury & Dynamic Accounting Spec

- **Loại:** change
- **Ngày tạo:** 2026-11-16
- **Người thực hiện:** loipham
- **Branch:** `loipham/s26-align-spec`
- **Trạng thái:** Done

## Mục tiêu
Đồng bộ Backend (`ERPm-BE`) S26.1–S26.3 với đặc tả `TREASURY_AND_DYNAMIC_ACCOUNTING_ERP_SPEC.md`:
- Bổ sung `AccountsController` & `IAccountService` / `AccountService` (COA Tree, filter `?isPostable`, tạo/sửa tài khoản có chặn sửa khi đã phát sinh bút toán `error.accounting.account_in_use`).
- Bổ sung phân trang `PagedResult<T>` và filter (`page`, `pageSize`, `q`, `from`, `to`) cho `GET receipt-vouchers` và `GET payment-vouchers`.
- Nhúng `JournalEntrySummaryDto` (`Lines` + `AccountCode`/`AccountName`) và `BranchName`, `FundOrBankName`, `PartnerName` vào `ReceiptVoucherDto` / `PaymentVoucherDto`.
- Bổ sung `ReceiptType.EmployeeRefund`, `PaymentType.EmployeeAdvance`, `PaymentType.TaxPayment` và seed posting profiles tương ứng.
- Bổ sung `InternalTransferDirection.BankToBank` cho `TreasuryFundService.TransferInternalAsync`.
- Bổ sung tùy chọn `AutoAllocateFifo` cho `CreateReceiptVoucherRequest`.
- Chuẩn hóa HTTP `400` cho `error.accounting.unbalanced_journal` và thêm route alias `/api/v1/treasury/*`.

## Verification
- `dotnet build ERPm-BE/ERPm.sln`: **PASS** (`0 Warning(s)`, `0 Error(s)`).
- `dotnet run --project ERPm-BE/tests/ERPm.Verification --no-build` (isolated DB `erpm_verify_62a030da7d0041a4af7cbc4184a447ba` on Neon): **PASS** (`VERIFIED 560 checks`, exit code `0`).
