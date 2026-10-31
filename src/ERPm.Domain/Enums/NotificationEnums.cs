namespace ERPm.Domain.Enums;

/// <summary>
/// Predefined notification categories matching enterprise ERP modules.
/// </summary>
public static class NotificationCategory
{
    public const string SYS = "SYS";
    public const string WMS = "WMS";
    public const string SD = "SD";
    public const string PUR = "PUR";
    public const string ACC = "ACC";
    public const string CRM = "CRM";
    public const string HRM = "HRM";
    public const string MFG = "MFG";
    public const string SCM = "SCM";
    public const string PM = "PM";
    public const string ALL = "ALL";
}

/// <summary>
/// Notification severity levels determining visual badges and alert styles.
/// </summary>
public static class NotificationSeverity
{
    public const string INFO = "INFO";
    public const string SUCCESS = "SUCCESS";
    public const string WARNING = "WARNING";
    public const string DANGER = "DANGER";
}

/// <summary>
/// Channels through which notifications can be delivered.
/// </summary>
public static class NotificationChannel
{
    public const string InApp = "IN_APP";
    public const string PushMobile = "PUSH_MOBILE";
    public const string Email = "EMAIL";
    public const string Sms = "SMS";
    public const string Webhook = "WEBHOOK";
}

/// <summary>
/// Target audience type for recipient routing.
/// </summary>
public static class NotificationTargetType
{
    public const string User = "USER";
    public const string JobPosition = "JOB_POSITION";
    public const string Department = "DEPARTMENT";
    public const string BranchTree = "BRANCH_TREE";
    public const string Company = "COMPANY";
    public const string All = "ALL";
    public const string Role = "ROLE";
}

/// <summary>
/// Execution status for asynchronous outbox delivery.
/// </summary>
public static class NotificationOutboxStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Sent = "SENT";
    public const string Failed = "FAILED";
}
