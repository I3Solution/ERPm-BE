namespace ERPm.Application.DTOs.Partner;

public class DeliveryAddressDto
{
    public int Id { get; set; }
    public int PartnerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveDeliveryAddressRequest
{
    public string RecipientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public bool IsDefault { get; set; }
}

public class SetUnlimitedCreditRequest
{
    public bool IsUnlimitedCredit { get; set; }
}
