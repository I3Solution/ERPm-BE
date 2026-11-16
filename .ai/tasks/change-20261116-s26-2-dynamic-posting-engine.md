# [change] S26.2 Động cơ Định khoản Động (DynamicPostingEngine) & Ma trận Cấu hình Tài khoản

> **Loại:** change
> **Ngày tạo:** 2026-11-16 · **Người làm:** Phạm Hữu Lợi (Dev chính Backend) · **Người yêu cầu:** Phúc (Lead / Core)
> **Trạng thái:** Done
> **Progress aggregation:** v1
> **Liên quan:** Trello card `[S26.2][BE] Động cơ Định khoản Động (DynamicPostingEngine) & Ma trận Cấu hình Tài khoản` (`https://trello.com/c/WBSKAWis/111`)
> **Phạm vi:** `ERPm-BE` (`Domain`, `Application`, `Infrastructure`, `API`, `Migrations`, `Tests`)

## 1. Mô tả
Thay thế hoàn toàn việc gán cứng mã tài khoản kế toán (`"511"`, `"111"`, `"112"`, `"131"`, `"632"`, `"156"`) trong `SalesPostingService.cs` bằng `DynamicPostingEngine`, hỗ trợ:
- Phân giải rule định khoản theo `Module`, `TransactionType`, `BranchId` (ưu tiên chi nhánh, fallback `BranchId = null`), `DimensionKey`.
- Lấy tài khoản tiền mặt từ `CashFund.AccountId`, tiền gửi từ `BankAccount.AccountId`, phải thu từ `Partner.ReceivableAccountId`.
- Bóc tách thuế GTGT đầu ra (`3331`) khi hóa đơn có `TaxRate` / `TaxAmount`.
- Kiểm soát bút toán kép (`TotalDebit == TotalCredit`, `IsPostable`, `TrackPartner`).
- API quản lý ma trận cấu hình định khoản (`PostingProfilesController`).

## 2. Phân tích
- Nhánh `loipham/s26-1-treasury-coa-schema` (`e0b520c`) chưa merge vào `master` (`ac508f3`), nên tạo nhánh `loipham/s26-2-dynamic-posting-engine` từ `loipham/s26-1-treasury-coa-schema`.
- Bổ sung `Partner.ReceivableAccountId` và `SalesInvoice.TaxRate`, `SalesInvoice.CashFundId`, `SalesInvoice.BankAccountId` kèm migration.
- Giữ mã TK thuế GTGT đầu ra `3331` theo `EssentialDataSeeder`.

## 3. Features / Milestones
- [x] Feature 1 — Mở rộng `Partner`, `SalesInvoice`, `SalesDtos`, `PermissionCatalog`, `ApplicationDbContext` và sinh migration `AddDynamicPostingPartnerAndInvoiceFields`.
- [x] Feature 2 — Xây dựng `IDynamicPostingEngine` / `DynamicPostingEngine` và `IPostingProfileService` / `PostingProfileService`.
- [x] Feature 3 — Refactor `SalesPostingService.cs` dùng `IDynamicPostingEngine`, hỗ trợ VAT (`3331`) và kiểm soát bút toán kép.
- [x] Feature 4 — Xây dựng `PostingProfilesController` (`GET`/`POST /api/v1/accounting/posting-profiles`) và bổ sung dictionary lỗi.
- [x] Feature 5 — Viết test kiểm thử (`DynamicPostingChecks.cs`) và chạy xác minh trên PostgreSQL thực tế (PASS toàn bộ 534 checks).

## 4. Cách làm
- Tạo nhánh `loipham/s26-2-dynamic-posting-engine` từ `loipham/s26-1-treasury-coa-schema` (`e0b520c`).
- Bổ sung `Partner.ReceivableAccountId`, `SalesInvoice.TaxRate`, `SalesInvoice.CashFundId`, `SalesInvoice.BankAccountId`, `PermissionCatalog` (`acc:posting-profile`), và migration `AddDynamicPostingPartnerAndInvoiceFields`.
- Triển khai `DynamicPostingEngine`, `PostingProfileService`, `PostingProfilesController`, cập nhật `SalesPostingService.cs`, `DependencyInjection.cs`, `system_dictionary.json`, và `DynamicPostingChecks.cs`.
- Cập nhật assertion ở `Program.cs` dòng 128 từ `"111"` (tài khoản mẹ `IsPostable = false`) sang `"1111"` (tài khoản chi tiết trong `AccountPostingProfile`).

## 5. Kiểm tra
- `dotnet build ERPm.sln --no-restore` → **PASS** (`0 Warning(s), 0 Error(s)`).
- `dotnet ef migrations has-pending-model-changes --project src/ERPm.Infrastructure --startup-project src/ERPm.API --no-build` → **PASS** (`No changes have been made to the model since the last migration.`).
- `dotnet run --project tests/ERPm.Verification/ERPm.Verification.csproj` (Neon PostgreSQL) → **PASS** (`VERIFIED 534 checks`).

## 6. Việc còn lại
- Người dùng bổ sung tài liệu spec/plan và mở PR sau khi S26.1 merge vào `master`.

## 7. Evidence và cập nhật
- Nhánh hiện tại: `loipham/s26-2-dynamic-posting-engine` (từ `e0b520c`).
- Ngày cập nhật cuối: 2026-11-16
