using Cartex.Application.Printing;
using Cartex.Application.Settings.Commands;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Settings;
using Xunit;

namespace Cartex.Application.Tests;

/// Chek kengligi endi qat'iy 32/42/48 ro'yxati emas: istalgan real termal kenglik
/// (24..64 belgi) qabul qilinadi, undan tashqarisi rad etiladi.
public sealed class ReceiptPaperWidthValidationTests
{
    [Theory]
    [InlineData(24, true)]
    [InlineData(32, true)]
    [InlineData(40, true)]
    [InlineData(48, true)]
    [InlineData(64, true)]
    [InlineData(10, false)]
    [InlineData(23, false)]
    [InlineData(65, false)]
    [InlineData(0, true)]
    public void ReceiptSettings_AcceptAnyRealThermalWidth(int width, bool valid)
    {
        var result = new UpdateReceiptSettingsCommandValidator()
            .Validate(new UpdateReceiptSettingsCommand(null, null, width));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("uz-latn", true)]
    [InlineData("uz-cyrl", true)]
    [InlineData("ru", true)]
    [InlineData("en", true)]
    [InlineData(null, true)]
    [InlineData("fr", false)]
    public void ReceiptSettings_LanguageMustBeSupported(string? language, bool valid)
    {
        var result = new UpdateReceiptSettingsCommandValidator()
            .Validate(new UpdateReceiptSettingsCommand(null, null, 32, Language: language));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(0, true)]
    [InlineData(23, false)]
    [InlineData(65, false)]
    public void ProformaSettings_AcceptAnyRealThermalWidth(int width, bool valid)
    {
        var result = new UpdateProformaSettingsCommandValidator()
            .Validate(new UpdateProformaSettingsCommand(null, null, width));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(0, true)]
    [InlineData(23, false)]
    [InlineData(65, false)]
    public void BranchReceiptOverride_AcceptAnyRealThermalWidth(int width, bool valid)
    {
        var settings = new ReceiptSettingsDto(null, null, width);
        var result = new UpdateReceiptPrintPolicyCommandValidator()
            .Validate(new UpdateReceiptPrintPolicyCommand(1,
                new UpdateReceiptPrintPolicyRequest(false, 1, true, settings)));

        Assert.Equal(valid, result.IsValid);
    }
}
