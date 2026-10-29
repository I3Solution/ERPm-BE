# Step19 — ERPm-BE stabilization

Ngày2026-10-29. Sửa ưu tiên review, xác thực nội bộ và dữ liệu thực. Chi tiết phạm vi/toàn bộ lệnh và giới hạn ở Step19 root, hướng dẫn docs/LOCAL_DEVELOPMENT.md và bảng đối chiếu F01–F22.

## Mục tiêu

Schema/migration, authentication/scope/audit, transactional posting và lifecycle, costing/payment/ledger, order aggregate, tests/CI.

## Lệnh đã chạy

- `dotnet restore ERPm.sln --ignore-failed-sources -p:NuGetAudit=false`
- `dotnet build ERPm.sln --no-restore`
- `dotnet-ef migrations add StabilizeMvp`
- `dotnet-ef migrations add PreserveInventoryCarryingValue`
- `dotnet run --project tools/ERPm.Schema -- database/current_model.sql`
- `dotnet run --project tests/ERPm.Verification --no-restore`
- `dotnet run --project tests/ERPm.Verification --no-build -- --setup-local`
- `API --migrate (Development, chỉ DB local mới)`
- `dotnet build-server shutdown`
- `dotnet tests/ERPm.Verification/bin/Debug/net10.0/ERPm.Verification.dll (TEMP/TMP trên D:)`

## Verification

- Backend solution build: **0 warning, 0 error** với SDK10.0.403; không dùng dotnet8 trên PATH.
- PostgreSQL18: **44 kiểm tra đạt**, DB cuối erpm_verify_4e0da447e406407398afec289ace3df2. Fixture nâng cấp chứa UOM/item UUID cũ và FK; kiểm tra snapshot/model, dashboard0, costing/payment/journal/idempotency/validation/concurrency/JSONB/cancellation/scope/immutability/master-data guard và HTTP auth/orders/revoke. Fixture chưa bao phủ mọi dạng dữ liệu lịch sử tài chính.
- Chạy đồng thời 4 bán +4 nhập không mất cập nhật. Case nhập3@1 +4@2 giữ trị giá tồn11; xuất hết ghi giá vốn11, tồn quantity/value0.
- Frontend production build đạt; initial **604.08kB**, còn warning budget500kB và Stencil empty-glob. Spec type-check đạt; **7/7 ChromeHeadless đạt**.
- 238 key VI và238 key EN từ JSON chung; literal template keys và error codes backend không thiếu bản dịch.
- Local smoke: migration/seed, admin login/profile, dictionary238 key và dashboard0 đạt trên DB mới erpm_mvp_local_a7b56c332ef941d3962eb338c78fc765. Database cũ không reset. Credentials/config/keys đều được git check-ignore xác nhận ignored.
- CI workflow mới ở root/BE/FE chưa chạy trên GitHub; chưa có E2E toàn bộ app trên mobile/desktop hoặc kiểm tra thiết bị in.

## Gián đoạn và giới hạn môi trường

- Ban đầu công cụ gặp os error112; người dùng đã dọn dung lượng và công việc được tiếp tục. SDK10 thực tế đã có trong thư mục người dùng, ngoài PATH; đính chính đánh giá Step18.
- NuGet advisory feed không truy cập được; restore dùng cache và tắt audit chỉ qua tham số lệnh, không thay chính sách CI.
- Một lượt kiểm thử cuối bị Npgsql SSLRequest error do PostgreSQL **out of memory/recovery**, log Windows1455. Đã đọc log được cấp quyền, xác nhận ổ C: hết chỗ (D: còn~96GB), tắt MSBuild/Roslyn build servers và chạy lại tuần tự với TEMP/TMP trên D:. Lượt sau đạt đủ44. Không sửa logic để che lỗi và không restart/reset DB.
- Cần duy trì dung lượng C:/virtual memory để tránh sự cố vận hành lặp lại. Không tự dọn dữ liệu người dùng ngoài workspace.

## Hướng tiếp theo

Nghiệm thu MVP bằng quy trình thực tế; E2E mobile/desktop và CI private submodules; UI quản trị account/hủy chứng từ; PO→nhận hàng→hóa đơn mua và thu/chi; sau đó sổ cái, chuyển kho/kiểm kê, native/ESC-POS/offline. DB tạo thủ công bằng script cũ cần adoption riêng nếu giữ dữ liệu. Chưa commit/push/deploy server trong phiên.

## File tạo/chỉnh sửa/xóa (đường dẫn trong repo này)

