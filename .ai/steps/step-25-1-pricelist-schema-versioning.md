# Step S25.1 — Schema Database & Entity Bảng giá (Type, IsTaxInclusive, Versioning)

Ngày: 2026-11-13 (cập nhật hoàn tất hồ sơ: 2026-11-14)

## Mục tiêu
Nâng cấp schema database và entity phân hệ Bảng giá (`PriceList`) theo chuẩn ERP:
- Bổ sung `Type` (`PriceListType`: `Sales = 1`, `Purchase = 2`) và `IsTaxInclusive` (`bool`) vào `PriceList`.
- Tạo entity `PriceListVersion` và bảng `tbl_SD_PriceListVersion` (`VersionCode`, `EffectiveFrom`, `EffectiveTo`, `Status`: `Draft = 1`, `Active = 2`, `Expired = 3`, `Archived = 4`).
- Chuyển `PriceListItem` liên kết qua `PriceListVersionId` thay cho `PriceListId`.
- Xây dựng migration PostgreSQL backfill an toàn dữ liệu cũ sang version `V1` kèm rollback `Down`.

## File tạo / chỉnh sửa
- Tạo mới:
  - `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.cs`
  - `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.Designer.cs`
  - `.ai/tasks/db-20261113-pricelist-type-tax-versioning.md`
  - `.ai/steps/step-25-1-pricelist-schema-versioning.md`
- Chỉnh sửa:
  - `src/ERPm.Domain/Entities/Sales/PriceList.cs`
  - `src/ERPm.Application/Common/Interfaces/IApplicationDbContext.cs`
  - `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`
  - `src/ERPm.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
  - `src/ERPm.Infrastructure/Services/PriceListService.cs`
  - `tests/ERPm.Verification/Program.cs`
  - `.ai/status.md`

## Thiết kế thực hiện
- Enum `PriceListType` và `PriceListVersionStatus` lưu dạng `integer` kèm CHECK constraint ở DB (`type IN (1, 2)`, `status BETWEEN 1 AND 4`, `effective_to IS NULL OR effective_to >= effective_from`).
- `PriceListVersion` kế thừa `BaseAuditableEntity`; unique `(price_list_id, version_code)` lọc `is_deleted = false`.
- Đặt tên tường minh cho FK (`fk_tbl_sd_price_list_item_price_list_version_id`) và unique index (`ix_tbl_sd_price_list_item_version_item_uom_min_quantity`, `AreNullsDistinct(false)`) của `PriceListItem` để tuân thủ giới hạn 63 ký tự của PostgreSQL.
- Cập nhật Global Query Filter cho `PriceListVersion` và `PriceListItem` trong `ApplicationDbContext`.
- Migration backfill tạo `V1` cho mọi `PriceList` hiện có (kể cả đã xoá mềm), giữ `Status = Expired` nếu `effective_to < NOW()`, ngược lại `Active`, chuyển 100% `PriceListItem` sang `V1` kèm 2 guard `RAISE EXCEPTION`.
- Giữ tương thích ngược cho `PriceListService` qua version `Active` hiện hành (`CurrentVersion`) và chỉ tính giá bán cho bảng giá `Type == PriceListType.Sales` thuộc version `Active` còn hiệu lực.

## Các lệnh đã chạy
- `dotnet build ERPm-BE/ERPm.sln --nologo -v q`
- `dotnet ef migrations has-pending-model-changes --project ERPm-BE/src/ERPm.Infrastructure --startup-project ERPm-BE/src/ERPm.Api --no-build`
- CI GitHub Actions run `#43394426337`: `dotnet build ERPm.sln`, `ERPm.Hrm.Verification`, `ERPm.Verification`
- Kiểm thử PostgreSQL 16 tạm (28 checks) và Neon test branch (migration + backfill)

## Verification
- `dotnet build ERPm.sln`: **PASS** (`0 Warning(s), 0 Error(s)` với .NET SDK `10.0.403`).
- `dotnet ef migrations has-pending-model-changes`: **PASS** (`No changes have been made to the model since the last migration.`).
- GitHub Actions run `#43394426337`: **PASS** toàn bộ build & verification suites.
- Neon test branch: tạo `V1` cho 2/2 `PriceList` (`Active = 2`, `Expired = 3`), backfill 4/4 `PriceListItem`, `missing_v1 = 0`, `orphan_items = 0` — **PASS**.
- PR [#43](https://github.com/I3Solution/ERPm-BE/pull/43) đã merge vào `ERPm-BE/master` tại commit `54217df0b04cf5fb4be654ade7d6b57ddb6b1d07`.

## Hướng đi cho step tiếp theo
- S25.2: Cập nhật `PricingEngine` (xử lý `DocumentDate` UTC, thuế `IsTaxInclusive` vs Tax Exclusive theo `Item.VatRate`) và bổ sung API quản lý/clone/bulk-adjustment cho `PriceListVersion`.
