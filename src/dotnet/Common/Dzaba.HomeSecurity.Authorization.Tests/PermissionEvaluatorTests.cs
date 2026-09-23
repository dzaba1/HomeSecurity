using AutoFixture;
using Dzaba.HomeSecurity.Caching.Contracts;
using Dzaba.TestUtils;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dzaba.HomeSecurity.Authorization.Tests;

public sealed class PermissionEvaluatorTests : AutoFixtureTestFixture
{
    private const string PermissionKey = "router.view";
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string UserId = "user-1";
    private static readonly string CacheKey = $"perms:{TenantId}:{UserId}";

    private Mock<ICacheClient> cache = null!;
    private Mock<IPermissionSourceLoader> sourceLoader = null!;
    private PermissionEvaluator evaluator = null!;

    [SetUp]
    public void SetUp()
    {
        Fixture.Register<IOptions<PermissionCacheOptions>>(() => Options.Create(new PermissionCacheOptions()));

        cache = Fixture.FreezeMock<ICacheClient>();
        sourceLoader = Fixture.FreezeMock<IPermissionSourceLoader>();

        evaluator = Fixture.Create<PermissionEvaluator>();
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheHitAndPermissionGranted_ThenReturnsTrue()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cache.Setup(c => c.SetContainsAsync(CacheKey, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await evaluator.HasPermissionAsync(UserId, TenantId, PermissionKey, CancellationToken.None);

        result.Should().BeTrue();
        sourceLoader.Verify(s => s.LoadAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheHitAndPermissionNotGranted_ThenReturnsFalse()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cache.Setup(c => c.SetContainsAsync(CacheKey, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await evaluator.HasPermissionAsync(UserId, TenantId, PermissionKey, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheMiss_ThenPopulatesFromSourceLoaderBeforeChecking()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        sourceLoader.Setup(s => s.LoadAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(true, [PermissionKey]));
        cache.Setup(c => c.SetContainsAsync(CacheKey, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await evaluator.HasPermissionAsync(UserId, TenantId, PermissionKey, CancellationToken.None);

        result.Should().BeTrue();
        cache.Verify(c => c.SetAddAsync(CacheKey, new[] { PermissionKey }, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.KeyExpireAsync(CacheKey, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheMissAndUserHasNoPermissions_ThenPopulatesWithNoneSentinel()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        sourceLoader.Setup(s => s.LoadAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(true, []));
        cache.Setup(c => c.SetContainsAsync(CacheKey, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await evaluator.HasPermissionAsync(UserId, TenantId, PermissionKey, CancellationToken.None);

        result.Should().BeFalse();
        cache.Verify(c => c.SetAddAsync(CacheKey,
            It.Is<IReadOnlyCollection<string>>(v => v.Count == 1 && v.Single() != PermissionKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HasPermissionAsync_WhenCacheUnavailable_ThenFallsBackToSourceLoaderDirectly()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CacheUnavailableException("boom", new InvalidOperationException()));
        sourceLoader.Setup(s => s.LoadAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(true, [PermissionKey]));

        var result = await evaluator.HasPermissionAsync(UserId, TenantId, PermissionKey, CancellationToken.None);

        result.Should().BeTrue();
        cache.Verify(c => c.SetAddAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task IsMemberAsync_WhenCacheMissAndUserIsNotAMember_ThenReturnsFalseAndCachesNotMemberSentinel()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        sourceLoader.Setup(s => s.LoadAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(false, []));
        cache.Setup(c => c.SetContainsAsync(CacheKey, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await evaluator.IsMemberAsync(UserId, TenantId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task IsMemberAsync_WhenCacheHitAndUserIsAMember_ThenReturnsTrue()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cache.Setup(c => c.SetContainsAsync(CacheKey, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await evaluator.IsMemberAsync(UserId, TenantId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task IsMemberAsync_WhenCacheUnavailable_ThenFallsBackToSourceLoaderDirectly()
    {
        cache.Setup(c => c.KeyExistsAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CacheUnavailableException("boom", new InvalidOperationException()));
        sourceLoader.Setup(s => s.LoadAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(false, []));

        var result = await evaluator.IsMemberAsync(UserId, TenantId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task InvalidateAsync_DeletesTheCacheKey()
    {
        await evaluator.InvalidateAsync(TenantId, UserId, CancellationToken.None);

        cache.Verify(c => c.KeyDeleteAsync(CacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InvalidateAsync_WhenCacheUnavailable_ThenSwallowsTheException()
    {
        cache.Setup(c => c.KeyDeleteAsync(CacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CacheUnavailableException("boom", new InvalidOperationException()));

        var act = async () => await evaluator.InvalidateAsync(TenantId, UserId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
