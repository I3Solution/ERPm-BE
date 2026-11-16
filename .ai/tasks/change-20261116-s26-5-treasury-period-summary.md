# change-20261116-s26-5-treasury-period-summary

> **Trạng thái:** Doing
> **Người làm:** Dev 2 ERPm. Nhánh: thien/s26-5-treasury-summary (BE). Thẻ: S26.5 FE (KPI thu/chi trong kỳ).

## Mục tiêu
Thêm `GET /api/v1/accounting/treasury/summary?from&to&branchId` (quyền `acc:treasury:view`): tổng phiếu thu, tổng phiếu chi, dòng tiền ròng theo kỳ, để FE dashboard Dòng tiền không phải cộng từ danh sách phiếu.

## Quy tắc
- Chỉ phiếu Posted (Cancelled = đã đảo, Draft chưa hiệu lực); `to` gồm cả ngày cuối; mặc định từ đầu tháng đến hôm nay; `from > to` -> lỗi validation.
- `branchId` gồm cả chi nhánh con (BranchService).

## Checklist
- [x] DTO + service + controller
- [x] Check trong tests/ERPm.Verification/TreasuryChecks.cs
- [ ] Verification chạy xanh, PR, QA

## Verification
(chưa có kết quả)
