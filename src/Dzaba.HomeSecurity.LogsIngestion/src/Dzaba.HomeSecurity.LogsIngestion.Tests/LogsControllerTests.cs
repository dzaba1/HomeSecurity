using AutoFixture;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.LogsIngestion.Controllers;
using Dzaba.TestUtils;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests;

[TestFixture]
public class LogsControllerTests : ControllerUnitTestFixture
{
    private LogsController CreateSut()
    {
        return Fixture.Create<LogsController>();
    }

    [Test]
    public async Task Ingest_WhenLogs_ThenThoseArePropagated()
    {
        var eventPublisher = Fixture.FreezeMock<IEventPublisher>();
        var requestBody = Fixture.Create<IngestLogsRequest>();

        var sut = CreateSut();

        await sut.Ingest(requestBody).ConfigureAwait(false);

        foreach (var evt in requestBody.Events)
        {
            eventPublisher.Verify(p => p.PublishAsync(evt), Times.Once());
        }
    }
}
