using AutoFixture;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.TestUtils;
using EasyNetQ;
using EasyNetQ.Consumer;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IMessageHandler =Dzaba.HomeSecurity.MessageBroker.Contracts.IMessageHandler;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ.Tests;

[TestFixture]
public sealed class SubscriptionTests : AutoFixtureTestFixture
{
    private HandlerBehaviour behaviour = null!;
    private ServiceProvider provider = null!;
    private Subscription<RecordingHandler> subscription = null!;

    [SetUp]
    public void SetUp()
    {
        behaviour = new HandlerBehaviour();

        var services = new ServiceCollection();
        services.AddSingleton(behaviour);
        services.AddScoped<ScopeMarker>();
        services.AddTransient<RecordingHandler>();
        provider = services.BuildServiceProvider();

        Fixture.Register(() => provider.GetRequiredService<IServiceScopeFactory>());
        Fixture.Register(() => new SubscriptionOptions { Queue = "homesecurity.test", RoutingKeys = ["role.assigned"] });
        Fixture.Register<ILogger<Subscription<RecordingHandler>>>(() => NullLogger<Subscription<RecordingHandler>>.Instance);
        Fixture.Register(() => new Mock<IBus>().Object);

        subscription = Fixture.Create<Subscription<RecordingHandler>>();
    }

    [TearDown]
    public void TearDown()
    {
        subscription.Dispose();
        provider.Dispose();
    }

    private static MessageReceivedInfo Info(string routingKey) =>
        new("consumer", 1, false, ExchangeNames.Events, routingKey, "homesecurity.test");

    [Test]
    public async Task OnMessage_WhenTheHandlerAcknowledges_ThenTheMessageIsAcked()
    {
        behaviour.Outcome = MessageOutcome.Acknowledge;

        var strategy = await subscription.OnMessageAsync(new byte[] { 1 }, new MessageProperties(), Info("role.assigned"));

        strategy.Should().BeSameAs(AckStrategies.AckAsync);
    }

    [Test]
    public async Task OnMessage_WhenTheHandlerAsksForARetry_ThenTheMessageIsRequeued()
    {
        behaviour.Outcome = MessageOutcome.Retry;

        var strategy = await subscription.OnMessageAsync(new byte[] { 1 }, new MessageProperties(), Info("role.assigned"));

        strategy.Should().BeSameAs(AckStrategies.NackWithRequeueAsync);
    }

    [Test]
    public async Task OnMessage_WhenTheHandlerRejects_ThenTheMessageIsDeadLetteredNotRequeued()
    {
        behaviour.Outcome = MessageOutcome.Reject;

        var strategy = await subscription.OnMessageAsync(new byte[] { 1 }, new MessageProperties(), Info("role.assigned"));

        strategy.Should().BeSameAs(AckStrategies.NackWithoutRequeueAsync);
    }

    [Test]
    public async Task OnMessage_WhenTheHandlerThrows_ThenTheMessageIsRequeuedAndNothingEscapes()
    {
        behaviour.Failure = new InvalidOperationException("database is down");

        var strategy = await subscription.OnMessageAsync(new byte[] { 1 }, new MessageProperties(), Info("role.assigned"));

        strategy.Should().BeSameAs(AckStrategies.NackWithRequeueAsync);
    }

    [Test]
    public async Task OnMessage_WhenCalled_ThenTheHandlerGetsTheRoutingKeyAndTheRawBody()
    {
        await subscription.OnMessageAsync(new byte[] { 7, 8, 9 }, new MessageProperties(), Info("membership.added"));

        var received = behaviour.Received.Should().ContainSingle().Subject;
        received.RoutingKey.Should().Be("membership.added");
        received.Body.Should().Equal(7, 8, 9);
    }

    [Test]
    public async Task OnMessage_WhenTwoMessagesArrive_ThenEachIsHandledInItsOwnScope()
    {
        // A scoped service such as a DbContext must not be shared between messages.
        await subscription.OnMessageAsync(new byte[] { 1 }, new MessageProperties(), Info("role.assigned"));
        await subscription.OnMessageAsync(new byte[] { 2 }, new MessageProperties(), Info("role.assigned"));

        behaviour.Received.Select(r => r.ScopeId).Distinct().Should().HaveCount(2);
    }

    [Test]
    public void ToAckStrategy_WhenTheOutcomeIsUnknown_ThenItThrows()
    {
        var act = () => Subscription<RecordingHandler>.ToAckStrategy((MessageOutcome)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Constructor_WhenThereIsNoQueueName_ThenItThrows()
    {
        var options = new SubscriptionOptions { Queue = " ", RoutingKeys = ["x.y"] };

        var act = () => CreateDirectly(options);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_WhenThereAreNoRoutingKeys_ThenItThrows()
    {
        // A queue bound to nothing would silently receive nothing.
        var options = new SubscriptionOptions { Queue = "homesecurity.test", RoutingKeys = [] };

        var act = () => CreateDirectly(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Built by hand, not by AutoFixture, which wraps a constructor's exception
    // and would hide the guard being asserted.
    private Subscription<RecordingHandler> CreateDirectly(SubscriptionOptions options) =>
        new(new Mock<IBus>().Object, provider.GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<Subscription<RecordingHandler>>.Instance);

    public sealed class HandlerBehaviour
    {
        public MessageOutcome Outcome { get; set; } = MessageOutcome.Acknowledge;

        public Exception? Failure { get; set; }

        public List<(string RoutingKey, byte[] Body, Guid ScopeId)> Received { get; } = [];
    }

    public sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class RecordingHandler : IMessageHandler
    {
        private readonly HandlerBehaviour behaviour;
        private readonly ScopeMarker marker;

        public RecordingHandler(HandlerBehaviour behaviour, ScopeMarker marker)
        {
            this.behaviour = behaviour;
            this.marker = marker;
        }

        public Task<MessageOutcome> HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            behaviour.Received.Add((routingKey, body.ToArray(), marker.Id));

            return behaviour.Failure is null
                ? Task.FromResult(behaviour.Outcome)
                : Task.FromException<MessageOutcome>(behaviour.Failure);
        }
    }
}
