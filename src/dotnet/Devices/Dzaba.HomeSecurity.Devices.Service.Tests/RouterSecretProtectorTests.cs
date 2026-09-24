using System.Security.Cryptography;
using Dzaba.HomeSecurity.Devices.Service.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

[TestFixture]
public sealed class RouterSecretProtectorTests
{
    private RouterSecretProtector protector = null!;

    [SetUp]
    public void SetUp()
    {
        protector = new RouterSecretProtector(DataProtectionProvider.Create("router-secret-protector-tests"));
    }

    [Test]
    public void Unprotect_WhenGivenWhatProtectReturned_ThenReturnsTheOriginalSecret()
    {
        var tenantId = Guid.NewGuid();

        var roundTripped = protector.Unprotect(tenantId, protector.Protect(tenantId, "hunter2"));

        roundTripped.Should().Be("hunter2");
    }

    [Test]
    public void Protect_WhenCalledTwiceForTheSameSecret_ThenCiphertextsDiffer()
    {
        var tenantId = Guid.NewGuid();

        protector.Protect(tenantId, "hunter2").Should().NotEqual(protector.Protect(tenantId, "hunter2"));
    }

    [Test]
    public void Unprotect_WhenCiphertextBelongsToAnotherTenant_ThenItCannotBeDecrypted()
    {
        var ciphertext = protector.Protect(Guid.NewGuid(), "hunter2");

        var act = () => protector.Unprotect(Guid.NewGuid(), ciphertext);

        act.Should().Throw<CryptographicException>();
    }
}
