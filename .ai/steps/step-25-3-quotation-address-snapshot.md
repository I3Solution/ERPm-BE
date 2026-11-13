# Step 25.3 — Quotation Delivery Address Snapshot

## Mục tiêu
Đưa snapshot địa chỉ giao hàng vào SalesQuotation và chuyển nguyên vẹn snapshot sang SalesOrder khi convert, không đọc lại địa chỉ nguồn ở thời điểm convert.

## Phạm vi
- Domain: bốn trường DeliveryAddressId, DeliveryRecipientName, DeliveryPhone, DeliveryAddressText.
- API: create/update/detail DTO hỗ trợ chọn DeliveryAddressId và trả snapshot.
- Service: validate địa chỉ thuộc khách hàng; giữ snapshot cũ khi update cùng địa chỉ đã chọn; copy snapshot trong convert.
- Database: migration nullable + index/FK phù hợp.
- Verification: tạo/cập nhật, sai khách hàng, nguồn địa chỉ thay đổi/xóa, convert.

## Nhật ký
- 2026-11-13: Khảo sát SalesOrder snapshot hiện hữu và luồng SalesQuotation; xác định lỗi hiện tại là convert đọc Partner.Address thay vì snapshot tại báo giá.
- 2026-11-13: Tạo branch `loipham/s25.3-quotation-address-snapshot` từ `master`.

## Files
- Dự kiến: SalesQuotation entity/DTO/service, ApplicationDbContext, migration + model snapshot, ERPm.Verification, task/status docs.

## Verification
- Chưa chạy. Không ghi nhận build/test pass cho tới khi có CI hoặc lệnh thực thi xác nhận.

## Bước tiếp theo
- Hoàn thiện schema, service và test; mở PR để Phúc review trước khi merge.
