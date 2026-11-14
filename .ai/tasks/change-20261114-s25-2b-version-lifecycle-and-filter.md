# [change] S25.2b Vòng đời phiên bản bảng giá và lọc theo Type

> **Loại:** change
> **Ngày tạo:** 2026-11-14 · **Người làm:** Lợi (Dev chính Backend) · **Người yêu cầu:** Lợi
> **Trạng thái:** Doing
> **Liên quan:** Step 25 (`plan-step-25-enterprise-pricing-and-partner-address.md`) · Nối tiếp PR #44 (`loipham/s25.2-pricing-engine`) · Trello card `https://trello.com/c/W1QLkld1`

## 1. Mô tả
Bổ sung phần API còn lại so với hợp đồng Step 25 để hoàn thiện vòng đời phiên bản bảng giá và lọc loại bảng giá trên BE:
- **Vòng đời phiên bản (`Draft` ➔ `Active` ➔ `Expired`):**
  - `POST /api/v1/sales/price-lists/{priceListId}/versions` — Tạo phiên bản mới ở trạng thái `Draft`.
  - `PUT /api/v1/sales/price-lists/{priceListId}/versions/{versionId}` — Cập nhật thông tin phiên bản (chỉ cho phép khi `Draft`).
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/activate` — Kích hoạt phiên bản `Draft` thành `Active` (hỗ trợ publish bản nháp sau khi clone và bulk adjustment trên UI).
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/close` — Đóng phiên bản sang `Expired`.
- **Quản lý dòng giá trong phiên bản `Draft`:**
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items` — Thêm item vào phiên bản `Draft`.
  - `PUT /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items/{itemId}` — Cập nhật đơn giá, `MinQuantity`, chiết khấu của item trong phiên bản `Draft`.
  - `DELETE /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items/{itemId}` — Xóa item khỏi phiên bản `Draft`.
- **Lọc danh sách bảng giá theo `Type`:**
  - `GET /api/v1/sales/price-lists?type=Sales|Purchase` (hoặc `1|2`): hỗ trợ lọc server-side theo `PriceListType`.

## 2. Phân tích
- Hiện PR #44 (`S25.2`) đã có `GET .../versions`, `GET .../versions/{versionId}`, `GET .../versions/{versionId}/items`, `POST .../versions/clone`, `POST .../versions/{versionId}/bulk-adjustment`, nhưng chưa có API kích hoạt (`activate`), đóng (`close`), tạo/sửa `Draft` version và thêm/sửa/xóa lẻ dòng giá trên bản `Draft`.
- `PriceListsController` kế thừa `BaseCrudController` nhận `BaseFilterQuery`; cần mở rộng query filter hoặc action `GetPaged` của `PriceListsController` / `PriceListService` để hỗ trợ lọc `type` mà không ảnh hưởng các controller CRUD khác.
- Mọi thao tác sửa/kích hoạt/đóng version hoặc CRUD item trên version phải kiểm tra `PriceListVersionStatus` (`Draft` mới cho sửa/xóa item; không sửa version `Active`/`Expired`/`Archived`) và dùng quyền `sd:pricelist:edit`.

## 3. Cách làm
- Mở rộng DTO & `IPricingService` cho các request/operation: tạo/sửa `Draft` version, `ActivateVersionAsync`, `CloseVersionAsync`, thêm/sửa/xóa `PriceListItem` trên version `Draft`, và filter `Type` cho `GET /api/v1/sales/price-lists`.
- Cài đặt trong `PriceListService` và mở endpoint trong `PriceListsController` với `[RequirePermission("sd:pricelist:edit")]` / `[RequirePermission("sd:pricelist:view")]`.
- Bổ sung mã lỗi i18n VI/EN trong `system_dictionary.json` nếu phát sinh mã lỗi mới.
- Thêm verification cases (unit + HTTP route + phân quyền) trong `tests/ERPm.Verification/Program.cs`.

## 4. Kiểm tra
- `dotnet build ERPm-BE/ERPm.sln --nologo`: **Build succeeded, 0 Warning(s), 0 Error(s)**.
- Kiểm tra cú pháp và đối xứng khóa VI/EN `system_dictionary.json`: đạt.
- Đã bổ sung kiểm tra tự động (service + HTTP route + validation + i18n + phân quyền `sd:pricelist:view` / `sd:pricelist:edit`) trong `tests/ERPm.Verification/Program.cs`.
- `ERPm.Hrm.Verification` & `ERPm.Verification`: chưa chạy cục bộ do không có PostgreSQL/Docker đang chạy cục bộ; chờ chạy trên GitHub Actions CI (`verify.yml`) sau khi push.

## 5. Việc còn lại
- Push nhánh `loipham/s25.2b-version-lifecycle-and-filter` lên `ERPm-BE` và theo dõi GitHub Actions CI (`dotnet build`, `ERPm.Hrm.Verification`, `ERPm.Verification`) PASS.
- Mở PR về `ERPm-BE/master`, đồng bộ `ERPm-FE/src/assets/i18n/system.json` với 2 mã lỗi mới (`error.sd.pricelist_version_invalid_status`, `error.sd.pricelist_item_duplicate`) trước khi bump gitlink ở repo cha `ERPm`.
- Chuyển trạng thái sang `Done` sau khi CI xanh và PR được duyệt.
