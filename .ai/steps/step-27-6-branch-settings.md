# Step 27.6 · Branch Settings: DB, resolver và API

- **Mục tiêu:** catalog key có kiểu, override theo `(branchId, key)`, lịch sử, quyền `sys:branch-setting:{view,edit,manage_definition}`, resolver tại chỗ → cha gần nhất → mặc định trong cùng công ty, API theo `ERPm/docs/BRANCH_SETTINGS_API_CONTRACT.md`.
- **File:** `Domain/Entities/System/BranchSetting.cs`, `Application/DTOs/System/BranchSettingDtos.cs`, `Application/Features/System/IBranchSettingService.cs`, `Infrastructure/Services/BranchSettingService.cs`, `API/Controllers/SYS/BranchSettingsController.cs`, `PermissionCatalog`, `SystemRoles`, `ICurrentUser.CanAt`, migration `20261121062222_AddBranchSettings`, `system_dictionary.json`, `tests/ERPm.Verification/BranchSettingChecks.cs`.
- **Lệnh:** `dotnet ef migrations add AddBranchSettings`, `dotnet build ERPm.sln`, `dotnet ef migrations has-pending-model-changes`, `dotnet run --project tests/ERPm.Verification`.
- **Verification:** build 0/0; không còn thay đổi model; `VERIFIED 725 checks` (44 mới).
- **Tiếp theo:** S27.8 đăng ký `wms.so_warehouse_policy` qua `IBranchSettingModuleProvider`; S27.7 dựng UI theo contract.
- **Task record:** `ERPm/.ai/tasks/add-20261121-s27-6-branch-settings.md`.
