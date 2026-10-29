namespace ERPm.Domain.Common;

/// <summary>
/// RFC 9562 compliant time-ordered UUIDv7 generator optimized for PostgreSQL B-Tree indexing.
/// Uses native .NET 10 Guid.CreateVersion7() so the 48-bit Unix timestamp in milliseconds is placed
/// in the most significant bits and new values append cleanly to the right-most leaf page of B-Tree indexes.
/// </summary>
public static class Uuid7Generator
{
    public static Guid NewUuid7()
    {
        return Guid.CreateVersion7();
    }

    public static Guid NewUuid7(DateTimeOffset timestamp)
    {
        return Guid.CreateVersion7(timestamp);
    }
}

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? LastModifiedAtUtc { get; set; }
    string? LastModifiedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

public interface ITenant
{
    int? TenantId { get; set; }
}

public abstract class BaseEntity<TId>
{
    /// <summary>
    /// Internal auto-increment primary key used for high-speed relational JOINs and foreign keys.
    /// </summary>
    public TId Id { get; set; } = default!;

    /// <summary>
    /// Time-ordered UUIDv7 key used for external integrations, public URLs, and offline synchronization.
    /// </summary>
    public Guid ExternalId { get; set; } = Uuid7Generator.NewUuid7();
}

public abstract class BaseEntity : BaseEntity<int>
{
}

public abstract class BaseAuditableEntity : BaseEntity, IAuditable, ISoftDelete, ITenant
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public int? TenantId { get; set; }
}
