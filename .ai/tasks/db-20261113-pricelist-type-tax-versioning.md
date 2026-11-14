# [db] S25.1 Schema Bảng giá: Type, IsTaxInclusive, Versioning

> **Loại:** db
> **Ngày tạo:** 2026-11-13 · **Người làm:** Lợi (Dev chính Backend) · **Người yêu cầu:** Phúc (Kiến trúc & Review)
> **Trạng thái:** Done
> **Progress aggregation:** v1
> **Liên quan:** Trello card `[S25.1][BE] Schema Database & Entity Bảng giá (Type, Thuế, Versioning)` (`https://trello.com/c/U54wkZHg`) · PR [#43](https://github.com/I3Solution/ERPm-BE/pull/43) · merge commit `54217df0b04cf5fb4be654ade7d6b57ddb6b1d07` · migration `20261113030000_AddPriceListTypeTaxAndVersioning` · [step log](../steps/step-25-1-pricelist-schema-versioning.md)
> **Phạm vi:** ERPm-BE (Domain, Application, Infrastructure, Migrations, Verification)

## 1. Mô tả
Thiết kế Entity và Migration cho Bảng giá chuẩn ERP:
- Thêm `Type` (1: Sales, 2: Purchase) và `IsTaxInclusive` (bool) vào `PriceList`.
- Thêm bảng `PriceListVersions` (`VersionCode`, `EffectiveFrom`, `EffectiveTo`, `Status` Draft/Active/Expired/Archived).
- Chuyển `PriceListItems` liên kết qua `PriceListVersionId` (bỏ `PriceListId`).
- Migration PostgreSQL an toàn cho dữ liệu cũ.

## 2. Phân tích
Quyết định thiết kế:
- Enum lưu dạng `integer` (giống các enum khác trong repo): `PriceListType` Sales=1/Purchase=2; `PriceListVersionStatus` Draft=1/Active=2/Expired=3/Archived=4. Có CHECK constraint ở DB.
- `PriceListVersion` kế thừa `BaseAuditableEntity` (soft-delete, audit, tenant, ExternalId). `PriceListItem` vẫn là `BaseEntity`, cascade xoá theo version; version cascade theo bảng giá.
- Giữ `EffectiveFrom/EffectiveTo` trên `PriceList` để không vỡ hợp đồng API và `PricingEngine`; ngày hiệu lực của version là lớp lọc thêm khi tính giá.
- Unique `(price_list_id, version_code)` lọc `is_deleted = false`; unique dòng giá đổi sang `(price_list_version_id, item_id, uom_id, min_quantity)` vẫn `NULLS NOT DISTINCT`.
- Tên FK/index của `PriceListItem` đặt tường minh vì tên EF tự sinh vượt giới hạn 63 ký tự của PostgreSQL.

Backfill dữ liệu cũ (mỗi bảng giá, kể cả đã xoá mềm, nhận 1 version `V1`):
- Version lấy nguyên khoảng ngày của bảng giá; `Status = Expired (3)` nếu `effective_to < NOW()`, ngược lại `Active (2)`. Cờ `is_active` của bảng giá vẫn quyết định bảng giá có được tính giá hay không, nên hành vi cũ không đổi.
- Mọi dòng giá cũ được gán vào `V1` của bảng giá chứa nó. Bảng giá cũ mặc định `Type = Sales`, `IsTaxInclusive = false`.
- `external_id` của version dùng `gen_random_uuid()` (UUIDv4) như các migration backfill trước đây trong repo.
- Có hai guard `RAISE EXCEPTION` để dừng migration (rollback cả transaction) nếu có bảng giá `effective_to < effective_from` hoặc còn dòng giá không có version.

Thay đổi code đi kèm để build không vỡ (không đổi hợp đồng API):
- `PriceListService`: đọc/ghi dòng giá qua "version hiện hành" (Active mới nhất; bảng giá cũ chưa có thì tạo `V1`), đồng bộ ngày của version theo header, chỉ tính giá từ bảng giá `Type = Sales` và từ dòng của version Active còn trong hiệu lực.
- `ApplicationDbContext` / `IApplicationDbContext`: thêm `DbSet<PriceListVersion>`, cập nhật query filter của `PriceListItem` và `PriceListVersion`.

## 3. Features / Milestones
- [x] Feature 1 — Bổ sung `PriceListType` (`Sales`/`Purchase`) và `IsTaxInclusive` vào `PriceList` entity & `DbContext`.
- [x] Feature 2 — Thiết kế `PriceListVersion` entity, bảng `tbl_SD_PriceListVersion` và chuyển `PriceListItem` liên kết sang `PriceListVersionId`.
- [x] Feature 3 — Xây dựng migration `20261113030000_AddPriceListTypeTaxAndVersioning` kèm backfill `V1` an toàn và rollback `Down`.
- [x] Feature 4 — Cập nhật `PriceListService` tương thích ngược và bổ sung kiểm thử trong `ERPm.Verification`.
- [x] Feature 5 — Xác minh `dotnet build`, `dotnet ef migrations has-pending-model-changes`, CI GitHub Actions và migration trên Neon test branch.

## 4. Cách làm
File tạo mới:
- `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.Designer.cs`
- `.ai/steps/step-25-1-pricelist-schema-versioning.md`

File sửa:
- `src/ERPm.Domain/Entities/Sales/PriceList.cs`
- `src/ERPm.Application/Common/Interfaces/IApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContextModelSnapshot.cs`
- `src/ERPm.Infrastructure/Services/PriceListService.cs`
- `tests/ERPm.Verification/Program.cs`

Lưu ý quy trình: ở phiên làm việc ban đầu không có .NET SDK nên migration và Snapshot được viết/vá theo pattern hiện có; sau đó đã được kiểm chứng đầy đủ bằng `.NET 10 SDK` (`10.0.403`) và CI.

## 5. Kiểm tra
- **PostgreSQL 16 tạm (migration & constraints):** **28/28 kiểm tra đạt** (backfill `V1`, CHECK `type`/`status`/khoảng ngày, unique `version_code`, unique dòng giá `NULLS NOT DISTINCT`, cascade delete, `Down` rollback, guard chặn dữ liệu sai).
- **CI GitHub Actions (run `#43394426337`):** `dotnet build ERPm.sln`, `ERPm.Hrm.Verification` và `ERPm.Verification` đều **PASS**.
- **Neon test branch (migration & backfill):** Tạo `V1` cho 2/2 `PriceList` (`Active = 2`, `Expired = 3`), liên kết 4/4 `PriceListItem`; `missing_v1 = 0`, `orphan_items = 0` — **PASS**.
- **Kiểm tra trực tiếp trên `ERPm-BE/master` (`54217df0b04cf5fb4be654ade7d6b57ddb6b1d07`) với .NET SDK `10.0.403`:**
  - `dotnet build ERPm-BE/ERPm.sln --nologo -v q` → **PASS** (`0 Warning(s), 0 Error(s)`).
  - `dotnet ef migrations has-pending-model-changes --project ERPm-BE/src/ERPm.Infrastructure --startup-project ERPm-BE/src/ERPm.Api --no-build` → **PASS** (`No changes have been made to the model since the last migration.`).

Rollback: `dotnet ef database update 20261108033546_AddWarehouseOrgUnitAndItemVatRate`. `Down` mất dữ liệu có chủ đích: schema cũ chỉ chứa một tập dòng giá cho mỗi bảng giá nên chỉ giữ dòng của version Active mới nhất (hoặc version mới nhất nếu không có Active); dòng của version khác bị xoá. Sao lưu trước khi rollback nếu đã tạo nhiều version.

## 6. Việc còn lại
Không. Các phần DTO/API quản lý version, `PricingEngine` theo ngày chứng từ, tính thuế Inclusive/Exclusive, Clone & Bulk Adjustment được thực hiện ở S25.2.

## 7. Evidence và cập nhật
- Commit đã merge vào `ERPm-BE/master` qua PR [#43](https://github.com/I3Solution/ERPm-BE/pull/43):
  - `4550a0d` (`[db](SD) S25.1 price list Type, IsTaxInclusive and PriceListVersions`)
  - `2c6f47c` (`[docs] S25.1 log db task for price list versioning schema`)
  - `9197f0c` (`test: update price list verification for versioned schema`)
  - Merge commit: `54217df0b04cf5fb4be654ade7d6b57ddb6b1d07`
- CI / workflow run: GitHub Actions run `#43394426337` — PASS (`dotnet build ERPm.sln`, `ERPm.Hrm.Verification`, `ERPm.Verification`).
- Neon test branch: backfill 2/2 `PriceList` (`V1`), 4/4 `PriceListItem`, `missing_v1 = 0`, `orphan_items = 0` — PASS.
- Ngày cập nhật cuối: 2026-11-14
- Đã xác minh: build 0 warning/0 error, EF model snapshot khớp 100% với model hiện tại, CI verification và migration/backfill Neon đều đã pass.
