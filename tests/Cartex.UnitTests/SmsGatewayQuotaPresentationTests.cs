using Cartex.UI.ViewModels;
using Xunit;

namespace Cartex.UnitTests;

public sealed class SmsGatewayQuotaPresentationTests
{
    [Theory]
    [InlineData(50, 100, SmsQuotaLevel.Success)]
    [InlineData(51, 100, SmsQuotaLevel.Warning)]
    [InlineData(75, 100, SmsQuotaLevel.Warning)]
    [InlineData(76, 100, SmsQuotaLevel.Orange)]
    [InlineData(91, 100, SmsQuotaLevel.Danger)]
    [InlineData(100, 100, SmsQuotaLevel.Exhausted)]
    [InlineData(120, 100, SmsQuotaLevel.Exhausted)]
    public void SMS_18_Remaining_quota_selects_expected_view_model_level(int used, int limit, SmsQuotaLevel expected)
    {
        Assert.Equal(expected, SmsGatewayDeviceRow.QuotaLevelFor(used, limit));
    }
}
