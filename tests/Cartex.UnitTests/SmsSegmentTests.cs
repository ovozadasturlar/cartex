using Cartex.Infrastructure.Notifications.Sms;
using Xunit;

namespace Cartex.UnitTests;

public class SmsSegmentTests
{
    [Theory]
    [InlineData("Oddiy SMS", 1)]
    [InlineData("ўзбекча хабар", 1)]
    public void Short_messages_use_one_segment(string text, int expected) =>
        Assert.Equal(expected, SmsService.CountSegments(text));

    [Fact]
    public void Long_unicode_message_uses_67_character_segments() =>
        Assert.Equal(2, SmsService.CountSegments(new string('ў', 71)));

    [Fact]
    public void Gsm_extension_characters_count_as_two_septets() =>
        Assert.Equal(2, SmsService.CountSegments(new string('^', 81)));
}
