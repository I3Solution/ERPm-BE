# [change] S25.3 Tích hợp Địa chỉ Snapshot vào Báo giá & Convert to Order

> **Loại:** change
> **Ngày tạo:** 2026-11-13 · **Người làm:** Lợi · **Người yêu cầu:** Trello S25.3
> **Trạng thái:** Doing
> **Liên quan:** Trello [S25.3][BE] Tích hợp Địa chỉ Snapshot vào Báo giá & Convert to Order

## 1. Mô tả
- Bổ sung DeliveryAddressId, DeliveryRecipientName, DeliveryPhone và DeliveryAddressText vào SalesQuotation.
- Khi tạo/cập nhật báo giá, lấy địa chỉ thuộc đúng khách hàng và lưu snapshot; không phụ thuộc dữ liệu địa chỉ nguồn về sau.
- Khi convert Quotation -> SalesOrder, sao chép nguyên vẹn snapshot sang đơn hàng.
- Giữ tương thích request cũ khi không chọn DeliveryAddressId.

## 2. Phân tích
- SalesOrder đã có cơ chế snapshot địa chỉ và API DTO tương ứng.
- SalesQuotation hiện chưa có trường địa chỉ; ConvertToOrderAsync đang đọc Partner.Address tại thời điểm convert, làm mất snapshot theo thời điểm lập báo giá.
- Cần migration nullable để bảo toàn các báo giá hiện hữu.

## 3. Cách làm
- Thêm fields, DTO mapping/validation, snapshot address resolution và conversion copy.
- Thêm EF migration và cập nhật model snapshot.
- Bổ sung verification cho snapshot, validation khách hàng, convert và bất biến khi địa chỉ nguồn thay đổi/xóa.

## 4. Kiểm tra
- Chưa chạy; chờ build, verification và CI trên PR.

## 5. Việc còn lại
- Hoàn tất implementation, migration, tests, CI và chờ Phúc review.
