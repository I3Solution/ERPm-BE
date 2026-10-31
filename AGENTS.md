# 🤖 AGENTS.MD: HƯỚNG DẪN DÀNH CHO AI AGENT TRONG DỰ ÁN ERPM-BE

Chào bạn AI Assistant, bạn đang làm việc trên repository **ERPm-BE** - Phần mềm Backend .NET 10 Clean Architecture & PostgreSQL cho ERPm (Quản trị Bán hàng, Tồn kho & Kế toán mở rộng).
- **Repository Remote:** `https://github.com/I3Solution/ERPm-BE.git`

---

## 📌 QUY TẮC HOẠT ĐỘNG BẮT BUỘC

1. **Đọc tài liệu cốt lõi trước khi thực hiện nhiệm vụ:**
   - [PROJECT_MASTER.md](PROJECT_MASTER.md): Kiến trúc Clean Architecture, quy ước đặt tên bảng/cột và mô hình dữ liệu.
   - [.ai/roadmap.md](.ai/roadmap.md): Lộ trình phát triển API & nghiệp vụ Backend.
   - [.ai/status.md](.ai/status.md): Trạng thái hiện tại của Backend và các công việc tiếp theo.

2. **Quy chuẩn lưu vết từng Step vào thư mục `.ai/steps/`:**
   - Mỗi khi bạn hoàn thành một tính năng, controller, entity hoặc sửa đổi kiến trúc, bạn **PHẢI** tạo hoặc cập nhật file log:
     `ERPm-BE/.ai/steps/step-[số thứ tự]-[tên-ngắn-gọn].md`
   - File log cần nêu rõ:
     - Mục tiêu của step.
     - Danh sách file đã tạo / chỉnh sửa.
     - Các lệnh đã chạy (ví dụ `dotnet build`, `dotnet ef migrations add ...`).
     - Kết quả kiểm tra (Verification).
     - Hướng đi cho step tiếp theo.

3. **Cập nhật `.ai/status.md`:**
   - Cập nhật tiến độ phân hệ, trạng thái hoàn thành và next steps độc lập cho Backend.

4. **Kỷ luật Kỹ thuật Backend (.NET 10):**
   - Tuân thủ Clean Architecture (`Domain` -> `Application` -> `Infrastructure` -> `API`). Không nhồi nhét business logic vào Controllers.
   - Sử dụng Generic Base CRUD (`BaseCrudController`, `IBaseCrudService`) cho các danh mục chuẩn.
   - Database (PostgreSQL): Tiền tệ & số lượng bắt buộc dùng `decimal(18, 4)`. Hỗ trợ Soft-delete và Auditing.
   - Bảng giao dịch tài chính/kho bãi sẵn sàng cho Double-Entry Ledger.
   - Đa ngôn ngữ (i18n): Hỗ trợ `app_dictionaries` và JSONB translations.
   - **Code Comments (English Only):** Toàn bộ comment trong mã nguồn C# và SQL scripts bắt buộc 100% bằng Tiếng Anh. Không viết tiếng Việt trong source code.

5. **Quy tắc Git Commit (Commit Message Convention):**
   - Mọi lệnh `git commit` bắt buộc tuân thủ cú pháp:
     `[tag] nội dung commit` hoặc `[tag](MODULE) nội dung commit`
   - **Bộ 6 Tag chuẩn:**
     - `[add]`: Thêm mới tính năng, bảng, API, Entity, Service.
     - `[update]`: Cập nhật nghiệp vụ, nâng cấp kiến trúc hoặc tối ưu hiệu năng.
     - `[fix]`: Sửa lỗi (bug logic, lỗi SQL, lỗi build).
     - `[refactor]`: Tái cấu trúc mã nguồn không làm thay đổi nghiệp vụ.
     - `[db]`: Thay đổi riêng về SQL Schema, EF Migration, Index, Seed data.
     - `[docs]`: Cập nhật tài liệu (`PROJECT_MASTER.md`, `AGENTS.md`, `.ai/`).