- `.ai/roadmap.md` (M)
- `.ai/status.md` (M)
- `.gitignore` (M)
- `ERPm.sln` (M)
- `PROJECT_MASTER.md` (M)
- `database/erpm_schema.sql` (M)
- `docker-compose.yml` (M)
- `src/ERPm.API/Controllers/Base/BaseCrudController.cs` (M)
- `src/ERPm.API/Controllers/CRM/PartnersController.cs` (M)
- `src/ERPm.API/Controllers/PUR/PurchaseOrdersController.cs` (M)
- `src/ERPm.API/Controllers/SD/SalesController.cs` (M)
- `src/ERPm.API/Controllers/SD/SalesOrdersController.cs` (M)
- `src/ERPm.API/Controllers/SYS/BranchesController.cs` (M)
- `src/ERPm.API/Controllers/SYS/LocalizationController.cs` (M)
- `src/ERPm.API/Controllers/WMS/ItemsController.cs` (M)
- `src/ERPm.API/Controllers/WMS/StockController.cs` (M)
- `src/ERPm.API/Middlewares/ExceptionHandlingMiddleware.cs` (D)
- `src/ERPm.API/Program.cs` (D)
- `src/ERPm.API/appsettings.json` (M)
- `src/ERPm.Application/Common/ApiResponse.cs` (M)
- `src/ERPm.Application/DTOs/Inventory/InventoryDtos.cs` (M)
- `src/ERPm.Application/DTOs/Sales/SalesDtos.cs` (M)
- `src/ERPm.Application/Features/Sales/ISalesInvoiceService.cs` (M)
- `src/ERPm.Domain/Entities/Accounting/AccountEnums.cs` (M)
- `src/ERPm.Domain/Entities/Accounting/JournalEntry.cs` (M)
- `src/ERPm.Domain/Entities/Catalog/ItemType.cs` (M)
- `src/ERPm.Domain/Entities/Inventory/StockBalance.cs` (M)
- `src/ERPm.Domain/Entities/Inventory/StockReceipt.cs` (M)
- `src/ERPm.Domain/Entities/Partner/PartnerType.cs` (M)
- `src/ERPm.Domain/Entities/Sales/SalesInvoice.cs` (M)
- `src/ERPm.Infrastructure/DependencyInjection.cs` (M)
- `src/ERPm.Infrastructure/ERPm.Infrastructure.csproj` (M)
- `src/ERPm.Infrastructure/Persistence/ApplicationDbContext.cs` (M)
- `src/ERPm.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` (M)
- `src/ERPm.Infrastructure/Services/BranchService.cs` (M)
- `src/ERPm.Infrastructure/Services/Common/BaseCrudService.cs` (M)
- `src/ERPm.Infrastructure/Services/DashboardService.cs` (M)
- `src/ERPm.Infrastructure/Services/ItemService.cs` (M)
- `src/ERPm.Infrastructure/Services/PartnerService.cs` (M)
- `src/ERPm.Infrastructure/Services/SalesInvoiceService.cs` (D)
- `src/ERPm.Infrastructure/Services/StockService.cs` (M)
- `.ai/steps/step-19-stabilization-and-internal-accounts.md` (??)
- `.github/workflows/verify.yml` (??)
- `README.md` (??)
- `database/current_model.sql` (??)
- `global.json` (??)
- `src/ERPm.API/Controllers/SYS/AuthController.cs` (??)
- `src/ERPm.API/DesignTimeDbContextFactory.cs` (??)
- `src/ERPm.API/Middlewares/ApiExceptionMiddleware.cs` (??)
- `src/ERPm.API/Security/AccountService.cs` (??)
- `src/ERPm.API/Security/AccountValidationMiddleware.cs` (??)
- `src/ERPm.API/Security/CurrentUser.cs` (??)
- `src/ERPm.API/Security/WriteAccessFilter.cs` (??)
- `src/ERPm.API/Startup.cs` (??)
- `src/ERPm.API/appsettings.local.example.json` (??)
- `src/ERPm.Application/Common/BusinessException.cs` (??)
- `src/ERPm.Application/Common/Crud/IOrderWorkflow.cs` (??)
- `src/ERPm.Application/Common/Interfaces/ICurrentUser.cs` (??)
- `src/ERPm.Domain/Entities/System/AppUser.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Migrations/20261029144740_StabilizeMvp.Designer.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Migrations/20261029144740_StabilizeMvp.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Migrations/20261029153523_PreserveInventoryCarryingValue.Designer.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Migrations/20261029153523_PreserveInventoryCarryingValue.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Migrations/Sql/StabilizeMvp.sql` (??)
- `src/ERPm.Infrastructure/Persistence/Seeders/EssentialDataSeeder.cs` (??)
- `src/ERPm.Infrastructure/Persistence/Seeders/system_dictionary.json` (??)
- `src/ERPm.Infrastructure/Services/Common/PostingTransaction.cs` (??)
- `src/ERPm.Infrastructure/Services/PurchaseOrderService.cs` (??)
- `src/ERPm.Infrastructure/Services/SalesCancellationService.cs` (??)
- `src/ERPm.Infrastructure/Services/SalesOrderService.cs` (??)
- `src/ERPm.Infrastructure/Services/SalesPostingService.cs` (??)
- `src/ERPm.Infrastructure/Services/StockReceiptPostingService.cs` (??)
- `tests/ERPm.Verification/ERPm.Verification.csproj` (??)
- `tests/ERPm.Verification/Program.cs` (??)
- `tools/ERPm.Schema/ERPm.Schema.csproj` (??)
- `tools/ERPm.Schema/Program.cs` (??)
