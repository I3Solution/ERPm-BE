# 🗺️ LỘ TRÌNH PHÁT TRIỂN BACKEND (ERPM-BE ROADMAP)

> **Repository:** https://github.com/I3Solution/ERPm-BE.git  
> **Kiến trúc:** .NET 8 Clean Architecture, PostgreSQL, Dapper, EF Core

---

## 📌 GIAI ĐOẠN 1: NỀN TẢNG KIẾN TRÚC & QUẢN TRỊ BÁN HÀNG - KHO (HOÀN THÀNH)
- [x] **Clean Architecture Core:** Tách 4 tầng `Domain`, `Application`, `Infrastructure`, `API`.
- [x] **PostgreSQL Database Schema:** Schema script chuẩn, bảng tiền tố module (`tbl_SYS_`, `tbl_WMS_`, `tbl_SALES_`, `tbl_PURCHASE_`, `tbl_ACCT_`).
- [x] **Phân hệ Danh mục (Item & Attributes):** CRUD Item, Dynamic Attributes (JSONB + GIN Index), Categories, UOM.
- [x] **Multi-Branch Architecture:** Phân cấp chi nhánh vô hạn tầng (`tbl_SYS_Branch`), đệ quy lọc dữ liệu cha con theo quyền.
- [x] **Framework Base CRUD:** `BaseCrudController`, `IBaseCrudService` cho phát triển cực nhanh các entity chuẩn.
- [x] **Phân hệ Bán hàng & Hóa đơn:** `SalesInvoice`, trừ kho tức thời, sinh bút toán kép tự động.
- [x] **Phân hệ Tồn kho & Kho bãi:** `Warehouse`, `StockReceipt`, `StockIssue`, tính số dư tức thời.
- [x] **Phân hệ Đối tác (Partners):** Khách hàng, Nhà cung cấp, công nợ.
- [x] **Đa ngôn ngữ Backend:** `app_dictionaries` và JSONB translations.

---

## 📌 GIAI ĐOẠN 2: MUA HÀNG (PURCHASE) & SỔ QUỸ (CASH/BANK)
- [ ] **Phân hệ Mua hàng (PURCHASE):**
  - Đơn đặt hàng mua (Purchase Order).
  - Hóa đơn mua hàng (Purchase Invoice).
  - Tự động nhập kho từ đơn mua và ghi nhận công nợ NCC (TK 331).
- [ ] **Sổ quỹ & Thu chi (Cash/Bank Management):**
  - Phiếu thu tiền mặt/ngân hàng (Receipt Voucher).
  - Phiếu chi tiền mặt/ngân hàng (Payment Voucher).
  - Theo dõi số dư quỹ tức thời và liên kết hóa đơn bán/mua.

---

## 📌 GIAI ĐOẠN 3: SỔ CÁI KẾ TOÁN & BÁO CÁO TÀI CHÍNH
- [ ] Bảng cân đối tài khoản (Trial Balance).
- [ ] Sổ cái (General Ledger), Sổ nhật ký chung.
- [ ] Báo cáo kết quả kinh doanh (P&L) và Lưu chuyển tiền tệ.
