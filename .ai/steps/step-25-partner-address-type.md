# Step 25 — Bổ sung `AddressType` cho Địa chỉ Đối tác (`PartnerDeliveryAddress`)

> **Ngày:** 2026-11-14
> **Nhánh:** `loipham/s25-partner-address-type`
> **Trello:** `https://trello.com/c/a9fIJoeG`
> **Task record:** [change-20261114-s25-partner-address-type.md](../tasks/change-20261114-s25-partner-address-type.md)

## 1. Mục tiêu
- Thêm phân loại `AddressType` (`Shipping = 1`, `Billing = 2`, `Office = 3`) cho `PartnerDeliveryAddress`, mặc định `Shipping`, tương thích JSON string của FE (`"Shipping" | "Billing" | "Office"`).
- Đổi quy tắc mặc định sang **1 địa chỉ mặc định riêng cho mỗi `(PartnerId, AddressType)`**, bảo toàn dữ liệu hiện có và giữ giới hạn 20 địa chỉ / đối tác.

## 2. File đã tạo / chỉnh sửa
- `src/ERPm.Domain/Entities/Partner/PartnerDeliveryAddress.cs`
- `src/ERPm.Application/DTOs/Partner/DeliveryAddressDtos.cs`
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/20261114141918_AddPartnerAddressType.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/20261114141918_AddPartnerAddressType.Designer.cs`
- `src/ERPm.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/ERPm.Infrastructure/Services/PartnerDeliveryAddressService.cs`
- `tests/ERPm.Verification/Program.cs`
- `.ai/tasks/change-20261114-s25-partner-address-type.md`
- `.ai/steps/step-25-partner-address-type.md`
- `.ai/status.md`

## 3. Lệnh & Kết quả kiểm tra
- `dotnet ef migrations add AddPartnerAddressType --project src/ERPm.Infrastructure --startup-project src/ERPm.API --output-dir Persistence/Migrations` → Thành công.
- `dotnet build ERPm.sln` → `0 Warning(s), 0 Error(s)`.
- `dotnet run --project tests/ERPm.Hrm.Verification --no-build` → `VERIFIED 28 HRM checks`.
- `dotnet run --project tests/ERPm.Verification --no-build` → `VERIFIED 495 checks`.

## 4. Bước tiếp theo
- Mở PR về `ERPm-BE/master`, chờ GitHub Actions `Verify backend` xanh, merge và cập nhật card Trello `a9fIJoeG`.
- Dev FE tiếp nhận hợp đồng mới để bật `addressTypeEnabled` trong card `S25.6` (`sIPU3Vqb`).
