# [change] S25.3 Tích hợp Địa chỉ Snapshot vào Báo giá & Convert to Order

> **Loại:** change
> **Ngày tạo:** 2026-11-13 · **Người làm:** Lợi · **Người yêu cầu:** Trello S25.3
> **Trạng thái:** Done
> **Liên quan:** Trello [S25.3][BE] Tích hợp Địa chỉ Snapshot vào Báo giá & Convert to Order (`https://trello.com/c/xscVHtTF`) · PR [#45](https://github.com/I3Solution/ERPm-BE/pull/45) (merge `299da6239a59f42c52ff6047f5a29f694e77e302`)

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
- Bổ sung bốn trường snapshot vào SalesQuotation; DTO create/update/detail nhận/trả DeliveryAddressId và các giá trị snapshot.
- Service chỉ nhận địa chỉ thuộc đúng khách hàng; khi update giữ nguyên bản snapshot nếu vẫn chọn cùng địa chỉ/khách hàng, kể cả nguồn đã đổi hoặc bị xóa.
- Convert sao chép bốn trường sang SalesOrder; DeliveryAddress legacy được giới hạn 500 ký tự, DeliveryAddressText giữ đủ tối đa 800 ký tự.
- Thêm migration nullable và index; không tạo FK để giữ ID nguồn làm provenance, snapshot không phụ thuộc vòng đời bản ghi địa chỉ.
- Thêm verification cho snapshot, địa chỉ không hợp lệ, update sau khi nguồn thay đổi và convert sau khi nguồn bị xóa.

## 4. Kiểm tra
- Đã cập nhật ERPm.Verification; GitHub Actions trên PR #45 (commit `bd2ced5`, run #245) đã PASS toàn bộ (`dotnet build`, `ERPm.Hrm.Verification`, `ERPm.Verification`).

## 5. Việc còn lại
Không. PR #45 đã được merge vào `master` tại commit `299da6239a59f42c52ff6047f5a29f694e77e302`.
