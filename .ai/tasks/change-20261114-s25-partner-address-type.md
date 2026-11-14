# [change] Bổ sung `AddressType` cho `PartnerDeliveryAddress` & mặc định theo từng loại

> **Loại:** change
> **Ngày tạo:** 2026-11-14 · **Người làm:** Lợi (BE) · **Người yêu cầu:** BA / Team S25 (Trello `a9fIJoeG`)
> **Trạng thái:** Done
> **Liên quan:** Step 25 (`https://trello.com/c/a9fIJoeG`, nối tiếp `S25.6` `https://trello.com/c/sIPU3Vqb`); nhánh `loipham/s25-partner-address-type`

## 1. Mô tả
- Hoàn tất phần Backend còn thiếu của card `[S25][BE]` (`a9fIJoeG`) — các endpoint `GET .../versions` và PR #44/#45/#50 đã có sẵn trên `master`, phần còn thiếu là phân loại `AddressType` trên `PartnerDeliveryAddress` và quy tắc địa chỉ mặc định theo từng loại (`Shipping = 1`, `Billing = 2`, `Office = 3`).
- Hợp đồng API JSON dùng chuỗi `"Shipping"`, `"Billing"`, `"Office"` (nhận được cả tên enum và giá trị số `1|2|3`, mặc định `"Shipping"` khi client chưa gửi trường `addressType`).

## 2. Phân tích
- Bảng `tbl_CRM_PartnerDeliveryAddress` trước đó chưa có cột `address_type` và chỉ có chỉ mục duy nhất `ux_partner_delivery_address_default` theo `(partner_id) WHERE is_default = true AND is_deleted = false`.
- Theo quyết định chốt trên card `a9fIJoeG`, mỗi đối tác được phép có **1 địa chỉ mặc định riêng cho từng `AddressType`**, đồng thời giữ nguyên giới hạn tổng **20 địa chỉ còn sống / đối tác**.
- Khóa từ điển `partners.address_type*` (5 khóa VI/EN) đã được đồng bộ sẵn trên `ERPm-BE/master` và `ERPm-FE/master`.

## 3. Cách làm
- `src/ERPm.Domain/Entities/Partner/PartnerDeliveryAddress.cs`: thêm `enum AddressType` (`Shipping = 1`, `Billing = 2`, `Office = 3`) gắn `[JsonConverter(typeof(JsonStringEnumConverter))]` và property `AddressType` mặc định `AddressType.Shipping`.
- `src/ERPm.Application/DTOs/Partner/DeliveryAddressDtos.cs`: thêm `AddressType` vào `DeliveryAddressDto` và `SaveDeliveryAddressRequest`.
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs` + migration `src/ERPm.Infrastructure/Persistence/Migrations/20261114141918_AddPartnerAddressType.cs`: thêm cột `address_type` (`DEFAULT 1`), check constraint `CK_tbl_CRM_PartnerDeliveryAddress_AddressType` (`address_type IN (1, 2, 3)`), index `ix_tbl_crm_partner_delivery_address_partner_id`, và đổi `ux_partner_delivery_address_default` sang `(partner_id, address_type) WHERE is_default = true AND is_deleted = false`.
- `src/ERPm.Infrastructure/Services/PartnerDeliveryAddressService.cs`:
  - `GetAsync`: sắp xếp `IsDefault` giảm dần, `AddressType` tăng dần (`Shipping` mặc định lên đầu), `CreatedAtUtc`, `Id`.
  - `CreateAsync` / `UpdateAsync` / `DeleteAsync` / `SetDefaultAsync`: tách riêng logic mặc định theo `(PartnerId, AddressType)`, kể cả trường hợp đổi `AddressType` khi đang là địa chỉ mặc định của loại cũ (`PromoteOldestDefaultAsync` + `PeersAsync`).
  - `Validate`: kiểm tra `Enum.IsDefined(request.AddressType)` (`400 error.validation.request`).
- `tests/ERPm.Verification/Program.cs`: bổ sung test unit/DB và HTTP contract cho `AddressType` và mặc định theo từng loại.
- Lệnh đã chạy:
  - `dotnet ef migrations add AddPartnerAddressType --project src/ERPm.Infrastructure --startup-project src/ERPm.API --output-dir Persistence/Migrations`
  - `dotnet build ERPm.sln`
  - `dotnet run --project tests/ERPm.Hrm.Verification --no-build`
  - `dotnet run --project tests/ERPm.Verification --no-build`

## 4. Kiểm tra
- `dotnet build ERPm.sln`: **0 Warning, 0 Error**.
- `ERPm.Hrm.Verification`: **VERIFIED 28 HRM checks**.
- `ERPm.Verification`: **VERIFIED 495 checks** (bao gồm `migration matches current model`, kiểm tra unique index theo `(partner_id, address_type)`, mặc định độc lập cho từng loại, chuyển loại địa chỉ đang mặc định, validate enum, và vòng đời HTTP API `/api/v1/partners/{partnerId}/delivery-addresses`).

## 5. Việc còn lại
- Sau khi PR merge vào `ERPm-BE/master`: áp dụng migration lên DB môi trường thực tế (`--migrate`) và chuyển sang card FE `S25.6` (`sIPU3Vqb`) để bật `addressTypeEnabled = true` khi team triển khai FE.
