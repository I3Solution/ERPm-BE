# change-20261116-s26-5-treasury-period-summary

> **Trạng thái:** Done
> **Người làm:** Dev 2 ERPm. Nhánh: thien/s26-5-treasury-summary (BE). Thẻ: S26.5 FE (KPI thu/chi trong kỳ).

## Mục tiêu
Thêm `GET /api/v1/accounting/treasury/summary?from&to&branchId` (quyền `acc:treasury:view`): tổng phiếu thu, tổng phiếu chi, dòng tiền ròng theo kỳ, để FE dashboard Dòng tiền không phải cộng từ danh sách phiếu.

## Quy tắc
- Chỉ phiếu Posted (Cancelled = đã đảo, Draft chưa hiệu lực); `to` gồm cả ngày cuối; mặc định từ đầu tháng đến hôm nay; `from > to` -> lỗi validation.
- `branchId` gồm cả chi nhánh con (BranchService).

## Checklist
- [x] DTO + service + controller
- [x] Check trong tests/ERPm.Verification/TreasuryChecks.cs
- [x] Verification chạy xanh, PR BE#75 merged (không có QA riêng của Tester; Thiện cho merge)

## Verification
Verification (docker PG local :5433): exit 0, 563 PASS, 0 FAIL; 2 check mới PASS ("Treasury period summary totals posted receipts and payments of the branch", "... excludes vouchers outside the period"). CI của PR không có check nào. FE đọc endpoint ở thẻ S26.5 FE (chưa làm).
