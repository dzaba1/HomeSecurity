using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;

namespace Dzaba.HomeSecurity.Audit.Service.Data;

/// <summary>
/// Computes an event's place in its tamper-evident hash chain:
/// <c>hash = SHA-256(canonical(prevHash, event))</c>. Anyone who edits a stored
/// event, or removes one from the middle of a chain, changes what the
/// following events' hashes should have been, which a re-walk of the chain
/// detects - see docs/architecture/16-auditing-and-compliance.md section 4.
/// </summary>
/// <remarks>
/// The canonical form is part of the stored data's contract: changing it (or
/// renaming an enum member that is hashed by name) invalidates every existing
/// chain. It is versioned for that reason, and pinned by a known-answer test.
/// Every field is length-prefixed so two different events can never produce
/// the same byte stream by shifting a boundary between fields.
/// </remarks>
internal static class AuditEventHasher
{
    private const string FormatVersion = "v1";

    // A .NET tick is 100 ns.
    private const long TicksPerMicrosecond = 10;

    /// <summary>
    /// Drops sub-microsecond precision. PostgreSQL stores timestamps to the
    /// microsecond while .NET carries 100 ns ticks, so a value must be
    /// truncated *before* it is hashed and stored, or the hash recomputed from
    /// what was read back would not match.
    /// </summary>
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % TicksPerMicrosecond), TimeSpan.Zero);

    public static byte[] Compute(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendText(hash, FormatVersion);
        AppendBytes(hash, auditEvent.PrevHash);
        AppendText(hash, auditEvent.EventId.ToString("N"));
        // Microseconds since 0001-01-01 UTC: independent of the offset the
        // value happens to carry.
        AppendText(hash, (auditEvent.OccurredAt.UtcTicks / TicksPerMicrosecond).ToString(CultureInfo.InvariantCulture));
        AppendText(hash, auditEvent.TenantId?.ToString("N") ?? string.Empty);
        AppendText(hash, auditEvent.Category.ToString());
        AppendText(hash, auditEvent.ActorType.ToString());
        AppendText(hash, auditEvent.ActorRef.ToString("N"));
        AppendText(hash, auditEvent.Action);
        AppendText(hash, auditEvent.TargetType);
        AppendText(hash, auditEvent.TargetId);
        AppendText(hash, auditEvent.Metadata);

        return hash.GetHashAndReset();
    }

    private static void AppendText(IncrementalHash hash, string value) =>
        AppendBytes(hash, Encoding.UTF8.GetBytes(value));

    private static void AppendBytes(IncrementalHash hash, byte[] value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}
