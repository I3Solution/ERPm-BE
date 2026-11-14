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
- 2026-11-13: CI run #237 build và HRM Verification pass; ERPm.Verification phát hiện test fixture cố tạo thêm default address cho khách hàng đã có default. Đổi fixture sang địa chỉ non-default có sẵn.
- 2026-11-13: CI run #240/#242 tiếp tục phát hiện test dùng `Remove` gây hard-delete địa chỉ, vi phạm FK của SalesOrder. Sửa fixture mô phỏng đúng semantics ứng dụng: soft-delete bằng `IsDeleted` + `DeletedAtUtc`, giữ lại row nguồn và snapshot.
- 2026-11-14: Xác nhận GitHub Actions trên PR #45 (`bd2ced5`) đã PASS cả hai job (`verify` trên CI và `ERPm.Verification`).

## Files
- `src/ERPm.Domain/Entities/Sales/SalesQuotation.cs`
- `src/ERPm.Application/DTOs/Sales/SalesQuotationDtos.cs`
- `src/ERPm.Infrastructure/Services/SalesQuotationService.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/20261113170000_AddQuotationDeliveryAddressSnapshot.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- `tests/ERPm.Verification/Program.cs`
- Task/status documents.

## Verification
- Added checks for snapshot creation, invalid address/customer association, preserving the selected snapshot after source edits, and carrying it to SalesOrder after source deletion.
- GitHub Actions PR #45 (`bd2ced5`) passed: `dotnet build`, `ERPm.Hrm.Verification`, and `ERPm.Verification`.

## Bước tiếp theo
- Chờ Phúc review PR #45 trước khi merge.
