# [change] S26.3 Dịch vụ Dòng tiền: Sổ quỹ, Phiếu Thu/Chi & Động cơ Cấn trừ Hóa đơn (Allocation)

> **Loại:** change
> **Ngày tạo:** 2026-11-16 · **Người làm:** Phạm Hữu Lợi (Dev chính Backend) · **Người yêu cầu:** Phúc (Lead / Core)
> **Trạng thái:** Done
> **Progress aggregation:** v1
> **Liên quan:** Trello card `[S26.3][BE] Dịch vụ Dòng tiền: Sổ quỹ, Phiếu Thu/Chi & Động cơ Cấn trừ Hóa đơn (Allocation)` (`https://trello.com/c/DgV7H4DC`)
> **Phạm vi:** `ERPm-BE` (`Domain`, `Application`, `Infrastructure`, `API`, `Tests`)

## 1. Mô tả
Xây dựng toàn bộ tầng nghiệp vụ xử lý dòng tiền (Treasury) trong Step 26 — S26.3:
- Quản lý Sổ quỹ tiền mặt (`CashFund`) và Tài khoản ngân hàng (`BankAccount`), truy vấn số dư tức thời theo chi nhánh, và chuyển tiền nội bộ (`TransferInternalAsync`).
- Lập và ghi sổ Phiếu Thu tiền (`ReceiptVoucherService`) kèm Động cơ Cấn trừ Hóa đơn bán hàng (`ReceiptVoucherAllocation`), cập nhật `SalesInvoice.RemainingDebt` / `PaidAmount`, `Partner.DebtBalance`, `CurrentBalance`, và sinh `JournalEntry` qua `IDynamicPostingEngine`.
- Lập và ghi sổ Phiếu Chi tiền (`PaymentVoucherService`), kiểm soát không cho chi âm quỹ (`error.treasury.insufficient_fund_balance`), cập nhật `CurrentBalance` và sinh `JournalEntry`.
- Cơ chế Hủy / Đảo phiếu (`ReverseReceiptVoucherAsync` & `ReversePaymentVoucherAsync`) sinh `JournalEntry` đảo ngược (`ReversesJournalEntryId`), phục hồi nguyên trạng `RemainingDebt`, `PaidAmount`, `DebtBalance`, `CurrentBalance`.

## 2. Phân tích
- Tạo nhánh `loipham/s26-3-treasury-vouchers` từ `loipham/s26-2-dynamic-posting-engine`.
- Giữ nguyên `SalesInvoice` (không thêm cột `PaymentStatus`, dùng `RemainingDebt == 0` để xác định đã thanh toán đủ).
- Khi đảo phiếu thu (`ReverseReceiptVoucherAsync`), nếu số dư quỹ/ngân hàng hiện tại nhỏ hơn số tiền phiếu thu, chặn với mã lỗi `error.treasury.insufficient_fund_balance`.
- Dùng `PostingTransaction.RunAsync` + `LockAsync` (`pg_advisory_xact_lock`) để đảm bảo tính nguyên tử và chống race condition.

## 3. Features / Milestones
- [x] Feature 1 — Bổ sung `PermissionCatalog` (`acc:treasury`, `acc:receipt-voucher`, `acc:payment-voucher`), `EssentialDataSeeder` (COA `141`, `334`, `711`, `811` + Posting Profiles bổ sung) và `system_dictionary.json` (`error.treasury.*`).
- [x] Feature 2 — Xây dựng `TreasuryDtos`, `ITreasuryFundService`, `TreasuryFundService` & `TreasuryFundsController` (CRUD CashFund/BankAccount, Balance query, `TransferInternalAsync`).
- [x] Feature 3 — Xây dựng `ReceiptVoucherDtos`, `IReceiptVoucherService`, `ReceiptVoucherService` & `ReceiptVouchersController` (Create + Invoice Allocation Engine + `ReverseReceiptVoucherAsync`).
- [x] Feature 4 — Xây dựng `PaymentVoucherDtos`, `IPaymentVoucherService`, `PaymentVoucherService` & `PaymentVouchersController` (Create + kiểm soát âm quỹ + `ReversePaymentVoucherAsync`).
- [x] Feature 5 — Đăng ký DI, viết `TreasuryChecks.cs` trong `ERPm.Verification` và kiểm tra build + pending model changes.
- [x] Feature 6 — Chạy `ERPm.Verification` với `ERPM_TEST_CONNECTION` PostgreSQL hợp lệ.

## 4. Cách làm
- Triển khai theo Clean Architecture: DTOs & Interfaces trong `ERPm.Application`, Services (`TreasuryPostingHelper`, `TreasuryFundService`, `ReceiptVoucherService`, `PaymentVoucherService`) trong `ERPm.Infrastructure/Services/Accounting/`, Controllers (`TreasuryFundsController`, `ReceiptVouchersController`, `PaymentVouchersController`) trong `ERPm.API/Controllers/ACC/`.

## 5. Kiểm tra
- `dotnet build ERPm.sln --no-restore` → **PASS** (`0 Warning(s), 0 Error(s)`).
- `dotnet ef migrations has-pending-model-changes --project src/ERPm.Infrastructure --startup-project src/ERPm.API --no-build` → **PASS** (`No changes have been made to the model since the last migration.`).
- `dotnet run --project tests/ERPm.Verification/ERPm.Verification.csproj` (Neon PostgreSQL) → **PASS** (`VERIFIED 553 checks`).

## 6. Việc còn lại
- Review & mở PR nhánh `loipham/s26-3-treasury-vouchers` sau khi S26.1 và S26.2 merge vào `master`.

## 7. Evidence và cập nhật
- Nhánh hiện tại: `loipham/s26-3-treasury-vouchers` (từ `loipham/s26-2-dynamic-posting-engine`).
- Ngày cập nhật cuối: 2026-11-16
