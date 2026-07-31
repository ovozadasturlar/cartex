using Cartex.UI.Controls;
using Xunit;

namespace Cartex.UnitTests;

public sealed class PreviewZoomHostTests
{
    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.4, 1.4)]
    [InlineData(2.0, 2.0)]
    [InlineData(5.0, 2.0)]
    public void Zoom_IsClampedToUsablePreviewRange(double requested, double expected)
    {
        var host = new PreviewZoomHost
        {
            Zoom = requested
        };

        Assert.Equal(expected, host.Zoom);
    }
}
