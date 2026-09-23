using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using StackExchange.Redis;
using Role = Dzaba.HomeSecurity.Data.Entities.Role;
using RolePermission = Dzaba.HomeSecurity.Data.Entities.RolePermission;
using UserRole = Dzaba.HomeSecurity.Data.Entities.UserRole;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// Exercises PermissionEvaluator directly - no HTTP, no WebApplicationFactory
/// - against FakeRedis's in-memory double (fresh tenant/user GUIDs per test,
/// so no explicit isolation/flush is needed) and an EF Core InMemory
/// AppDbContext.
/// </summary>
[TestFixture]
public class PermissionEvaluatorTests
{
    private IConnectionMultiplexer redis = null!;
    private DbContextOptions<AppDbContext> dbOptions = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        redis = FakeRedis.CreateConnectionMultiplexer();
        dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(dbOptions, new StaticTenantContext(Guid.Empty));
        await db.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        redis.Dispose();
    }

    private PermissionEvaluator CreateEvaluator(int ttlMinutes = 15) =>
        CreateEvaluator(redis, ttlMinutes);

    private PermissionEvaluator CreateEvaluator(IConnectionMultiplexer redisMultiplexer, int ttlMinutes = 15) =>
        new(redisMultiplexer, dbOptions, Options.Create(new PermissionCacheOptions { TtlMinutes = ttlMinutes }),
            NullLogger<PermissionEvaluator>.Instance);

    private async Task SeedRoleAssignmentAsync(Guid tenantId, string userId, Guid roleId, params string[] permissionKeys)
    {
        using var db = new AppDbContext(dbOptions, new StaticTenantContext(tenantId));
        db.Roles.Add(new Role { Id = roleId, TenantId = tenantId, Name = "Custom" });
        foreach (var key in permissionKeys)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionKey = key });
        }
        db.UserRoles.Add(new UserRole { UserId = userId, TenantId = tenantId, RoleId = roleId });
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheMissesAndUserHoldsRole_ThenPopulatesFromDbAndReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        await SeedRoleAssignmentAsync(tenantId, userId, Guid.NewGuid(), PermissionKeys.DeviceView);

        var result = await CreateEvaluator().HasPermissionAsync(userId, tenantId, PermissionKeys.DeviceView, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task HasPermissionAsync_WhenUserHoldsNoRoles_ThenReturnsFalseButStillCachesTheEmptyResult()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        var result = await CreateEvaluator().HasPermissionAsync(userId, tenantId, PermissionKeys.DeviceView, CancellationToken.None);

        result.Should().BeFalse();

        // Distinguishes "checked, has none" from "never checked" - the
        // sentinel-member design (docs/architecture/07) exists specifically
        // so this key still exists despite the permission set being empty.
        var db = redis.GetDatabase();
        (await db.KeyExistsAsync($"perms:{tenantId}:{userId}")).Should().BeTrue();
    }

    [Test]
    public async Task HasPermissionAsync_WhenCalledTwice_ThenSecondCallServesFromCacheNotTheDatabase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        await SeedRoleAssignmentAsync(tenantId, userId, Guid.NewGuid(), PermissionKeys.LogsView);

        var evaluator = CreateEvaluator();
        await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.LogsView, CancellationToken.None);

        // Remove the underlying role assignment directly - if the second
        // call still returns true, the cache (not the DB) answered it.
        using (var db = new AppDbContext(dbOptions, new StaticTenantContext(tenantId)))
        {
            db.UserRoles.RemoveRange(db.UserRoles);
            await db.SaveChangesAsync();
        }

        var result = await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.LogsView, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheMisses_ThenPopulatedKeyHasATtl()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        await CreateEvaluator(ttlMinutes: 5).HasPermissionAsync(userId, tenantId, PermissionKeys.DeviceView, CancellationToken.None);

        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync($"perms:{tenantId}:{userId}");

        ttl.Should().NotBeNull();
        ttl!.Value.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task InvalidateAsync_WhenCalledAfterTheUnderlyingRoleChanges_ThenNextCheckReflectsTheNewState()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        await SeedRoleAssignmentAsync(tenantId, userId, Guid.NewGuid(), PermissionKeys.LogsView);

        var evaluator = CreateEvaluator();
        (await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.LogsView, CancellationToken.None)).Should().BeTrue();

        using (var db = new AppDbContext(dbOptions, new StaticTenantContext(tenantId)))
        {
            db.UserRoles.RemoveRange(db.UserRoles);
            await db.SaveChangesAsync();
        }
        await evaluator.InvalidateAsync(tenantId, userId, CancellationToken.None);

        var result = await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.LogsView, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task InvalidateAsync_WhenNoCacheEntryExists_ThenDoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        var act = async () => await evaluator.InvalidateAsync(Guid.NewGuid(), Guid.NewGuid().ToString(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task HasPermissionAsync_WhenRedisIsUnavailableAndUserHoldsRole_ThenFallsBackToTheDatabaseAndReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        await SeedRoleAssignmentAsync(tenantId, userId, Guid.NewGuid(), PermissionKeys.DeviceView);

        var evaluator = CreateEvaluator(FakeRedis.CreateUnavailableConnectionMultiplexer());

        var result = await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.DeviceView, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task HasPermissionAsync_WhenRedisIsUnavailableAndUserHoldsNoRoles_ThenReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        var evaluator = CreateEvaluator(FakeRedis.CreateUnavailableConnectionMultiplexer());

        var result = await evaluator.HasPermissionAsync(userId, tenantId, PermissionKeys.DeviceView, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task InvalidateAsync_WhenRedisIsUnavailable_ThenDoesNotThrow()
    {
        var evaluator = CreateEvaluator(FakeRedis.CreateUnavailableConnectionMultiplexer());

        var act = async () => await evaluator.InvalidateAsync(Guid.NewGuid(), Guid.NewGuid().ToString(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
