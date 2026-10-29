namespace ERPm.Domain.Entities.Partner;

[Flags]
public enum PartnerType
{
    Customer = 1,
    Supplier = 2,
    Both = Customer | Supplier
}
