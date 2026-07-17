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
    [ObservableProperty] private decimal _labelWidthMm = 40;
    [ObservableProperty] private decimal _labelHeightMm = 30;
    [ObservableProperty] private decimal _labelGapMm = 4;
    [ObservableProperty] private string _labelMode = "tspl";
    [ObservableProperty] private decimal _labelShiftXMm;
    [ObservableProperty] private decimal _labelShiftYMm;
    [ObservableProperty] private int _labelDpi = 203;
    [ObservableProperty] private int _labelRotation = 180;
    [ObservableProperty] private decimal _labelDensity = 8;
    [ObservableProperty] private decimal _labelSpeed = 4;
    [ObservableProperty] private string? _selectedLabelPreset;
    [ObservableProperty] private string _receiptMode = "thermal";
    [ObservableProperty] private string _receiptPaperWidth = "default";
    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _footerText = string.Empty;
    [ObservableProperty] private int _businessPaperWidth = 32;

    public bool IsThermal => ReceiptMode == "thermal";

    partial void OnReceiptModeChanged(string value) => OnPropertyChanged(nameof(IsThermal));

    public string[] LabelPresets { get; } = ["40×30", "58×40", "40×58", "58×60", "custom"];
    public string[] LabelModes { get; } = ["tspl", "pdf"];
    public int[] LabelDpis { get; } = [203, 300];
    public int[] LabelRotations { get; } = [0, 180];
    public string[] ReceiptModes { get; } = ["thermal", "a5", "a4"];
    public string[] ReceiptPaperWidths { get; } = ["default", "32", "42", "48"];
    public int[] BusinessPaperWidths { get; } = [32, 42, 48];
    public string[] SendFormats { get; } = ["Thermal", "A5", "A4"];
    [ObservableProperty] private string _sendFormat = "Thermal";

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

        var printers = await Task.Run(_printer.GetInstalledPrinters);
        Printers.Clear();
        foreach (var p in printers) Printers.Add(p);

        ReceiptPrinter = s.ReceiptPrinter;
        ZReportPrinter = s.ZReportPrinter;
        BarcodePrinter = s.BarcodePrinter;
        DocumentPrinter = s.DocumentPrinter;
        AutoPrintReceipt = s.AutoPrintReceipt;
        AutoPrintZReport = s.AutoPrintZReport;
        ReceiptCopies = Math.Clamp(s.ReceiptCopies, 1, 5);
        ReceiptMode = s.ReceiptMode is "a4" or "a5" ? s.ReceiptMode : "thermal";
        ReceiptPaperWidth = s.ReceiptPaperWidth is 32 or 42 or 48 ? s.ReceiptPaperWidth.ToString() : "default";
        var label = LabelSize.Resolve(s);
        LabelWidthMm = (decimal)label.WidthMm;
        LabelHeightMm = (decimal)label.HeightMm;
        LabelGapMm = (decimal)label.GapMm;
        LabelShiftXMm = (decimal)label.ShiftXMm;
        LabelShiftYMm = (decimal)label.ShiftYMm;
        LabelDpi = label.Dpi;
        LabelRotation = label.Rotation;
        LabelDensity = label.Density;
        LabelSpeed = label.Speed;
        LabelMode = s.LabelMode == "pdf" ? "pdf" : "tspl";
        SelectedLabelPreset = LabelPresets.FirstOrDefault(p => p == $"{label.WidthMm:0}×{label.HeightMm:0}") ?? "custom";

        try
        {
            var cfg = await _settingsApi.GetReceiptAsync();
            HeaderText = cfg.HeaderText ?? string.Empty;
            FooterText = cfg.FooterText ?? string.Empty;
            BusinessPaperWidth = cfg.PaperWidth is 42 or 48 ? cfg.PaperWidth : 32;
            SendFormat = SendFormats.Contains(cfg.PaperFormat) ? cfg.PaperFormat : "Thermal";
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
            AutoPrintZReport,
            LabelMode,
            (double)LabelGapMm,
            LabelDpi,
            (double)LabelShiftXMm,
            (double)LabelShiftYMm,
            LabelRotation,
            (int)LabelDensity,
            (int)LabelSpeed));
        try
        {
            await _settingsApi.UpdateReceiptAsync(new UpdateReceiptSettingsRequest(
                string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
                string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
                BusinessPaperWidth,
                SendFormat));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Receipt);
        _toast.Success(L["success"]);
    }

    [RelayCommand]
    private void TestPrint()
    {
        if (string.IsNullOrWhiteSpace(ReceiptPrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            _printer.PrintRaw(ReceiptPrinter, "Cartex\n  Test print\n\n\n");
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void TestZPrint()
    {
        if (string.IsNullOrWhiteSpace(ZReportPrinter) && string.IsNullOrWhiteSpace(ReceiptPrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            _printer.PrintZReport(new ZReportDto(0, 100_000, 1_250_000, 0, 50_000, 30_000, 200_000, 0, 1_570_000, 1_570_000, 0));
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void TestLabelPrint()
    {
        if (string.IsNullOrWhiteSpace(BarcodePrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            _labels.PrintLabels("4780000000000", "Sinov mahsulot", 1, BarcodePrinter);
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CalibrateLabel()
    {
        if (string.IsNullOrWhiteSpace(BarcodePrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            _printer.PrintRawBytes(BarcodePrinter, TsplLabel.BuildCalibration(LabelSize.Resolve(_printer.GetSettings())));
            _toast.Info(L["label_calibrate_started"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
