using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class TestDataTest : BaseWorkerLogicIntegrationTest
    {
        public TestDataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public void IsNotOverwritingTestData()
        {
            Assert.False(OverwriteTestData);
        }
    }
}
