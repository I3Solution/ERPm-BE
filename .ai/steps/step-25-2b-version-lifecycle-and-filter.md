# Step S25.2b — Vòng đời Phiên bản Bảng giá & Lọc theo Type

Ngày: 2026-11-14

## Mục tiêu
Hoàn thiện các API vòng đời phiên bản bảng giá (`Draft` ➔ `Active` ➔ `Expired`), CRUD dòng giá trên phiên bản `Draft`, và lọc danh sách bảng giá server-side theo `PriceListType`.

## File tạo/chỉnh sửa
- `src/ERPm.Application/DTOs/Sales/PriceListDtos.cs`
- `src/ERPm.Application/Common/Crud/IPricingService.cs`
- `src/ERPm.Infrastructure/Services/PriceListService.cs`
- `src/ERPm.API/Controllers/SD/PriceListsController.cs`
- `src/ERPm.Infrastructure/Persistence/Seeders/system_dictionary.json`
- `tests/ERPm.Verification/Program.cs`
- `.ai/tasks/change-20261114-s25-2b-version-lifecycle-and-filter.md`
- `.ai/steps/step-25-2b-version-lifecycle-and-filter.md`
- `.ai/status.md`

## Thiết kế thực hiện
- Mở rộng `PriceListFilterQuery : BaseFilterQuery` với `PriceListType? Type` và override `Search` trên `PriceListsController` + `ApplyFilters` trên `PriceListService` để hỗ trợ `?type=Sales|Purchase` hoặc `?type=1|2` (trả 400 nếu giá trị enum không hợp lệ).
- Bổ sung 4 API quản lý phiên bản (yêu cầu `sd:pricelist:edit`):
  - `POST /api/v1/sales/price-lists/{priceListId}/versions`: tạo version `Draft` mới.
  - `PUT /api/v1/sales/price-lists/{priceListId}/versions/{versionId}`: cập nhật `VersionCode`, `EffectiveFrom`, `EffectiveTo` (chỉ cho phép khi `Draft`).
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/activate`: chuyển `Draft` ➔ `Active` (cho phép nhiều version `Active` theo `PricingEngine` chọn bản mới nhất theo `EffectiveFrom`, `Id`).
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/close`: chuyển `Active` ➔ `Expired`.
- Bổ sung 3 API CRUD item trên phiên bản `Draft` (yêu cầu `sd:pricelist:edit`):
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items`
  - `PUT /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items/{itemId}`
  - `DELETE /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items/{itemId}`
- Thêm 2 mã lỗi VI/EN vào `system_dictionary.json`: `error.sd.pricelist_version_invalid_status` và `error.sd.pricelist_item_duplicate`.
- Không thay đổi schema DB / migration.

## Lệnh đã chạy
- `dotnet build ERPm-BE/ERPm.sln --nologo`: **Build succeeded, 0 Warning(s), 0 Error(s)**.
- Kiểm tra JSON & đối xứng key VI/EN `system_dictionary.json`: đạt.

## Verification
- Đã bổ sung kiểm tra service + HTTP route + validation + i18n + phân quyền (`sd:pricelist:view` / `sd:pricelist:edit`) trong `tests/ERPm.Verification/Program.cs`.
- `ERPm.Hrm.Verification` & `ERPm.Verification` chưa chạy cục bộ do không có PostgreSQL/Docker cục bộ; cần push để GitHub Actions CI (`verify.yml`) chạy đầy đủ.

## Hướng tiếp theo
- Push nhánh `loipham/s25.2b-version-lifecycle-and-filter`, kiểm tra CI xanh và mở PR về `master`.
- Đồng bộ `ERPm-FE/src/assets/i18n/system.json` cho 2 key mới trước khi bump gitlink ở repo cha `ERPm`.
