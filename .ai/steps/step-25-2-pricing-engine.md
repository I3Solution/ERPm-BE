# Step S25.2 — PricingEngine và version bảng giá

Ngày: 2026-11-13

## Mục tiêu
Triển khai định giá theo ngày chứng từ, thuế Inclusive/Exclusive, clone version và bulk adjustment trên schema S25.1.

## File tạo/chỉnh sửa
- `src/ERPm.Application/DTOs/Sales/PriceListDtos.cs`
- `src/ERPm.Application/Common/Crud/IPricingService.cs`
- `src/ERPm.Domain/Services/Sales/PricingEngine.cs`
- `src/ERPm.Infrastructure/Services/PriceListService.cs`
- `src/ERPm.API/Controllers/SD/PriceListsController.cs`
- `tests/ERPm.Verification/Program.cs`
- `src/ERPm.Infrastructure/Persistence/Seeders/system_dictionary.json`
- `.ai/tasks/change-20261113-s25-2-pricing-engine.md`
- `.ai/status.md`

## Thiết kế thực hiện
- Lọc version Active theo ngày chứng từ và ngày hiệu lực của price list header.
- Chọn version có EffectiveFrom mới nhất nếu các version Active bị chồng khoảng ngày.
- Tính VAT từ Item.VatRate; trả UnitPrice/LineTotal trước thuế, TaxAmount và TotalWithTax.
- Bổ sung `GET /api/v1/sales/price-lists/{priceListId}/versions` (danh sách version nhẹ kèm `ItemCount`, `Items` rỗng), `GET /api/v1/sales/price-lists/{priceListId}/versions/{versionId}` (chi tiết version kèm `Items`) và `GET /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/items` (quyền `sd:pricelist:view`) để FE dựng danh sách phiên bản, lấy `SourceVersionId` / `{versionId}` và xem items của từng version.
- Clone version tạo Draft và copy toàn bộ dòng giá; bulk adjustment chỉ nhận Draft.
- Không thay đổi database schema.

## Lệnh đã chạy
- `dotnet build ERPm.sln --nologo` (sau khi bổ sung các endpoint GET version): Build succeeded, 0 Warning(s), 0 Error(s).

## Verification
- Đã bổ sung verification cases trong `tests/ERPm.Verification/Program.cs` cho service + HTTP route + phân quyền của `GET .../versions`, `GET .../versions/{versionId}`, `GET .../versions/{versionId}/items`, `POST .../versions/clone` và `POST .../versions/{versionId}/bulk-adjustment`.
- PR #44 đã merge vào `master` (`d5cb0771a32e0407cc714e20a45ec751ea800f4a`); `dotnet build ERPm.sln --nologo -v q` trên `master` đạt `0 Warning(s), 0 Error(s)`.

## Hướng tiếp theo
- Triển khai task nối tiếp `S25.2b` (vòng đời version `activate`/`close`/CRUD items `Draft` & lọc `?type=Sales|Purchase`).
