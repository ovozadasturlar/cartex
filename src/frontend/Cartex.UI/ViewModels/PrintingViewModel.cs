using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class PrintingViewModel : ViewModelBase, ILoadable
{
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;

    public ObservableCollection<string> Printers { get; } = [];

    [ObservableProperty] private string? _receiptPrinter;
    [ObservableProperty] private string? _zReportPrinter;
    [ObservableProperty] private string? _barcodePrinter;
    [ObservableProperty] private string? _documentPrinter;
    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private bool _autoPrintReceipt;
    [ObservableProperty] private decimal _labelWidthMm = 58;
    [ObservableProperty] private decimal _labelHeightMm = 40;
    [ObservableProperty] private string? _selectedLabelPreset;

    public string[] LabelPresets { get; } = ["58×40", "40×58", "58×60", "40×30", "custom"];

    partial void OnSelectedLabelPresetChanged(string? value)
    {
        if (value is null || value == "custom") return;
        var parts = value.Split('×');
        LabelWidthMm = decimal.Parse(parts[0]);
        LabelHeightMm = decimal.Parse(parts[1]);
    }

    public PrintingViewModel(IPrinterService printer, IToastService toast)
    {
        _printer = printer;
        _toast = toast;
    }

    public Task LoadAsync()
    {
        Printers.Clear();
        foreach (var p in _printer.GetInstalledPrinters()) Printers.Add(p);

        var s = _printer.GetSettings();
        ReceiptPrinter = s.ReceiptPrinter;
        ZReportPrinter = s.ZReportPrinter;
        BarcodePrinter = s.BarcodePrinter;
        DocumentPrinter = s.DocumentPrinter;
        ServerUrl = s.ServerUrl ?? "";
        AutoPrintReceipt = s.AutoPrintReceipt;
        var (width, height) = LabelSize.Resolve(s.LabelWidthMm, s.LabelHeightMm);
        LabelWidthMm = (decimal)width;
        LabelHeightMm = (decimal)height;
        SelectedLabelPreset = LabelPresets.FirstOrDefault(p => p == $"{width:0}×{height:0}") ?? "custom";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Save()
    {
        _printer.SaveSettings(new PrinterSettings(
            ReceiptPrinter,
            ZReportPrinter,
            BarcodePrinter,
            DocumentPrinter,
            string.IsNullOrWhiteSpace(ServerUrl) ? null : ServerUrl.Trim(),
            AutoPrintReceipt,
            (double)LabelWidthMm,
            (double)LabelHeightMm));
        _toast.Success(L["success"]);
    }

    [RelayCommand]
    private void TestPrint()
    {
        if (string.IsNullOrWhiteSpace(ReceiptPrinter)) { _toast.Warning(L["error"]); return; }
        _printer.PrintRaw(ReceiptPrinter, "Cartex\n  Test print\n\n\n");
        _toast.Info(L["success"]);
    }
}
