namespace ERPm.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsSystem { get; }
    bool IsAdmin { get; }
    int[] BranchIds { get; }
}
