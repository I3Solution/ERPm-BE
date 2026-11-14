# [change] FE Bảng giá (S25.4) và Địa chỉ đối tác (S25.6)

> **Loại:** change
> **Ngày tạo:** 2026-11-14 · **Người làm:** Dev 2 ERPm · **Người yêu cầu:** BA (Trello #95, #97)
> **Trạng thái:** Doing
> **Liên quan:** BE #43/#44/#45/#46; ERPm-FE-SD, ERPm-FE-CRM; FE #36, #37, và PR phần 2 (phiên bản)

## 1. Mô tả
- S25.4: Price Lists có loại Bán/Mua, cờ giá gồm VAT, danh sách phiên bản, Clone phiên bản, điều chỉnh giá hàng loạt.
- S25.6: địa chỉ đối tác: validation, giới hạn 20 địa chỉ qua i18n, ô Loại địa chỉ dựng sẵn nhưng ẩn.

## 2. Phân tích
- Contract BE sau BE #44: GET price-lists/{id}/versions, GET .../versions/{vid}(/items), POST .../versions/clone, POST .../versions/{vid}/bulk-adjustment.
- AddressType chưa có ở BE (thẻ riêng của Phúc), nên S25.6 chỉ làm phần không đụng contract.
- Làm sau (BA chốt): chuyển Price Lists sang app-list-page + list query state, PR riêng từ master sau khi chuỗi S25 merge.

## 3. Cách làm
- BE: chỉ khóa từ điển (system_dictionary.json) cho S25.4/S25.6.
- FE-SD: loại/thuế ở form và danh sách; component price-list-versions ở trang chi tiết.
- FE-CRM: partner-delivery-addresses (khoảng trắng, i18n, cờ addressTypeEnabled).

## 4. Kiểm tra
- Karma price-lists 10/10, partner-delivery-addresses 2/2; tsc 0 lỗi.
- Tester 1/2 ERPm: phần Bán/Mua + VAT và địa chỉ PASS (khi có BE #44). Phần 2 (phiên bản): chờ.

## 5. Việc còn lại
- Test phần 2; bật ô AddressType khi BE có cột; chuyển sang app-list-page nếu BA yêu cầu.
