using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class PrintingViewModel : ViewModelBase, ILoadable
{
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;
    private readonly ISettingsApi _settingsApi;
    private readonly IBarcodeLabelService _labels;

    public ObservableCollection<string> Printers { get; } = [];

    [ObservableProperty] private string _sectionKey = "receipt";
    public bool IsReceiptSection => SectionKey == "receipt";
    public bool IsBarcodeSection => SectionKey == "barcode";
    public bool IsZSection => SectionKey == "zreport";

    partial void OnSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsReceiptSection));
        OnPropertyChanged(nameof(IsBarcodeSection));
        OnPropertyChanged(nameof(IsZSection));
    }

    [RelayCommand]
    private void SelectSection(string key) => SectionKey = key;

    [ObservableProperty] private string? _receiptPrinter;
    [ObservableProperty] private string? _zReportPrinter;
    [ObservableProperty] private string? _barcodePrinter;
    [ObservableProperty] private string? _documentPrinter;
    [ObservableProperty] private bool _autoPrintReceipt;
    [ObservableProperty] private bool _autoPrintZReport;
    [ObservableProperty] private decimal _receiptCopies = 1;
    [ObservableProperty] private decimal _labelWidthMm = 58;
    [ObservableProperty] private decimal _labelHeightMm = 40;
    [ObservableProperty] private string? _selectedLabelPreset;
    [ObservableProperty] private string _receiptMode = "thermal";
    [ObservableProperty] private string _receiptPaperWidth = "default";
    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _footerText = string.Empty;
    [ObservableProperty] private int _businessPaperWidth = 32;

    public bool IsThermal => ReceiptMode == "thermal";

    partial void OnReceiptModeChanged(string value) => OnPropertyChanged(nameof(IsThermal));

    public string[] LabelPresets { get; } = ["58×40", "40×58", "58×60", "40×30", "custom"];
    public string[] ReceiptModes { get; } = ["thermal", "a5", "a4"];
    public string[] ReceiptPaperWidths { get; } = ["default", "32", "42", "48"];
    public int[] BusinessPaperWidths { get; } = [32, 42, 48];

    partial void OnSelectedLabelPresetChanged(string? value)
    {
        if (value is null || value == "custom") return;
        var parts = value.Split('×');
        LabelWidthMm = decimal.Parse(parts[0]);
        LabelHeightMm = decimal.Parse(parts[1]);
    }

    public PrintingViewModel(IPrinterService printer, IToastService toast, ISettingsApi settingsApi, IBarcodeLabelService labels)
    {
        _printer = printer;
        _toast = toast;
        _settingsApi = settingsApi;
        _labels = labels;
    }

    public async Task LoadAsync()
    {
        var s = _printer.GetSettings();
        ReceiptPrinter = s.ReceiptPrinter;
        ZReportPrinter = s.ZReportPrinter;
        BarcodePrinter = s.BarcodePrinter;
        DocumentPrinter = s.DocumentPrinter;
        AutoPrintReceipt = s.AutoPrintReceipt;
        AutoPrintZReport = s.AutoPrintZReport;
        ReceiptCopies = Math.Clamp(s.ReceiptCopies, 1, 5);
        ReceiptMode = s.ReceiptMode is "a4" or "a5" ? s.ReceiptMode : "thermal";
        ReceiptPaperWidth = s.ReceiptPaperWidth is 32 or 42 or 48 ? s.ReceiptPaperWidth.ToString() : "default";
        var (width, height) = LabelSize.Resolve(s.LabelWidthMm, s.LabelHeightMm);
        LabelWidthMm = (decimal)width;
        LabelHeightMm = (decimal)height;
        SelectedLabelPreset = LabelPresets.FirstOrDefault(p => p == $"{width:0}×{height:0}") ?? "custom";

        var printers = await Task.Run(_printer.GetInstalledPrinters);
        Printers.Clear();
        foreach (var p in printers) Printers.Add(p);
        OnPropertyChanged(nameof(ReceiptPrinter));
        OnPropertyChanged(nameof(ZReportPrinter));
        OnPropertyChanged(nameof(BarcodePrinter));
        OnPropertyChanged(nameof(DocumentPrinter));

        try
        {
            var cfg = await _settingsApi.GetReceiptAsync();
            HeaderText = cfg.HeaderText ?? string.Empty;
            FooterText = cfg.FooterText ?? string.Empty;
            BusinessPaperWidth = cfg.PaperWidth is 42 or 48 ? cfg.PaperWidth : 32;
        }
        catch { }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _printer.SaveSettings(new PrinterSettings(
            ReceiptPrinter,
            ZReportPrinter,
            BarcodePrinter,
            DocumentPrinter,
            AutoPrintReceipt,
            (double)LabelWidthMm,
            (double)LabelHeightMm,
            ReceiptMode,
            int.TryParse(ReceiptPaperWidth, out var width) ? width : 0,
            (int)Math.Clamp(ReceiptCopies, 1, 5),
            AutoPrintZReport));
        try
        {
            await _settingsApi.UpdateReceiptAsync(new UpdateReceiptSettingsRequest(
                string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
                string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
                BusinessPaperWidth));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        _toast.Success(L["success"]);
    }

    [RelayCommand]
    private void TestPrint()
    {
        if (string.IsNullOrWhiteSpace(ReceiptPrinter)) { _toast.Warning(L["error"]); return; }
        _printer.PrintRaw(ReceiptPrinter, "Cartex\n  Test print\n\n\n");
        _toast.Info(L["success"]);
    }

    [RelayCommand]
    private void TestZPrint()
    {
        if (string.IsNullOrWhiteSpace(ZReportPrinter) && string.IsNullOrWhiteSpace(ReceiptPrinter)) { _toast.Warning(L["error"]); return; }
        _printer.PrintZReport(new ZReportDto(0, 100_000, 1_250_000, 0, 50_000, 30_000, 200_000, 0, 1_570_000, 1_570_000, 0));
        _toast.Info(L["success"]);
    }

    [RelayCommand]
    private void TestLabelPrint()
    {
        if (string.IsNullOrWhiteSpace(BarcodePrinter)) { _toast.Warning(L["error"]); return; }
        _labels.PrintLabels("4780000000000", "Sinov mahsulot", 1, BarcodePrinter);
        _toast.Info(L["success"]);
    }
}
