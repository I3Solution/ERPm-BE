# [db] S26.1 Schema Database PostgreSQL: Hệ thống Tài khoản (COA), Sổ quỹ, Ngân hàng & Phiếu Thu/Chi

> **Loại:** db
> **Ngày tạo:** 2026-11-16 · **Người làm:** Phạm Hữu Lợi (Dev chính Backend) · **Người yêu cầu:** Phúc (Lead / Core)
> **Trạng thái:** Doing
> **Progress aggregation:** v1
> **Liên quan:** Trello card `[S26.1][BE] Schema Database PostgreSQL: Hệ thống Tài khoản (COA), Sổ quỹ, Ngân hàng & Phiếu Thu/Chi` (`https://trello.com/c/l3r11ivY`)
> **Phạm vi:** `ERPm-BE` (`Domain`, `Application`, `Infrastructure`, `Migrations`, `Seeders`)

## 1. Mô tả
Mở rộng và chuẩn hóa schema PostgreSQL cho phân hệ Kế toán (ACC) và Quản trị Dòng tiền (Treasury) trong Step 26 — S26.1:
- Mở rộng `Account` (`tbl_ACC_Account`) với `Nature` (`AccountNature`: `DebitOnly = 1`, `CreditOnly = 2`, `Bilateral = 3`), `IsPostable`, `TrackPartner`, `TrackBranch`.
- Tạo bảng `tbl_ACC_AccountPostingProfile` (Ma trận Định khoản Động, `BranchId` nullable để hỗ trợ default toàn công ty và override theo chi nhánh, `UQ_PostingProfile_Key` với `AreNullsDistinct(false)`).
- Tạo bảng `tbl_ACC_CashFund` (Sổ quỹ tiền mặt) và `tbl_ACC_BankAccount` (Tài khoản ngân hàng, `UQ_BankAccount_Number`).
- Tạo bảng `tbl_ACC_ReceiptVoucher`, `tbl_ACC_ReceiptVoucherAllocation`, `tbl_ACC_PaymentVoucher` (dùng enum `TreasuryPaymentMethod`, `ReceiptType`, `PaymentType`, `VoucherStatus`).
- Cập nhật `EssentialDataSeeder` seed danh mục tài khoản TT200/133 mẫu (cha/con `111`/`1111`, `112`/`1121`, `131`, `156`, `331`, `3331`, `411`, `511`, `521`, `632`, `642`, `911`) và 12 `AccountPostingProfile` mặc định.

## 2. Phân tích
- Gom toàn bộ bảng Treasury và Accounting vào prefix `tbl_ACC_*` theo thống nhất với người dùng.
- `AccountPostingProfile.BranchId` để `int?` kèm `AreNullsDistinct(false)` và filter `is_deleted = false` để tránh trùng lặp cấu hình mặc định (`NULL`).
- Toàn bộ trường tiền tệ dùng `decimal(18, 4)`; enum lưu dạng `integer` kèm CHECK constraint.
- Áp dụng Global Query Filter (`!IsDeleted` và `AllowedBranchIds`) cho `CashFund`, `BankAccount`, `ReceiptVoucher`, `ReceiptVoucherAllocation`, `PaymentVoucher`.

## 3. Features / Milestones
- [x] Feature 1 — Mở rộng `Account`, bổ sung `AccountEnums` và các entity `AccountPostingProfile`, `CashFund`, `BankAccount`, `ReceiptVoucher`, `ReceiptVoucherAllocation`, `PaymentVoucher`.
- [x] Feature 2 — Cấu hình `IApplicationDbContext` và `ApplicationDbContext` (Fluent API, indexes, CHECK constraints, query filters).
- [x] Feature 3 — Cập nhật `EssentialDataSeeder` (TT200 COA + default posting profiles) và sinh migration `20261116013812_AddTreasuryAndDynamicAccountingSchema`.
- [ ] Feature 4 — Xác minh chạy `dotnet ef database update` trên PostgreSQL thực tế và hoàn tất CI/PR review.

## 4. Cách làm
- Tạo nhánh `loipham/s26-1-treasury-coa-schema` từ `ERPm-BE@ac508f3`.
- Cập nhật `ERPm.Domain/Entities/Accounting/`, `ERPm.Application/Common/Interfaces/IApplicationDbContext.cs`, `ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`, `ERPm.Infrastructure/Persistence/Seeders/EssentialDataSeeder.cs`.
- Sinh migration `20261116013812_AddTreasuryAndDynamicAccountingSchema`.

## 5. Kiểm tra
- `dotnet build ERPm.sln --no-restore` → **PASS** (`0 Warning(s), 0 Error(s)`).
- `dotnet ef migrations has-pending-model-changes --project src/ERPm.Infrastructure --startup-project src/ERPm.API --no-build` → **PASS** (`No changes have been made to the model since the last migration.`).
- `dotnet ef database update --project src/ERPm.Infrastructure --startup-project src/ERPm.API` → **BLOCKED** (`28P01: password authentication failed for user 'neondb_owner'` trên `ep-odd-heart-a1hfi89v-pooler.ap-southeast-1.aws.neon.tech:5432`).

## 6. Việc còn lại
- Chạy `dotnet ef database update` với connection string PostgreSQL hợp lệ (local hoặc Neon test branch).
- Review & merge PR nhánh `loipham/s26-1-treasury-coa-schema`.

## 7. Evidence và cập nhật
- Nhánh hiện tại: `loipham/s26-1-treasury-coa-schema` (từ `ac508f3`).
- Ngày cập nhật cuối: 2026-11-16
