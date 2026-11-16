namespace ERPm.Application.DTOs.Accounting;

public sealed record PostingRuleResult(
    int ProfileId,
    string Module,
    string TransactionType,
    int? BranchId,
    string? DimensionKey,
    int DebitAccountId,
    string DebitAccountCode,
    int CreditAccountId,
    string CreditAccountCode);

public class PostingProfileDto
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? DimensionKey { get; set; }
    public int DebitAccountId { get; set; }
    public string DebitAccountCode { get; set; } = string.Empty;
    public string DebitAccountName { get; set; } = string.Empty;
    public int CreditAccountId { get; set; }
    public string CreditAccountCode { get; set; } = string.Empty;
    public string CreditAccountName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertPostingProfileRequest
{
    public string Module { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? DimensionKey { get; set; }
    public int DebitAccountId { get; set; }
    public int CreditAccountId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
