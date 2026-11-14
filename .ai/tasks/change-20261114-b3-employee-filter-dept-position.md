# [change] Lọc danh sách nhân viên theo phòng ban và chức danh (B3)

> **Loại:** change
> **Ngày tạo:** 2026-11-14 · **Người làm:** Dev 2 ERPm · **Người yêu cầu:** BA (Trello #26)
> **Trạng thái:** Doing
> **Liên quan:** BE #50, ERPm-FE-HRM #13, FE #38; plan step 20 §6 ưu tiên #8

## 1. Mô tả
- Trước: GET /hrm/employees chỉ lọc theo từ khóa, chưa có tài khoản, chi nhánh (kèm nhánh con).
- Sau: lọc thêm theo phòng ban (kèm phòng ban con) và chức danh ở server, tránh tải hết danh sách về client.

## 2. Phân tích
- EmployeeFilterQuery đã có BranchId; cây chi nhánh dùng OrgTree.Subtree. Phòng ban có Department.ParentId nên dùng lại OrgTree cho phòng ban con.
- Employee đã có cột DepartmentId (nullable) và JobPositionId.

## 3. Cách làm
- HrmDtos.cs: thêm DepartmentId?, JobPositionId? vào EmployeeFilterQuery.
- HrmServices.cs (EmployeeService): tính cây con phòng ban trong GetPagedAsync, thêm Where trong ApplyFilters.
- IamChecks.cs: thêm check "employee list filters by department and job position".
- system_dictionary.json: emp.all_departments, emp.all_positions (khớp FE system.json).
- FE: ERPm-FE-HRM #13 (2 ô lọc, URL state), FE #38 (con trỏ, EmployeeFilter, từ điển).

## 4. Kiểm tra
- ERPm.Verification: 398 check PASS, gồm check mới. dotnet build API 0 lỗi.
- FE: tsc 0 lỗi; karma employees 7/7.
- Tester 1 ERPm: chờ kiểm lọc qua API thật.

## 5. Việc còn lại
- Tester 1 ERPm test; Thiện merge theo chuỗi chung.
- Chuỗi merge chung 9 PR: BE #44 → BE #46 → BE #50 (merge master vào trước, đồng bộ system_dictionary.json) → SD #10 → HRM #13 → CRM #5 → FE #36 → FE #37 (đổi base về master) → FE #38. Sau mỗi PR dictionary phải merge master vào PR sau và chạy lại CI FE.
