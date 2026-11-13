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
- Clone version tạo Draft và copy toàn bộ dòng giá; bulk adjustment chỉ nhận Draft.
- Không thay đổi database schema.

## Lệnh đã chạy
- Chưa chạy lệnh .NET trong phiên này; thao tác sửa file thực hiện qua GitHub API.

## Verification
- Đã bổ sung verification cases trong source, gồm API route và quyền edit cho hai endpoint mới.
- CI trên commit `cbf141beb5c81caae7119c41dfe16754362fcc28` được xác nhận xanh; chờ review PR.

## Hướng tiếp theo
- Chạy CI, sửa lỗi compile/test nếu có.
- Phúc review; sau đó mới merge và cập nhật Trello Done.
