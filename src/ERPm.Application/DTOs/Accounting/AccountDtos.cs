using ERPm.Domain.Entities.Accounting;

namespace ERPm.Application.DTOs.Accounting;

public class AccountDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public AccountType AccountType { get; set; }
    public AccountNature Nature { get; set; }
    public bool IsPostable { get; set; }
    public bool TrackPartner { get; set; }
    public bool TrackBranch { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; }
    public List<AccountDto> Children { get; set; } = [];
}

public class CreateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string>? Translations { get; set; }
    public AccountType AccountType { get; set; } = AccountType.Asset;
    public AccountNature Nature { get; set; } = AccountNature.DebitOnly;
    public bool IsPostable { get; set; } = true;
    public bool TrackPartner { get; set; } = false;
    public bool TrackBranch { get; set; } = true;
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string>? Translations { get; set; }
    public AccountType AccountType { get; set; } = AccountType.Asset;
    public AccountNature Nature { get; set; } = AccountNature.DebitOnly;
    public bool IsPostable { get; set; } = true;
    public bool TrackPartner { get; set; } = false;
    public bool TrackBranch { get; set; } = true;
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}
