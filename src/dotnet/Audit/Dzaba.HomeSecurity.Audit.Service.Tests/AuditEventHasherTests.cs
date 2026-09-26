using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

[TestFixture]
public sealed class AuditEventHasherTests
{
    private static AuditEvent NewEvent(byte[]? prevHash = null) => new()
    {
        EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        OccurredAt = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234560),
        ReceivedAt = DateTimeOffset.UtcNow,
        TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Category = AuditCategory.Access,
        ActorType = AuditActorType.User,
        ActorRef = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Action = "role.assigned",
        TargetType = "UserRole",
        TargetId = "user-1:role-1",
        Metadata = """{"roleName":"Admin"}""",
        PrevHash = prevHash ?? [],
        Hash = [],
    };

    [Test]
    public void Compute_WhenTheEventIsTheFirstInItsChain_ThenItMatchesTheKnownAnswer()
    {
        // Computed independently from the format's definition (length-prefixed
        // UTF-8 fields, little-endian int32 lengths, microseconds since
        // 0001-01-01 UTC). If this fails, the canonical form changed - which
        // invalidates every hash already stored, so it must be a versioned,
        // deliberate change, never an accident.
        var hash = AuditEventHasher.Compute(NewEvent());

        Convert.ToHexStringLower(hash).Should().Be("07b016b315fa59783bfffe9f5a4d3395070f24a1b5e6a3ff98e3cad0d337767a");
    }

    [Test]
    public void Compute_WhenTheEventFollowsAnother_ThenItMatchesTheKnownAnswer()
    {
        var hash = AuditEventHasher.Compute(NewEvent(Enumerable.Repeat((byte)0xAB, 32).ToArray()));

        Convert.ToHexStringLower(hash).Should().Be("87ce8452f8f9fad6ca99babae48cea93c39be79d6ef46c7db790a87e6cad124f");
    }

    [Test]
    public void Compute_WhenCalledTwiceOnTheSameEvent_ThenTheHashIsIdentical()
    {
        AuditEventHasher.Compute(NewEvent()).Should().Equal(AuditEventHasher.Compute(NewEvent()));
    }

    [Test]
    public void Compute_WhenTheStoredHashFieldIsSet_ThenItDoesNotAffectTheResult()
    {
        // A hash can't cover itself: only the event's content and PrevHash count.
        var withHash = NewEvent();
        withHash.Hash = [9, 9, 9];

        AuditEventHasher.Compute(withHash).Should().Equal(AuditEventHasher.Compute(NewEvent()));
    }

    // A change to each field the hash is defined over. Keyed by name because a
    // public test method can't take the service's internal AuditEvent type.
    private static void Mutate(AuditEvent e, string field)
    {
        switch (field)
        {
            case "EventId": e.EventId = Guid.NewGuid(); break;
            case "OccurredAt": e.OccurredAt = e.OccurredAt.AddTicks(10); break;
            case "TenantId": e.TenantId = Guid.NewGuid(); break;
            case "TenantId removed": e.TenantId = null; break;
            case "Category": e.Category = AuditCategory.Router; break;
            case "ActorType": e.ActorType = AuditActorType.Device; break;
            case "ActorRef": e.ActorRef = Guid.NewGuid(); break;
            case "Action": e.Action = "role.unassigned"; break;
            case "TargetType": e.TargetType = "Role"; break;
            case "TargetId": e.TargetId = "user-2:role-1"; break;
            case "Metadata": e.Metadata = """{"roleName":"Owner"}"""; break;
            case "Metadata whitespace": e.Metadata = """{"roleName": "Admin"}"""; break;
            case "PrevHash": e.PrevHash = [1]; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [TestCase("EventId")]
    [TestCase("OccurredAt")]
    [TestCase("TenantId")]
    [TestCase("TenantId removed")]
    [TestCase("Category")]
    [TestCase("ActorType")]
    [TestCase("ActorRef")]
    [TestCase("Action")]
    [TestCase("TargetType")]
    [TestCase("TargetId")]
    [TestCase("Metadata")]
    [TestCase("Metadata whitespace")]
    [TestCase("PrevHash")]
    public void Compute_WhenAnyHashedFieldChanges_ThenTheHashChanges(string field)
    {
        var original = AuditEventHasher.Compute(NewEvent());
        var changed = NewEvent();
        Mutate(changed, field);

        AuditEventHasher.Compute(changed).Should().NotEqual(original);
    }

    [Test]
    public void Compute_WhenAFieldBoundaryShiftsBetweenTwoFields_ThenTheHashChanges()
    {
        // Without length prefixes, ("ab", "c") and ("a", "bc") would hash alike.
        var first = NewEvent();
        first.TargetType = "ab";
        first.TargetId = "c";
        var second = NewEvent();
        second.TargetType = "a";
        second.TargetId = "bc";

        AuditEventHasher.Compute(first).Should().NotEqual(AuditEventHasher.Compute(second));
    }

    [Test]
    public void Compute_WhenTheSameInstantCarriesADifferentOffset_ThenTheHashIsTheSame()
    {
        var utc = NewEvent();
        var offset = NewEvent();
        offset.OccurredAt = utc.OccurredAt.ToOffset(TimeSpan.FromHours(2));

        AuditEventHasher.Compute(offset).Should().Equal(AuditEventHasher.Compute(utc));
    }

    [Test]
    public void Compute_WhenAnEarlierEventInAChainIsEdited_ThenEveryLaterHashChanges()
    {
        var firstOriginal = NewEvent();
        firstOriginal.Hash = AuditEventHasher.Compute(firstOriginal);
        var secondOriginal = NewEvent(firstOriginal.Hash);
        var expectedSecond = AuditEventHasher.Compute(secondOriginal);

        var firstTampered = NewEvent();
        firstTampered.Metadata = """{"roleName":"Owner"}""";
        firstTampered.Hash = AuditEventHasher.Compute(firstTampered);
        var secondAfterTamper = NewEvent(firstTampered.Hash);

        // Re-walking the chain: the second event's stored PrevHash is the old
        // first hash, which no longer equals the tampered first event's hash.
        secondOriginal.PrevHash.Should().NotEqual(firstTampered.Hash);
        AuditEventHasher.Compute(secondAfterTamper).Should().NotEqual(expectedSecond);
    }

    [Test]
    public void TruncateToMicroseconds_WhenTheValueHasFinerPrecision_ThenItDropsItRatherThanRounding()
    {
        var value = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        var truncated = AuditEventHasher.TruncateToMicroseconds(value);

        // PostgreSQL would round .1234567 to .123457; truncating gives the
        // stable .123456 that hashing and storing can both agree on.
        truncated.Should().Be(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234560));
        truncated.Offset.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void TruncateToMicroseconds_WhenTheValueIsAlreadyWholeMicroseconds_ThenItIsUnchanged()
    {
        var value = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234560);

        AuditEventHasher.TruncateToMicroseconds(value).Should().Be(value);
    }
}
