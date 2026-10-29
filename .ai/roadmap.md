# Mốc hiện hành — Step 19, 2026-10-29

| Giai đoạn | Trạng thái có bằng chứng |
| --- | --- |
| 1: MVP bán hàng, kho, đối tác, dashboard | Đã ổn định source, 44 kiểm tra PostgreSQL/API và build đạt; chờ nghiệm thu thủ công |
| 2: Mua hàng và sổ quỹ | Có API đơn Draft/Confirmed và nhập mua hạch toán 156/331; chưa có luồng PO–nhận hàng–hóa đơn/thu chi đầy đủ |
| 3: Kế toán tài chính | Có ledger nền tảng; chưa có bộ sổ/báo cáo đầy đủ |
| 4: Native/offline, phân hệ mở rộng | Chưa triển khai |

Ưu tiên tiếp theo: nghiệm thu MVP/E2E/CI → PUR và thu chi → sổ cái → native/offline. Checklist thiết kế cũ bên dưới thể hiện kế hoạch/lịch sử, không thay thế kết quả nghiệm thu hiện hành.


---

## Tài liệu trước Step 19 — lưu để truy vết

# 🗺️ LỘ TRÌNH PHÁT TRIỂN BACKEND (ERPM-BE ROADMAP)

> **Repository:** https://github.com/I3Solution/ERPm-BE.git  
> **Kiến trúc:** .NET 10 Clean Architecture, PostgreSQL, Dapper, EF Core

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
