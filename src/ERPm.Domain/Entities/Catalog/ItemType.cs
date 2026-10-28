namespace ERPm.Domain.Entities.Catalog;

public enum ItemType
{
    Goods = 1,       // Hàng hóa hữu hình (quản lý tồn kho)
    Service = 2,     // Dịch vụ (không quản lý tồn kho)
    Combo = 3        // Gói combo sản phẩm
}