6. **Quy tắc ghi chép theo loại Task** (đồng bộ với mục 6 `AGENTS.md` của repo gốc `ERPm`):
   - Mỗi việc thuộc đúng một loại dưới đây và được ghi ở đúng nơi tương ứng. Viết bằng Tiếng Việt.

     | Loại | Khi nào | Ghi ở đâu | Tag commit |
     | :--- | :--- | :--- | :--- |
     | **Step** (tính năng lớn theo roadmap) | Nhiều tầng/nhiều ngày, có đặc tả | Đặc tả ở repo gốc: `ERPm/.ai/plans/plan-step-XX-[ten].md` (tick `- [x]` từng Task khi xong). Log: `.ai/steps/step-XX-[ten].md` | `[add]` / `[update]` |
     | **change** (thay đổi yêu cầu) | Đổi nghiệp vụ/đặc tả đã chốt | Thuộc step đang làm: thêm mục "Thay đổi yêu cầu" (ngày, người yêu cầu, lý do, phạm vi) vào plan của step đó. Không thuộc step nào: `.ai/tasks/change-*.md` | `[update]` |
     | **bug** (sửa lỗi) | Lỗi logic, build, SQL | `.ai/tasks/bug-*.md`: hiện tượng, cách tái hiện, nguyên nhân gốc, cách sửa, test chống tái phát | `[fix]` |
     | **refactor** | Tái cấu trúc, không đổi nghiệp vụ | `.ai/tasks/refactor-*.md`: lý do, phạm vi, bằng chứng không đổi hành vi (test trước/sau) | `[refactor]` |
     | **db** (schema, migration, seed riêng lẻ) | Không nằm trong một step | `.ai/tasks/db-*.md`: migration, ảnh hưởng dữ liệu cũ, cách rollback | `[db]` |
     | **spike** (khảo sát, so sánh phương án) | Cần tìm hiểu trước khi quyết | `.ai/tasks/spike-*.md`: câu hỏi, phương án, kết luận/đề xuất | `[docs]` |
     | **review** (rà soát mã/tài liệu) | Đánh giá chất lượng, đối chiếu | Repo gốc: `ERPm/.ai/reviews/YYYY-MM-DD-[ten].md` | `[docs]` |

   - **Tên file task:** `.ai/tasks/[loai]-YYYYMMDD-[ten-ngan].md`, theo mẫu [`.ai/tasks/_TEMPLATE.md`](.ai/tasks/_TEMPLATE.md). Trường `Trạng thái` chỉ dùng: `Todo` / `Doing` / `Blocked` / `Done`.
   - **Task làm dở hoặc bị chặn:**
     - Thuộc một step: ghi vào mục **"Việc còn lại"** ở cuối file plan của step đó (task, trạng thái, ghi chú, đang chờ ai/việc gì).
     - Task riêng lẻ: để `Trạng thái: Blocked` hoặc `Doing` trong file task, ghi rõ việc còn lại.
     - `.ai/status.md` chỉ ghi một dòng ở **In-Progress** / **Next Steps** và trỏ link tới plan hoặc file task, không chép lại chi tiết.
   - **Khi xong:** chuyển trạng thái sang `Done`, ghi kết quả kiểm tra và commit liên quan, rồi thêm một dòng vào **Completed** của `.ai/status.md`.
   - Việc nhỏ (sửa chính tả, đổi một nhãn) không cần file task; commit message đúng tag là đủ.

7. **Quy tắc tạo card Trello:** theo mục 7 `AGENTS.md` của repo gốc `ERPm`.
   - Tên card: `[tag](MODULE) <MÃ> <tiêu đề>`.
   - Cột: Backlog / Todo / Doing / Blocked / Review / Done.
   - Mỗi card link tới plan hoặc file task, kèm checklist build và test.
