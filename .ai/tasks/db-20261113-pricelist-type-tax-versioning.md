# [db] S25.1 Schema Bảng giá: Type, IsTaxInclusive, Versioning

> **Loại:** db
> **Ngày tạo:** 2026-11-13 · **Người làm:** Lợi (Dev chính Backend) · **Người yêu cầu:** Phúc (Kiến trúc & Review)
> **Trạng thái:** Doing (chờ `dotnet build` và chạy migration trên DB thật; xem mục 5)
> **Liên quan:** Trello card `[S25.1][BE] Schema Database & Entity Bảng giá (Type, Thuế, Versioning)` · migration `20261113030000_AddPriceListTypeTaxAndVersioning`

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

## 3. Cách làm
File tạo mới:
- `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/20261113030000_AddPriceListTypeTaxAndVersioning.Designer.cs`

File sửa:
- `src/ERPm.Domain/Entities/Sales/PriceList.cs`
- `src/ERPm.Application/Common/Interfaces/IApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContextModelSnapshot.cs`
- `src/ERPm.Infrastructure/Services/PriceListService.cs`

Lưu ý quy trình: phiên làm việc này **không có .NET SDK** (không tải được SDK/NuGet) nên không chạy được `dotnet ef migrations add`. Migration và Snapshot được viết/vá bằng tay theo đúng pattern migration hiện có; file Designer sinh từ Snapshot bằng script (đã kiểm chứng script trên Snapshot cũ: chỉ lệch đúng một hunk `credit_approved_*` đã tồn tại sẵn trong repo giữa Snapshot và Designer của `20261108033546`, không liên quan task này).

## 4. Kiểm tra
Đã chạy (PostgreSQL 16 tạm, schema cũ lấy từ migration `AddSalesPriceList`, dữ liệu mẫu gồm bảng giá còn hạn, hết hạn, đã xoá mềm, không có dòng; các khối SQL được trích nguyên văn từ file migration): **28/28 kiểm tra đạt**:
- Backfill: 1 version `V1` cho mỗi bảng giá, trạng thái đúng, ngày/tenant/xoá mềm được chép, 100% dòng giá được gán đúng bảng giá, mặc định Sales/không gồm thuế.
- Ràng buộc: CHECK `type`, `status`, khoảng ngày; unique `version_code` (cho dùng lại sau khi xoá mềm); unique dòng giá với `uom_id` NULL; cùng dòng ở version khác được phép; xoá bảng giá cascade xuống version và dòng.
- `Down`: giữ dòng của version Active mới nhất mỗi bảng giá, khôi phục index/FK cũ, xoá bảng và cột mới; vòng Up→Down→Up giữ đủ dòng; guard dừng migration khi dữ liệu sai; Up chạy được trên bảng rỗng.
- Cú pháp C# của 7 file đã sửa/tạo được parse sạch bằng tree-sitter.

**Chưa chạy được:** `dotnet build ERPm.sln` và `dotnet ef` (không có SDK). Cần chạy trên máy dev/CI trước khi merge.

Rollback: `dotnet ef database update 20261108033546_AddWarehouseOrgUnitAndItemVatRate`. `Down` mất dữ liệu có chủ đích: schema cũ chỉ chứa một tập dòng giá cho mỗi bảng giá nên chỉ giữ dòng của version Active mới nhất (hoặc version mới nhất nếu không có Active); dòng của version khác bị xoá. Sao lưu trước khi rollback nếu đã tạo nhiều version.

## 5. Việc còn lại
- Chạy `dotnet build ERPm.sln` (kỳ vọng 0 warning/0 error) và `dotnet ef migrations has-pending-model-changes` để chắc Snapshot khớp model; nếu EF báo lệch thì sinh lại migration bằng `dotnet ef migrations add` rồi giữ lại phần backfill SQL.
- Áp migration lên bản sao DB dev có dữ liệu bảng giá thật và kiểm tra số dòng trước/sau.
- Chưa làm (thuộc các card S25 sau): DTO/API cho `Type`, `IsTaxInclusive` và quản lý version (tạo Draft, publish, archive), tự chuyển Active→Expired theo ngày, ràng buộc không chồng lấn hiệu lực giữa các version Active, và lọc bảng giá Purchase theo nhà cung cấp.
- Chờ Phúc review.
