# [change] Nhân viên mới: cấp tài khoản ngay sau khi lưu (B10)

> **Loại:** change
> **Ngày tạo:** 2026-11-14 · **Người làm:** Dev 2 ERPm · **Người yêu cầu:** BA (Trello #32)
> **Trạng thái:** Doing
> **Liên quan:** ERPm-FE-HRM (employee-detail), FE; BE chỉ thêm khóa từ điển

## 1. Mô tả
- Trước: tạo hồ sơ nhân viên (/employees/new) và cấp tài khoản (nút "Cấp tài khoản") là 2 thao tác rời.
- Sau: màn tạo nhân viên có ô "Cấp tài khoản đăng nhập ngay sau khi lưu"; tick thì sau khi lưu mở ngay form tài khoản đã chọn sẵn nhân viên. Không đổi API hiện có.

## 2. Phân tích
- API hiện đủ: POST /hrm/employees trả Employee (id), POST /auth/users nhận employeeId. Không cần hợp đồng BE mới.
- Hai lời gọi không nguyên tử: nhân viên đã tạo nếu cấp tài khoản lỗi/đóng form; có thể cấp lại từ hồ sơ nhân viên.
- Chưa làm: wizard nhiều bước (thông tin → vị trí tổ chức → tài khoản); bản này là luồng liên tục một màn.

## 3. Cách làm
- BE: system_dictionary.json thêm emp.create_account_after_save (vi + en).
- FE: employee-detail (ts, html, spec) trong ERPm-FE-HRM; con trỏ + system.json ở FE.

## 4. Kiểm tra
- FE: tsc 0 lỗi; karma employee-detail 7/7 (2 ca mới).
- Tester 2 ERPm: chờ.

## 5. Việc còn lại
- Tester 2 test; Thiện merge theo chuỗi (từ điển dùng chung với BE #46/#50: merge master vào trước).
