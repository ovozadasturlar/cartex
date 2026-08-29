using Cartex.UI.ViewModels;
using Xunit;

namespace Cartex.UnitTests;

public sealed class SmsSettingsPresentationTests
{
    [Fact]
    public void SMS_20_Allowed_numbers_are_normalized_added_once_and_removed()
    {
        var numbers = new SmsAllowedPhoneNumbers();

        Assert.True(numbers.Add("+998 (90) 123-45-67"));
        Assert.False(numbers.Add("998901234567"));
        Assert.Equal(["998901234567"], numbers.Items);

        numbers.Remove("998901234567");

        Assert.Empty(numbers.Items);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SMS_22_Sticky_wait_visibility_requires_multiple_devices(int deviceCount, bool expected)
    {
        Assert.Equal(expected, SmsGatewayViewModel.ShouldShowStickyWaits(deviceCount));
    }
}
