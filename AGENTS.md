# 🤖 AGENTS.MD: HƯỚNG DẪN DÀNH CHO AI AGENT TRONG DỰ ÁN ERPM-BE

Chào bạn AI Assistant, bạn đang làm việc trên repository **ERPm-BE** - Phần mềm Backend .NET 8 Clean Architecture & PostgreSQL cho ERPm (Quản trị Bán hàng, Tồn kho & Kế toán mở rộng).
- **Repository Remote:** `https://github.com/I3Solution/ERPm-BE.git`

---

## 📌 QUY TẮC HOẠT ĐỘNG BẮT BUỘC

1. **Đọc tài liệu cốt lõi trước khi thực hiện nhiệm vụ:**
   - [PROJECT_MASTER.md](file:///d:/WORKING%20PLACE/ERPm/ERPm-BE/PROJECT_MASTER.md): Kiến trúc Clean Architecture, quy ước đặt tên bảng/cột và mô hình dữ liệu.
   - [.ai/roadmap.md](file:///d:/WORKING%20PLACE/ERPm/ERPm-BE/.ai/roadmap.md): Lộ trình phát triển API & nghiệp vụ Backend.
   - [.ai/status.md](file:///d:/WORKING%20PLACE/ERPm/ERPm-BE/.ai/status.md): Trạng thái hiện tại của Backend và các công việc tiếp theo.

2. **Quy chuẩn lưu vết từng Step vào thư mục `.ai/steps/`:**
   - Mỗi khi bạn hoàn thành một tính năng, controller, entity hoặc sửa đổi kiến trúc, bạn **PHẢI** tạo hoặc cập nhật file log:
     `d:/WORKING PLACE/ERPm/ERPm-BE/.ai/steps/step-[số thứ tự]-[tên-ngắn-gọn].md`
   - File log cần nêu rõ:
     - Mục tiêu của step.
     - Danh sách file đã tạo / chỉnh sửa.
     - Các lệnh đã chạy (ví dụ `dotnet build`, `dotnet ef migrations add ...`).
     - Kết quả kiểm tra (Verification).
     - Hướng đi cho step tiếp theo.

3. **Cập nhật `.ai/status.md`:**
   - Cập nhật tiến độ phân hệ, trạng thái hoàn thành và next steps độc lập cho Backend.

4. **Kỷ luật Kỹ thuật Backend (.NET 8):**
   - Tuân thủ Clean Architecture (`Domain` -> `Application` -> `Infrastructure` -> `API`). Không nhồi nhét business logic vào Controllers.
   - Sử dụng Generic Base CRUD (`BaseCrudController`, `IBaseCrudService`) cho các danh mục chuẩn.
   - Database (PostgreSQL): Tiền tệ & số lượng bắt buộc dùng `decimal(18, 4)`. Hỗ trợ Soft-delete và Auditing.
   - Bảng giao dịch tài chính/kho bãi sẵn sàng cho Double-Entry Ledger.
   - Đa ngôn ngữ (i18n): Hỗ trợ `app_dictionaries` và JSONB translations.
   - **Code Comments (English Only):** Toàn bộ comment trong mã nguồn C# và SQL scripts bắt buộc 100% bằng Tiếng Anh. Không viết tiếng Việt trong source code.
