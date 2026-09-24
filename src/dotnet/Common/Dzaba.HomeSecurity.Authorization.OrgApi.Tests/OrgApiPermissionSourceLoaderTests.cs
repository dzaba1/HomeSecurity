using System.Net;
using AutoFixture;
using Dzaba.TestUtils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Dzaba.HomeSecurity.Authorization.OrgApi.Tests;

public sealed class OrgApiPermissionSourceLoaderTests : AutoFixtureTestFixture
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string UserId = "user-1";
    private const string BearerToken = "Bearer relayed-token";

    private Mock<IHttpContextAccessor> httpContextAccessor = null!;

    [SetUp]
    public void SetUp()
    {
        httpContextAccessor = Fixture.FreezeMock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = BearerToken;
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(httpContext);
    }

    private OrgApiPermissionSourceLoader CreateLoader(StubHttpMessageHandler handler)
    {
        Fixture.Register(() => new HttpClient(handler) { BaseAddress = new Uri("http://org.test/") });

        return Fixture.Create<OrgApiPermissionSourceLoader>();
    }

    [Test]
    public async Task LoadAsync_WhenOrgServiceAnswersOk_ThenReturnsAMemberWithTheReturnedPermissionKeys()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"permissionKeys":["router.view","router.manage"]}""");

        var result = await CreateLoader(handler).LoadAsync(TenantId, UserId, CancellationToken.None);

        result.IsMember.Should().BeTrue();
        result.PermissionKeys.Should().BeEquivalentTo("router.view", "router.manage");
    }

    [Test]
    public async Task LoadAsync_WhenCalled_ThenRelaysTheCallersBearerTokenToTheTenantsAccessContextUrl()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"permissionKeys":[]}""");

        await CreateLoader(handler).LoadAsync(TenantId, UserId, CancellationToken.None);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri.Should().Be(new Uri($"http://org.test/api/v1/orgs/{TenantId}/access-context"));
        request.Headers.Authorization!.ToString().Should().Be(BearerToken);
    }

    [Test]
    public async Task LoadAsync_WhenOrgServiceAnswersNotFound_ThenReturnsANonMemberWithNoPermissions()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound);

        var result = await CreateLoader(handler).LoadAsync(TenantId, UserId, CancellationToken.None);

        result.IsMember.Should().BeFalse();
        result.PermissionKeys.Should().BeEmpty();
    }

    [Test]
    public async Task LoadAsync_WhenOrgServiceAnswersForbidden_ThenThrowsRatherThanSilentlyDenying()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Forbidden);

        var act = () => CreateLoader(handler).LoadAsync(TenantId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task LoadAsync_WhenThereIsNoHttpContext_ThenThrows()
    {
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);

        var act = () => CreateLoader(new StubHttpMessageHandler(HttpStatusCode.OK)).LoadAsync(TenantId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task LoadAsync_WhenTheRequestHasNoAuthorizationHeader_ThenThrowsInsteadOfCallingOrgService()
    {
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);

        var act = () => CreateLoader(handler).LoadAsync(TenantId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().BeEmpty();
    }
}
