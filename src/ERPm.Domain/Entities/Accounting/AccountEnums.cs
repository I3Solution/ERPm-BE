namespace ERPm.Domain.Entities.Accounting;

public enum AccountType
{
    Asset = 1,       // Tài sản (1xx, 2xx)
    Liability = 2,   // Nợ phải trả (3xx)
    Equity = 3,      // Vốn chủ sở hữu (4xx)
    Revenue = 4,     // Doanh thu (5xx, 7xx)
    Expense = 5      // Chi phí & Giá vốn (6xx, 8xx)
}

public enum VoucherStatus
{
    Draft = 0,       // Đang lập phiếu (chưa ghi sổ)
    Posted = 1,      // Đã ghi sổ chính thức (không thể xóa)
    Cancelled = 2    // Đã hủy phiếu (lưu vết)
}
