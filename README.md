# ERPm Backend

.NET 10 / EF Core 10.0.14 / PostgreSQL, Clean Architecture. SDK pin trong global.json. Trạng thái và giới hạn ở [.ai/status.md](.ai/status.md), [Step 19](.ai/steps/step-19-stabilization-and-internal-accounts.md).

Trong umbrella checkout, xem [hướng dẫn local](../docs/LOCAL_DEVELOPMENT.md). Tạo cấu hình ignored từ src/ERPm.API/appsettings.local.example.json hoặc dùng ConnectionStrings__DefaultConnection. Chạy dotnet build ERPm.sln; chạy API với --migrate để khởi tạo DB trống, rồi dotnet run --project src/ERPm.API --launch-profile http. BootstrapAdmin__Password chỉ dùng khi chưa có user.

Test: dotnet run --project tests/ERPm.Verification; cần local config hoặc ERPM_TEST_CONNECTION với quyền CREATE DATABASE. Test tạo DB riêng và giữ để đối chiếu. --setup-local tạo DB/admin mới dành cho umbrella checkout, không reset DB cũ.

Migrations/snapshot là nguồn schema chuẩn; SQL chỉ tham chiếu. Production cần HTTPS, secret environment và certificate mã hóa DataProtection keys. Workflow CI nhắm PostgreSQL 16; local đã kiểm tra PostgreSQL 18.
