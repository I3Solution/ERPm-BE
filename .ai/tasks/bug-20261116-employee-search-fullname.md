# [bug] Tìm Nhân viên không khớp Họ tên

> **Loại:** bug
> **Ngày tạo:** 2026-11-16 · **Người làm:** Dev 2 (Thiện) · **Người yêu cầu:** BA 1 (Trello #117, phát hiện bởi Tester 2 khi đo HRM#23)
> **Trạng thái:** Doing
> **Liên quan:** nhánh thien/fix-employee-search-fullname

## 1. Mô tả
Ô tìm ở danh sách Nhân viên chỉ khớp Mã, gõ Họ tên không ra kết quả.

## 2. Phân tích
`BaseCrudService.ApplyFilters` chỉ quét các thuộc tính `Code`, `Name`, `OrderNumber`, `QuotationNumber`, `ReturnNumber`, `CustomerName`, `SupplierName`; `Employee` có `FullName` nên bị bỏ sót. Chỉ `Employee` có thuộc tính `FullName` trong Domain nên không ảnh hưởng entity khác.

## 3. Cách làm
- Thêm `FullName` vào danh sách thuộc tính tìm theo từ khoá (`BaseCrudService.cs`).
- Thêm check "the employee keyword matches the full name" vào `tests/ERPm.Verification/Program.cs`.

## 4. Kiểm tra
- `dotnet run --project tests/ERPm.Verification` trên PostgreSQL local (Docker :5433): VERIFIED 561 checks, gồm check mới.

## 5. Việc còn lại
- Chờ Tester đo và merge PR.
