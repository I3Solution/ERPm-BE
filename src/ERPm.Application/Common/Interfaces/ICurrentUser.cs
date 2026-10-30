namespace ERPm.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsSystem { get; }
    bool IsAdmin { get; }
    int[] BranchIds { get; }

    // Data scope of the current request (IAM): unrestricted, extra departments and own records (SELF).
    bool HasUnrestrictedScope => false;
    int[] DepartmentIds => [];
    bool IncludeOwnRecords => false;
}
