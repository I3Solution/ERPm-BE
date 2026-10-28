namespace ERPm.Domain.Entities.Partner;

[Flags]
public enum PartnerType
{
    Customer = 1,       // Khách hàng
    Supplier = 2,       // Nhà cung cấp
    Both = Customer | Supplier // Vừa là khách hàng vừa là nhà cung cấp
}
