using Dzaba.TestUtils;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests;

public abstract class ControllerUnitTestFixture : AutoFixtureTestFixture
{
    [SetUp]
    public void SetupMvc()
    {
        Fixture.Customize<BindingInfo>(c => c.OmitAutoProperties());
    }
}
