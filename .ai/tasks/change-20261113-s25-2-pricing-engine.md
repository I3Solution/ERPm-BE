# [change] S25.2 PricingEngine và dịch vụ phiên bản bảng giá

> **Loại:** change
> **Ngày tạo:** 2026-11-13 · **Người làm:** Lợi (Dev chính Backend) · **Người yêu cầu:** Lợi
> **Trạng thái:** Review
> **Liên quan:** Trello card [S25.2][BE] Động cơ định giá (PricingEngine) & Dịch vụ Phiên bản Bảng giá · S25.1 migration `20261113030000_AddPriceListTypeTaxAndVersioning`

## 1. Mô tả
Hoàn thiện nghiệp vụ định giá dựa trên schema versioning đã có:
- Chọn phiên bản bảng giá Active theo ngày chứng từ; khi có nhiều version Active cùng khớp thì chọn version có EffectiveFrom mới nhất, tie-break theo ID.
- Tính thuế theo `Item.VatRate` và `PriceList.IsTaxInclusive`; giá trả về tách thành giá chưa thuế, tiền thuế và tổng thanh toán.
- API clone version sang version mới trạng thái Draft, sao chép toàn bộ tier/UOM.
- API bulk adjustment cho Draft version theo phần trăm hoặc số tiền cố định, làm tròn theo bước cấu hình.

## 2. Phân tích
- Không thêm bảng/cột mới: dùng `PriceList.IsTaxInclusive`, `Item.VatRate`, `PriceListVersion` và `PriceListItem` đã có từ S25.1.
- `DocumentDate` là UTC; để tương thích client cũ, nếu không truyền thì mặc định `DateTime.UtcNow`.
- Với Tax Inclusive, bóc tách giá chưa thuế bằng `price / (1 + VatRate / 100)`; Tax Exclusive giữ giá chưa thuế và cộng VAT. Chiết khấu được trừ trước khi tính thuế.
- Clone chỉ tạo Draft; Bulk Adjustment chỉ sửa Draft để không thay đổi snapshot version đã Active/Expired.
- Bulk adjustment làm tròn đến bội số `RoundingIncrement`, midpoint away from zero, lưu 4 chữ số thập phân.
- Không tự động publish version; thao tác publish/activate là ngoài phạm vi card.

## 3. Cách làm
- Cập nhật DTO/API cho Type, IsTaxInclusive, DocumentDate, kết quả VAT và thông tin version.
- Bổ sung thông báo lỗi phiên bản bảng giá trong dictionary VI/EN.
- Cập nhật PricingEngine và PriceListService; chọn version theo ngày chứng từ.
- Thêm API:
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/clone`
  - `POST /api/v1/sales/price-lists/{priceListId}/versions/{versionId}/bulk-adjustment`
- Thêm verification cho Tax Inclusive/Exclusive, clone, adjustment, chọn version theo ngày.
- Không có thay đổi schema/migration dự kiến.

## 4. Kiểm tra
- Đã thêm kiểm tra tự động vào `tests/ERPm.Verification/Program.cs` cho tax inclusive/exclusive, clone version, bulk percentage/fixed-amount adjustment, rounding increment, chọn version theo ngày chứng từ và route/authorization HTTP của hai API mới.
- GitHub Actions trên commit `cbf141beb5c81caae7119c41dfe16754362fcc28`: người phụ trách xác nhận toàn bộ CI xanh.

## 5. Việc còn lại
- Kiểm tra API contract, phân quyền `sd:pricelist:edit`, và dữ liệu thực trên DB test.
- Chờ Phúc review PR; xử lý feedback trước khi merge.
