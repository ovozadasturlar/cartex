using Xunit;

namespace Cartex.ArchitectureTests;

/// Kundalik ish ekrani sozlamalardan ajratilgan bo'lishi kerak. Ikkala sahifa bir xil
/// ViewModel'ni ulashadi — sozlamalar uchun 370 qatorlik nusxa yaratish ortiqcha kod
/// bo'lardi; muhimi, operatsion ekranda sozlash boshqaruvlari bo'lmasligi.
public sealed class MobileSmsGatewayInformationArchitectureTests
{
    [Fact]
    public void SMS_15_Operational_screen_carries_no_configuration_controls()
    {
        var root = SolutionRoot.Find();
        var project = Path.Combine(root, "src", "mobile", "Cartex.Mobile.Store");
        var operationalView = File.ReadAllText(Path.Combine(project, "Views", "SmsGatewayPage.xaml"));
        var settingsView = File.ReadAllText(Path.Combine(project, "Views", "SmsGatewaySettingsPage.xaml"));

        Assert.Contains("x:DataType=\"vm:SmsGatewayViewModel\"", operationalView, StringComparison.Ordinal);
        Assert.Contains("OpenSettingsCommand", operationalView, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsUnregistered}\"", operationalView, StringComparison.Ordinal);
        Assert.Contains("MonthlyQuota", operationalView, StringComparison.Ordinal);
        Assert.Contains("RegisterCommand", operationalView, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSettingsCommand", operationalView, StringComparison.Ordinal);
        Assert.DoesNotContain("SendTestCommand", operationalView, StringComparison.Ordinal);
        Assert.DoesNotContain("TogglePauseCommand", operationalView, StringComparison.Ordinal);
        Assert.DoesNotContain("RevokeCommand", operationalView, StringComparison.Ordinal);
        Assert.Contains("x:DataType=\"vm:SmsGatewayViewModel\"", settingsView, StringComparison.Ordinal);
        Assert.Contains("MonthlyQuota", settingsView, StringComparison.Ordinal);
        Assert.Contains("QuotaResetDay", settingsView, StringComparison.Ordinal);
        Assert.Contains("SendTestCommand", settingsView, StringComparison.Ordinal);
        Assert.Contains("TogglePauseCommand", settingsView, StringComparison.Ordinal);
        Assert.Contains("RevokeCommand", settingsView, StringComparison.Ordinal);
    }
}
