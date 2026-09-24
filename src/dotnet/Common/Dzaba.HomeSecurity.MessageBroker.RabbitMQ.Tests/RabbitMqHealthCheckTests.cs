using EasyNetQ;
using EasyNetQ.Persistent;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ.Tests;

[TestFixture]
public class RabbitMqHealthCheckTests
{
    private Mock<IAdvancedBus> advanced = null!;
    private RabbitMqHealthCheck check = null!;

    [SetUp]
    public void SetUp()
    {
        advanced = new Mock<IAdvancedBus>();
        var bus = new Mock<IBus>();
        bus.SetupGet(b => b.Advanced).Returns(advanced.Object);
        check = new RabbitMqHealthCheck(bus.Object, new Mock<ILogger<RabbitMqHealthCheck>>().Object);
    }

    private void SetupState(params PersistentConnectionState[] states)
    {
        var sequence = advanced.SetupSequence(a => a.GetConnectionStatus(PersistentConnectionType.Producer));
        foreach (var state in states)
        {
            sequence.Returns(new PersistentConnectionStatus(PersistentConnectionType.Producer, state));
        }
    }

    [Test]
    public async Task CheckHealth_WhenTheConnectionIsNotInitialisedYet_ThenItConnectsAndReportsHealthy()
    {
        // Before the first publish EasyNetQ has not opened the connection;
        // the check has to be the one to ask for it.
        SetupState(PersistentConnectionState.NotInitialised, PersistentConnectionState.Connected);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        advanced.Verify(a => a.EnsureConnectedAsync(PersistentConnectionType.Producer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CheckHealth_WhenConnectingFails_ThenReportsUnhealthy()
    {
        SetupState(PersistentConnectionState.NotInitialised);
        advanced.Setup(a => a.EnsureConnectedAsync(PersistentConnectionType.Producer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("broker down");
    }

    [Test]
    public async Task CheckHealth_WhenConnectingHangs_ThenReportsUnhealthyInsteadOfBlockingReadiness()
    {
        SetupState(PersistentConnectionState.NotInitialised);
        advanced.Setup(a => a.EnsureConnectedAsync(PersistentConnectionType.Producer, It.IsAny<CancellationToken>()))
            .Returns<PersistentConnectionType, CancellationToken>((_, token) => Task.Delay(Timeout.Infinite, token));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Test]
    public async Task CheckHealth_WhenTheConnectionIsStillNotUpAfterEnsuring_ThenReportsUnhealthy()
    {
        SetupState(PersistentConnectionState.Disconnected, PersistentConnectionState.Disconnected);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Disconnected");
    }

    [Test]
    public async Task CheckHealth_WhenTheCallerCancels_ThenThePassedTokenCancellationPropagates()
    {
        SetupState(PersistentConnectionState.NotInitialised);
        advanced.Setup(a => a.EnsureConnectedAsync(PersistentConnectionType.Producer, It.IsAny<CancellationToken>()))
            .Returns<PersistentConnectionType, CancellationToken>((_, token) => Task.Delay(Timeout.Infinite, token));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => check.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
