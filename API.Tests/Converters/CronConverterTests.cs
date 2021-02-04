using API.Helpers.Converters;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Converters
{
    public class CronConverterTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CronConverterTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData("daily", "0 0 * * *")]
        [InlineData("disabled", "0 0 31 2 *")]
        [InlineData("weekly", "0 0 * * 1")]
        public void ConvertTest(string input, string expected)
        {
            Assert.Equal(expected, CronConverter.ConvertToCronNotation(input));
        }
    }
}