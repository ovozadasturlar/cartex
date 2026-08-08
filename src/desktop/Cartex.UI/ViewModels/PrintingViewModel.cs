using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class PrintingViewModel : ViewModelBase, ILoadable
{
    private const decimal ShiftStepMm = 0.5m;
    private static readonly IBrush ColorInkBrush = Brush.Parse("#1E293B");
    private static readonly IBrush ColorMutedBrush = Brush.Parse("#64748B");
    private static readonly IBrush ColorLineBrush = Brush.Parse("#CBD5E1");
    private static readonly IBrush ColorFaintBrush = Brush.Parse("#F1F5F9");
    private static readonly IBrush ColorStatusBrush = Brush.Parse("#16A34A");
    private static readonly IBrush ColorStatusBackgroundBrush = Brush.Parse("#F0FDF4");
    private static readonly IBrush ColorAccentBrush = Brush.Parse("#2563EB");
    private static readonly IBrush MonochromeInkBrush = Brush.Parse("#111111");
    private static readonly IBrush MonochromeMutedBrush = Brush.Parse("#4B4B4B");
    private static readonly IBrush MonochromeLineBrush = Brush.Parse("#A3A3A3");
    private static readonly IBrush MonochromeFaintBrush = Brush.Parse("#EEEEEE");
    private static readonly IBrush MonochromeStatusBackgroundBrush = Brush.Parse("#F3F3F3");
    private static readonly ZReportDto PreviewZReport = new(
        1048, 100_000, 1_250_000, 25_000, 50_000, 30_000, 200_000, 75_000, 1_470_000, 1_470_000, 0)
    {
        CardSales = 480_000,
        BonusUsed = 35_000,
        NewDebtIssued = 90_000,
        SalesCount = 18
    };
    private bool _isLoadingLabelSettings;

    private sealed record PrinterCalibrationProfile(int Dpi, int Rotation, decimal Density, decimal Speed);

    private readonly IPrinterService _printer;
    private readonly IToastService _toast;
    private readonly ISettingsApi _settingsApi;
    private readonly IBusinessApi _businessApi;
    private readonly IBarcodeLabelService _labels;
    private readonly BranchContextService _branch;
    private readonly AuthService _auth;
    private readonly IPrintingApi _printingApi;
    private readonly IRatesApi _ratesApi;
    private readonly IReceiptApi _receiptApi;
    private readonly ISalesApi _salesApi;
    private List<CurrencyDto> _labelPreviewCurrencies =
    [
        new("UZS", "Uzbek so'mi", true, true, true, true, 1, DateTime.UtcNow, "so'm", "Suffix", 0),
        new("USD", "US Dollar", true, true, false, false, 12_500, DateTime.UtcNow, "$", "Prefix", 2)
    ];

    public bool CanEditReceiptContent => _auth.HasPermission("settings.receipt");
    public bool CanEditLabelContent => _auth.HasPermission("settings.barcodeLabel");

    public ObservableCollection<string> Printers { get; } = [];

    [ObservableProperty] private string _sectionKey = "receipt";
    public bool IsReceiptSection => SectionKey == "receipt";
    public bool IsBarcodeSection => SectionKey == "barcode";
    public bool IsZSection => SectionKey == "zreport";
    public bool IsNetworkSection => SectionKey == "network";

    partial void OnSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsReceiptSection));
        OnPropertyChanged(nameof(IsBarcodeSection));
        OnPropertyChanged(nameof(IsZSection));
        OnPropertyChanged(nameof(IsNetworkSection));
    }

    [RelayCommand]
    private void SelectSection(string key) => SectionKey = key;

    [RelayCommand]
    private void SelectReceiptMode(string mode) =>
        ReceiptMode = DocumentPrintLayout.ResolveOutputFormat(mode, DocumentPaperSize);

    [RelayCommand]
    private void SelectDocumentPaper(string paperSize)
    {
        DocumentPaperSize = paperSize == "a5" ? "a5" : "a4";
        if (IsDocument)
            ReceiptMode = DocumentPaperSize;
    }

    [RelayCommand]
    private void SelectDocumentOrientation(string orientation) =>
        DocumentOrientation = orientation == "landscape" ? "landscape" : "portrait";

    [RelayCommand]
    private void SelectPagesPerSheet(string value) =>
        DocumentPagesPerSheet = int.TryParse(value, out var pages) && pages is 2 or 4 ? pages : 1;

    [RelayCommand]
    private void SelectZReportMode(string mode) =>
        ZReportMode = DocumentPrintLayout.ResolveOutputFormat(mode, ZReportDocumentPaperSize);

    [RelayCommand]
    private void SelectZReportDocumentPaper(string paperSize)
    {
        ZReportDocumentPaperSize = paperSize == "a5" ? "a5" : "a4";
        if (IsZReportDocument)
            ZReportMode = ZReportDocumentPaperSize;
    }

    [RelayCommand]
    private void SelectZReportDocumentOrientation(string orientation) =>
        ZReportDocumentOrientation = orientation == "landscape" ? "landscape" : "portrait";

    [RelayCommand]
    private void SelectZReportPagesPerSheet(string value) =>
        ZReportDocumentPagesPerSheet = int.TryParse(value, out var pages) && pages is 2 or 4 ? pages : 1;

    [ObservableProperty] private string? _receiptPrinter;
    [ObservableProperty] private string? _zReportPrinter;
    [ObservableProperty] private string? _barcodePrinter;
    [ObservableProperty] private string? _documentPrinter;
    [ObservableProperty] private string? _pdfExportPath;
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
    [ObservableProperty] private bool _usePrinterGapCalibration;
    [ObservableProperty] private string _labelCurrencyDisplay = "symbol";
    [ObservableProperty] private string _labelCurrencyCase = "original";
    [ObservableProperty] private string _labelPriceCurrencyMode = "product";
    [ObservableProperty] private string _labelNameLines = "2";
    [ObservableProperty] private bool _labelDefaultWithPrice;
    [ObservableProperty] private bool _labelAllowPriceOverride = true;
    [ObservableProperty] private bool _labelShowSku;
    [ObservableProperty] private string? _selectedLabelPreset;
    [ObservableProperty] private string _receiptMode = "thermal";
    [ObservableProperty] private string _receiptPaperWidth = "default";
    [ObservableProperty] private string _documentPaperSize = "a4";
    [ObservableProperty] private string _documentOrientation = "portrait";
    [ObservableProperty] private int _documentPagesPerSheet = 1;
    [ObservableProperty] private string _zReportMode = "thermal";
    [ObservableProperty] private string _zReportPaperWidth = "default";
    [ObservableProperty] private string _zReportDocumentPaperSize = "a4";
    [ObservableProperty] private string _zReportDocumentOrientation = "portrait";
    [ObservableProperty] private int _zReportDocumentPagesPerSheet = 1;
    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _footerText = string.Empty;
    [ObservableProperty] private int _businessPaperWidth = 32;
    [ObservableProperty] private bool _showBusinessName = true;
    [ObservableProperty] private bool _showBranchName = true;
    [ObservableProperty] private bool _showAddress = true;
    [ObservableProperty] private bool _showPhone = true;
    [ObservableProperty] private bool _showCashier = true;
    [ObservableProperty] private bool _showCustomer = true;
    [ObservableProperty] private bool _showReceiptNumber = true;
    [ObservableProperty] private bool _showPaymentDetails = true;
    [ObservableProperty] private bool _showQrCode = true;
    [ObservableProperty] private bool _showElectronicLink = true;
    [ObservableProperty] private bool _showLogo = true;
    [ObservableProperty] private bool _showCustomerPhone = true;
    [ObservableProperty] private bool _showCustomerEmail;
    [ObservableProperty] private string? _publicReceiptBaseUrl;
    [ObservableProperty] private string _previewBusinessName = string.Empty;
    [ObservableProperty] private string _previewBranchName = string.Empty;
    [ObservableProperty] private string _previewAddress = string.Empty;
    [ObservableProperty] private string _previewPhone = string.Empty;
    [ObservableProperty] private string _previewCashierName = "Akmal";
    [ObservableProperty] private bool _documentPrinterSupportsColor;
    [ObservableProperty] private bool _zReportPrinterSupportsColor;
    [ObservableProperty] private Bitmap? _labelPreview;
    [ObservableProperty] private bool _labelPreviewMayClip;

    public bool IsThermal => ReceiptMode == "thermal";
    public bool IsDocument => !IsThermal;
    public bool HasPublicReceiptBaseUrl => !string.IsNullOrWhiteSpace(PublicReceiptBaseUrl);
    public bool CanPreviewQrCode => ShowQrCode && HasPublicReceiptBaseUrl;
    public bool CanPreviewElectronicLink => ShowElectronicLink && HasPublicReceiptBaseUrl;
    public string PreviewElectronicReceiptLink => HasPublicReceiptBaseUrl
        ? $"{PublicReceiptBaseUrl!.TrimEnd('/')}/r/1048"
        : string.Empty;
    public bool IsDocumentPaperA4 => DocumentPaperSize == "a4";
    public bool IsDocumentPaperA5 => DocumentPaperSize == "a5";
    public bool IsPortrait => DocumentOrientation == "portrait";
    public bool IsLandscape => DocumentOrientation == "landscape";
    public bool IsOnePagePerSheet => DocumentPagesPerSheet == 1;
    public bool IsTwoPagesPerSheet => DocumentPagesPerSheet == 2;
    public bool IsFourPagesPerSheet => DocumentPagesPerSheet == 4;
    public bool IsTwoPagesPortrait => IsTwoPagesPerSheet && IsPortrait;
    public bool IsTwoPagesLandscape => IsTwoPagesPerSheet && IsLandscape;
    public bool IsReceiptRenderLandscape =>
        DocumentPrintLayout.GetReceiptOrientation(DocumentOrientation, DocumentPagesPerSheet) == "landscape";
    public bool IsPreviewColor => IsDocument && DocumentPrinterSupportsColor;
    public bool IsPreviewMonochrome => !IsPreviewColor;
    public string PreviewPrinterName =>
        string.IsNullOrWhiteSpace(IsThermal ? ReceiptPrinter : DocumentPrinter)
            ? L["printer_not_set"]
            : (IsThermal ? ReceiptPrinter : DocumentPrinter)!;
    public IBrush PreviewInkBrush => IsPreviewColor ? ColorInkBrush : MonochromeInkBrush;
    public IBrush PreviewMutedBrush => IsPreviewColor ? ColorMutedBrush : MonochromeMutedBrush;
    public IBrush PreviewLineBrush => IsPreviewColor ? ColorLineBrush : MonochromeLineBrush;
    public IBrush PreviewFaintBrush => IsPreviewColor ? ColorFaintBrush : MonochromeFaintBrush;
    public IBrush PreviewStatusBrush => IsPreviewColor ? ColorStatusBrush : MonochromeInkBrush;
    public IBrush PreviewStatusBackgroundBrush =>
        IsPreviewColor ? ColorStatusBackgroundBrush : MonochromeStatusBackgroundBrush;
    public double PreviewReceiptPageWidth => IsReceiptRenderLandscape ? 424 : 300;
    public double PreviewReceiptPageHeight => IsReceiptRenderLandscape ? 300 : 424;
    public Thickness PreviewReceiptPagePadding => new(28.5);
    public double PreviewReceiptOnSheetWidth => GetPreviewReceiptOnSheetSize().Width;
    public double PreviewReceiptOnSheetHeight => GetPreviewReceiptOnSheetSize().Height;
    public bool IsZReportThermal => ZReportMode == "thermal";
    public bool IsZReportDocument => !IsZReportThermal;
    public bool IsZReportDocumentPaperA4 => ZReportDocumentPaperSize == "a4";
    public bool IsZReportDocumentPaperA5 => ZReportDocumentPaperSize == "a5";
    public bool IsZReportPortrait => ZReportDocumentOrientation == "portrait";
    public bool IsZReportLandscape => ZReportDocumentOrientation == "landscape";
    public bool IsZReportOnePagePerSheet => ZReportDocumentPagesPerSheet == 1;
    public bool IsZReportTwoPagesPerSheet => ZReportDocumentPagesPerSheet == 2;
    public bool IsZReportFourPagesPerSheet => ZReportDocumentPagesPerSheet == 4;
    public bool IsZReportTwoPagesPortrait => IsZReportTwoPagesPerSheet && IsZReportPortrait;
    public bool IsZReportTwoPagesLandscape => IsZReportTwoPagesPerSheet && IsZReportLandscape;
    public bool IsZReportRenderLandscape =>
        DocumentPrintLayout.GetReceiptOrientation(
            ZReportDocumentOrientation,
            ZReportDocumentPagesPerSheet) == "landscape";
    public bool IsZReportPreviewColor => IsZReportDocument && ZReportPrinterSupportsColor;
    public bool IsZReportPreviewMonochrome => !IsZReportPreviewColor;
    public IBrush ZReportPreviewInkBrush => IsZReportPreviewColor ? ColorInkBrush : MonochromeInkBrush;
    public IBrush ZReportPreviewAccentBrush =>
        IsZReportPreviewColor ? ColorAccentBrush : MonochromeInkBrush;
    public IBrush ZReportPreviewMutedBrush =>
        IsZReportPreviewColor ? ColorMutedBrush : MonochromeMutedBrush;
    public IBrush ZReportPreviewLineBrush =>
        IsZReportPreviewColor ? ColorLineBrush : MonochromeLineBrush;
    public IBrush ZReportPreviewFaintBrush =>
        IsZReportPreviewColor ? ColorFaintBrush : MonochromeFaintBrush;
    public IBrush ZReportPreviewStatusBrush =>
        IsZReportPreviewColor ? ColorStatusBrush : MonochromeInkBrush;
    public IBrush ZReportPreviewStatusBackgroundBrush =>
        IsZReportPreviewColor ? ColorStatusBackgroundBrush : MonochromeStatusBackgroundBrush;
    public double LabelPreviewPaperWidth
    {
        get
        {
            var scale = Math.Min(
                340d / Math.Max(20, (double)LabelWidthMm),
                300d / Math.Max(20, (double)LabelHeightMm));
            return (double)LabelWidthMm * scale;
        }
    }
    public double LabelPreviewPaperHeight
    {
        get
        {
            var scale = Math.Min(
                340d / Math.Max(20, (double)LabelWidthMm),
                300d / Math.Max(20, (double)LabelHeightMm));
            return (double)LabelHeightMm * scale;
        }
    }
    public string ZReportPreviewText
    {
        get
        {
            var width = int.TryParse(ZReportPaperWidth, out var parsed) ? parsed : 32;
            return _printer.FormatZReport(PreviewZReport, width);
        }
    }
    public string ZReportPreviewBodyText
    {
        get
        {
            var lines = ZReportPreviewText
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n');
            var firstSeparator = Array.FindIndex(
                lines,
                line => line.Length >= 10 && line.All(character => character == '-'));
            return string.Join(
                Environment.NewLine,
                firstSeparator >= 0 ? lines.Skip(firstSeparator) : lines);
        }
    }
    public string ZReportPreviewDate => DateTime.Now.ToString("dd.MM.yyyy HH:mm");
    public string ZReportPreviewShiftNumber => $"#{PreviewZReport.ShiftId}";
    public string ZReportPreviewPeriod =>
        $"{DateTime.Today.AddHours(8):dd.MM.yyyy HH:mm} — {DateTime.Now:dd.MM.yyyy HH:mm}";
    public string ZReportPreviewSalesAmount => $"{PreviewZReport.CashSales + PreviewZReport.CardSales:N0}";
    public string ZReportPreviewExpectedCash => $"{PreviewZReport.ExpectedCash:N0}";
    public string ZReportPreviewCountedCash => $"{PreviewZReport.CountedCash:N0}";
    public string ZReportPreviewDifference => $"{PreviewZReport.Difference:N0}";
    public string ZReportPreviewCashSales => $"{PreviewZReport.CashSales:N0}";
    public string ZReportPreviewCardSales => $"{PreviewZReport.CardSales:N0}";
    public string ZReportPreviewBonusUsed => $"{PreviewZReport.BonusUsed:N0}";
    public string ZReportPreviewDebtIssued => $"{PreviewZReport.NewDebtIssued:N0}";
    public string ZReportPreviewCashReturns => $"-{PreviewZReport.CashReturns:N0}";
    public string ZReportPreviewCardReturns => $"-{PreviewZReport.CardReturns:N0}";
    public string ZReportPreviewSalesCount => $"{PreviewZReport.SalesCount:N0}";
    public string ZReportPreviewOpeningFloat => $"{PreviewZReport.OpeningFloat:N0}";
    public string ZReportPreviewPayIn => $"{PreviewZReport.PayIn:N0}";
    public string ZReportPreviewPayOut => $"-{PreviewZReport.PayOut:N0}";
    public string ZReportPreviewDebtPayIn => $"{PreviewZReport.DebtPayIn:N0}";
    public string ZReportPreviewSupplyPayOut => $"-{PreviewZReport.SupplyPayOut:N0}";
    public double ZReportPreviewPaperWidth => IsZReportThermal
        ? ZReportPaperWidth switch
        {
            "48" => 340,
            "42" => 300,
            _ => 240
        }
        : IsZReportLandscape
            ? IsZReportDocumentPaperA5 ? 380 : 430
            : IsZReportDocumentPaperA5 ? 270 : 310;
    public double ZReportPreviewPaperHeight => IsZReportThermal
        ? 520
        : IsZReportLandscape
            ? IsZReportDocumentPaperA5 ? 270 : 304
            : IsZReportDocumentPaperA5 ? 380 : 438;
    public double ZReportContentPageWidth => IsZReportRenderLandscape ? 424 : 300;
    public double ZReportContentPageHeight => IsZReportRenderLandscape ? 300 : 424;
    public double ZReportOnSheetWidth => GetZReportOnSheetSize().Width;
    public double ZReportOnSheetHeight => GetZReportOnSheetSize().Height;
    public string ZReportPreviewPrinterName =>
        !string.IsNullOrWhiteSpace(ZReportPrinter)
            ? ZReportPrinter
            : !string.IsNullOrWhiteSpace(IsZReportThermal ? ReceiptPrinter : DocumentPrinter)
                ? (IsZReportThermal ? ReceiptPrinter : DocumentPrinter)!
                : L["printer_not_set"];
    public double PreviewPaperWidth => IsThermal
        ? 250
        : IsLandscape
            ? IsDocumentPaperA5 ? 380 : 430
            : IsDocumentPaperA5 ? 270 : 310;
    public double PreviewPaperHeight => IsThermal
        ? 520
        : IsLandscape
            ? IsDocumentPaperA5 ? 270 : 304
            : IsDocumentPaperA5 ? 380 : 438;
    public string PreviewFooterText =>
        string.IsNullOrWhiteSpace(FooterText) ? L["receipt_footer_example"] : FooterText;

    partial void OnReceiptModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsThermal));
        OnPropertyChanged(nameof(IsDocument));
        NotifyPreviewColorChanged();
        NotifyPreviewLayoutChanged();
    }

    partial void OnReceiptPrinterChanged(string? value)
    {
        if (IsThermal)
            OnPropertyChanged(nameof(PreviewPrinterName));
        if (IsZReportThermal)
            RefreshZReportPrinterPreview();
    }

    partial void OnZReportPrinterChanged(string? value) => RefreshZReportPrinterPreview();

    partial void OnDocumentPrinterChanged(string? value)
    {
        DocumentPrinterSupportsColor = _printer.GetPrinterCapabilities(value).SupportsColor;
        if (IsDocument)
            OnPropertyChanged(nameof(PreviewPrinterName));
        if (IsZReportDocument && string.IsNullOrWhiteSpace(ZReportPrinter))
            RefreshZReportPrinterPreview();
    }

    partial void OnDocumentPrinterSupportsColorChanged(bool value) => NotifyPreviewColorChanged();

    partial void OnPublicReceiptBaseUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPublicReceiptBaseUrl));
        OnPropertyChanged(nameof(CanPreviewQrCode));
        OnPropertyChanged(nameof(CanPreviewElectronicLink));
        OnPropertyChanged(nameof(PreviewElectronicReceiptLink));
    }

    partial void OnShowQrCodeChanged(bool value) => OnPropertyChanged(nameof(CanPreviewQrCode));
    partial void OnShowElectronicLinkChanged(bool value) => OnPropertyChanged(nameof(CanPreviewElectronicLink));
    partial void OnFooterTextChanged(string value) => OnPropertyChanged(nameof(PreviewFooterText));

    partial void OnDocumentPaperSizeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDocumentPaperA4));
        OnPropertyChanged(nameof(IsDocumentPaperA5));
        if (IsDocument && ReceiptMode != value)
            ReceiptMode = value;
        NotifyPreviewLayoutChanged();
    }

    partial void OnDocumentOrientationChanged(string value)
    {
        OnPropertyChanged(nameof(IsPortrait));
        OnPropertyChanged(nameof(IsLandscape));
        NotifyPreviewLayoutChanged();
    }

    partial void OnDocumentPagesPerSheetChanged(int value) => NotifyPreviewLayoutChanged();

    partial void OnZReportModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsZReportThermal));
        OnPropertyChanged(nameof(IsZReportDocument));
        RefreshZReportPrinterPreview();
        NotifyZReportPreviewChanged();
    }

    partial void OnZReportPaperWidthChanged(string value) => NotifyZReportPreviewChanged();

    partial void OnZReportDocumentPaperSizeChanged(string value)
    {
        OnPropertyChanged(nameof(IsZReportDocumentPaperA4));
        OnPropertyChanged(nameof(IsZReportDocumentPaperA5));
        if (IsZReportDocument && ZReportMode != value)
            ZReportMode = value;
        NotifyZReportPreviewChanged();
    }

    partial void OnZReportDocumentOrientationChanged(string value)
    {
        OnPropertyChanged(nameof(IsZReportPortrait));
        OnPropertyChanged(nameof(IsZReportLandscape));
        NotifyZReportPreviewChanged();
    }

    partial void OnZReportDocumentPagesPerSheetChanged(int value) => NotifyZReportPreviewChanged();

    partial void OnZReportPrinterSupportsColorChanged(bool value)
        => NotifyZReportColorChanged();

    private void NotifyZReportColorChanged()
    {
        OnPropertyChanged(nameof(IsZReportPreviewColor));
        OnPropertyChanged(nameof(IsZReportPreviewMonochrome));
        OnPropertyChanged(nameof(ZReportPreviewInkBrush));
        OnPropertyChanged(nameof(ZReportPreviewAccentBrush));
        OnPropertyChanged(nameof(ZReportPreviewMutedBrush));
        OnPropertyChanged(nameof(ZReportPreviewLineBrush));
        OnPropertyChanged(nameof(ZReportPreviewFaintBrush));
        OnPropertyChanged(nameof(ZReportPreviewStatusBrush));
        OnPropertyChanged(nameof(ZReportPreviewStatusBackgroundBrush));
    }

    private void NotifyPreviewLayoutChanged()
    {
        OnPropertyChanged(nameof(IsOnePagePerSheet));
        OnPropertyChanged(nameof(IsTwoPagesPerSheet));
        OnPropertyChanged(nameof(IsFourPagesPerSheet));
        OnPropertyChanged(nameof(IsTwoPagesPortrait));
        OnPropertyChanged(nameof(IsTwoPagesLandscape));
        OnPropertyChanged(nameof(IsReceiptRenderLandscape));
        OnPropertyChanged(nameof(PreviewPaperWidth));
        OnPropertyChanged(nameof(PreviewPaperHeight));
        OnPropertyChanged(nameof(PreviewReceiptPageWidth));
        OnPropertyChanged(nameof(PreviewReceiptPageHeight));
        OnPropertyChanged(nameof(PreviewReceiptPagePadding));
        OnPropertyChanged(nameof(PreviewReceiptOnSheetWidth));
        OnPropertyChanged(nameof(PreviewReceiptOnSheetHeight));
    }

    private PrintPageSize GetPreviewReceiptOnSheetSize() =>
        DocumentPrintLayout.GetPreviewPageSize(
            DocumentPaperSize,
            ReceiptMode,
            DocumentOrientation,
            DocumentPagesPerSheet,
            PreviewPaperWidth,
            PreviewPaperHeight);

    private PrintPageSize GetZReportOnSheetSize() =>
        DocumentPrintLayout.GetPreviewPageSize(
            ZReportDocumentPaperSize,
            ZReportMode,
            ZReportDocumentOrientation,
            ZReportDocumentPagesPerSheet,
            ZReportPreviewPaperWidth,
            ZReportPreviewPaperHeight);

    private void NotifyPreviewColorChanged()
    {
        OnPropertyChanged(nameof(IsPreviewColor));
        OnPropertyChanged(nameof(IsPreviewMonochrome));
        OnPropertyChanged(nameof(PreviewPrinterName));
        OnPropertyChanged(nameof(PreviewInkBrush));
        OnPropertyChanged(nameof(PreviewMutedBrush));
        OnPropertyChanged(nameof(PreviewLineBrush));
        OnPropertyChanged(nameof(PreviewFaintBrush));
        OnPropertyChanged(nameof(PreviewStatusBrush));
        OnPropertyChanged(nameof(PreviewStatusBackgroundBrush));
    }

    public string[] LabelPresets { get; } = ["40×30", "58×40", "40×58", "58×60", "custom"];
    public string[] LabelModes { get; } = ["tspl", "pdf"];
    public int[] LabelDpis { get; } = [203, 300];
    public int[] LabelRotations { get; } = [0, 180];
    public string[] LabelCurrencyDisplays { get; } = ["symbol", "code"];
    public string[] LabelCurrencyCases { get; } = ["original", "upper", "lower"];
    public string[] LabelPriceCurrencyModes { get; } = ["product", "default"];
    public string[] LabelNameLineOptions { get; } = ["1", "2", "all"];
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

    partial void OnLabelWidthMmChanged(decimal value)
    {
        UseManualGapWhenLabelChanges();
        RefreshLabelPreview();
    }

    partial void OnLabelHeightMmChanged(decimal value)
    {
        UseManualGapWhenLabelChanges();
        RefreshLabelPreview();
    }

    partial void OnLabelGapMmChanged(decimal value) => UseManualGapWhenLabelChanges();
    partial void OnLabelShiftXMmChanged(decimal value) => RefreshLabelPreview();
    partial void OnLabelShiftYMmChanged(decimal value) => RefreshLabelPreview();
    partial void OnLabelDpiChanged(int value) => RefreshLabelPreview();
    partial void OnLabelRotationChanged(int value) => RefreshLabelPreview();
    partial void OnLabelCurrencyDisplayChanged(string value) => RefreshLabelPreview();
    partial void OnLabelCurrencyCaseChanged(string value) => RefreshLabelPreview();
    partial void OnLabelPriceCurrencyModeChanged(string value) => RefreshLabelPreview();
    partial void OnLabelNameLinesChanged(string value) => RefreshLabelPreview();
    partial void OnLabelDefaultWithPriceChanged(bool value) => RefreshLabelPreview();
    partial void OnLabelShowSkuChanged(bool value) => RefreshLabelPreview();
    private void NotifyZReportPreviewChanged()
    {
        OnPropertyChanged(nameof(IsZReportOnePagePerSheet));
        OnPropertyChanged(nameof(IsZReportTwoPagesPerSheet));
        OnPropertyChanged(nameof(IsZReportFourPagesPerSheet));
        OnPropertyChanged(nameof(IsZReportTwoPagesPortrait));
        OnPropertyChanged(nameof(IsZReportTwoPagesLandscape));
        OnPropertyChanged(nameof(IsZReportRenderLandscape));
        OnPropertyChanged(nameof(ZReportPreviewText));
        OnPropertyChanged(nameof(ZReportPreviewBodyText));
        OnPropertyChanged(nameof(ZReportPreviewDate));
        OnPropertyChanged(nameof(ZReportPreviewPaperWidth));
        OnPropertyChanged(nameof(ZReportPreviewPaperHeight));
        OnPropertyChanged(nameof(ZReportContentPageWidth));
        OnPropertyChanged(nameof(ZReportContentPageHeight));
        OnPropertyChanged(nameof(ZReportOnSheetWidth));
        OnPropertyChanged(nameof(ZReportOnSheetHeight));
    }

    private void RefreshZReportPrinterPreview()
    {
        var printerName = !string.IsNullOrWhiteSpace(ZReportPrinter)
            ? ZReportPrinter
            : IsZReportThermal
                ? ReceiptPrinter
                : DocumentPrinter;
        ZReportPrinterSupportsColor = IsZReportDocument
            && _printer.GetPrinterCapabilities(printerName).SupportsColor;
        OnPropertyChanged(nameof(ZReportPreviewPrinterName));
        NotifyZReportColorChanged();
    }

    private void UseManualGapWhenLabelChanges()
    {
        if (!_isLoadingLabelSettings)
            UsePrinterGapCalibration = false;
    }

    private void RefreshLabelPreview()
    {
        if (_isLoadingLabelSettings)
            return;

        try
        {
            var options = new LabelOptions(
                (double)LabelWidthMm,
                (double)LabelHeightMm,
                (double)LabelGapMm,
                LabelDpi,
                (double)LabelShiftXMm,
                (double)LabelShiftYMm,
                LabelRotation,
                (int)LabelDensity,
                (int)LabelSpeed,
                UsePrinterGapCalibration,
                int.TryParse(LabelNameLines, out var nameLines) ? nameLines : 0,
                LabelShowSku);
            var templateSettings = new PrinterSettings
            {
                LabelCurrencyDisplay = LabelCurrencyDisplay,
                LabelCurrencyCase = LabelCurrencyCase,
                LabelPriceCurrencyMode = LabelPriceCurrencyMode
            };
            var sampleCurrency = _labelPreviewCurrencies.First(currency => !currency.IsBase && currency.Rate is > 0);
            var result = _labels.RenderLabelPreview(
                "4780000123456",
                L["receipt_preview_item_one"],
                LabelDefaultWithPrice
                    ? BarcodeLabelFormatting.FormatProductPrice(
                        12.5m,
                        sampleCurrency.Code,
                        sampleCurrency.Symbol,
                        sampleCurrency.SymbolPosition,
                        sampleCurrency.DecimalDigits,
                        _labelPreviewCurrencies,
                        templateSettings)
                    : null,
                options,
                "CTX-001");
            using var stream = new MemoryStream(result.Image);
            var preview = new Bitmap(stream);
            var previous = LabelPreview;
            LabelPreview = preview;
            previous?.Dispose();
            LabelPreviewMayClip = result.MayClip;
            OnPropertyChanged(nameof(LabelPreviewPaperWidth));
            OnPropertyChanged(nameof(LabelPreviewPaperHeight));
        }
        catch
        {
            var previous = LabelPreview;
            LabelPreview = null;
            previous?.Dispose();
            LabelPreviewMayClip = false;
        }
    }

    public PrintingViewModel(
        IPrinterService printer,
        IToastService toast,
        ISettingsApi settingsApi,
        IBusinessApi businessApi,
        IBarcodeLabelService labels,
        BranchContextService branch,
        AuthService auth,
        IPrintingApi printingApi,
        IRatesApi ratesApi,
        IReceiptApi receiptApi,
        ISalesApi salesApi)
    {
        _printer = printer;
        _toast = toast;
        _settingsApi = settingsApi;
        _businessApi = businessApi;
        _labels = labels;
        _branch = branch;
        _auth = auth;
        _printingApi = printingApi;
        _ratesApi = ratesApi;
        _receiptApi = receiptApi;
        _salesApi = salesApi;
    }

    public async Task LoadAsync()
    {
        var s = _printer.GetSettings();

        var printers = await Task.Run(_printer.GetInstalledPrinters);
        Printers.Clear();
        foreach (var p in printers) Printers.Add(p);
        await LoadLabelPreviewCurrenciesAsync();

        _isLoadingLabelSettings = true;
        try
        {
            ReceiptPrinter = s.ReceiptPrinter;
            ZReportPrinter = s.ZReportPrinter;
            BarcodePrinter = s.BarcodePrinter;
            DocumentPrinter = s.DocumentPrinter;
            PdfExportPath = s.PdfExportPath;
            AutoPrintReceipt = s.AutoPrintReceipt;
            AutoPrintZReport = s.AutoPrintZReport;
            ReceiptCopies = Math.Clamp(s.ReceiptCopies, 1, 5);
            ReceiptMode = s.ReceiptMode is "a4" or "a5" ? s.ReceiptMode : "thermal";
            ReceiptPaperWidth = s.ReceiptPaperWidth is 32 or 42 or 48 ? s.ReceiptPaperWidth.ToString() : "default";
            HeaderText = s.ReceiptHeaderText ?? string.Empty;
            FooterText = s.ReceiptFooterText ?? string.Empty;
            BusinessPaperWidth = s.ReceiptContentWidth is 42 or 48 ? s.ReceiptContentWidth : 32;
            ShowBusinessName = s.ReceiptShowBusinessName;
            ShowBranchName = s.ReceiptShowBranchName;
            ShowAddress = s.ReceiptShowAddress;
            ShowPhone = s.ReceiptShowPhone;
            ShowCashier = s.ReceiptShowCashier;
            ShowCustomer = s.ReceiptShowCustomer;
            ShowReceiptNumber = s.ReceiptShowNumber;
            ShowPaymentDetails = s.ReceiptShowPaymentDetails;
            ShowQrCode = s.ReceiptShowQrCode;
            ShowElectronicLink = s.ReceiptShowElectronicLink;
            PublicReceiptBaseUrl = s.ReceiptPublicBaseUrl;
            DocumentPaperSize = s.DocumentPaperSize == "a5" ? "a5" : "a4";
            DocumentOrientation = s.DocumentOrientation == "landscape" ? "landscape" : "portrait";
            DocumentPagesPerSheet = s.DocumentPagesPerSheet is 2 or 4 ? s.DocumentPagesPerSheet : 1;
            ZReportMode = s.ZReportMode is "a4" or "a5" ? s.ZReportMode : "thermal";
            ZReportPaperWidth = s.ZReportPaperWidth is 32 or 42 or 48
                ? s.ZReportPaperWidth.ToString()
                : ReceiptPaperWidth;
            ZReportDocumentPaperSize = s.ZReportDocumentPaperSize is "a4" or "a5"
                ? s.ZReportDocumentPaperSize
                : DocumentPaperSize;
            ZReportDocumentOrientation = s.ZReportDocumentOrientation == "landscape"
                ? "landscape"
                : "portrait";
            ZReportDocumentPagesPerSheet = s.ZReportDocumentPagesPerSheet is 2 or 4
                ? s.ZReportDocumentPagesPerSheet
                : 1;
            if (ReceiptMode != "thermal")
                ReceiptMode = DocumentPaperSize;
            if (ZReportMode != "thermal")
                ZReportMode = ZReportDocumentPaperSize;
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
            UsePrinterGapCalibration = label.UsePrinterGapCalibration;
            LabelCurrencyDisplay = s.LabelCurrencyDisplay == "code" ? "code" : "symbol";
            LabelCurrencyCase = s.LabelCurrencyCase is "upper" or "lower" ? s.LabelCurrencyCase : "original";
            LabelPriceCurrencyMode = s.LabelPriceCurrencyMode == "default" ? "default" : "product";
            LabelNameLines = s.LabelNameLines is 1 or 2 ? s.LabelNameLines.ToString() : "all";
            LabelDefaultWithPrice = s.LabelDefaultWithPrice;
            LabelAllowPriceOverride = s.LabelAllowPriceOverride;
            LabelShowSku = s.LabelShowSku;
            SelectedLabelPreset = LabelPresets.FirstOrDefault(p => p == $"{label.WidthMm:0}×{label.HeightMm:0}") ?? "custom";
        }
        finally
        {
            _isLoadingLabelSettings = false;
        }
        RefreshZReportPrinterPreview();
        NotifyZReportPreviewChanged();
        RefreshLabelPreview();

        try
        {
            var labelSettings = await _settingsApi.GetBarcodeLabelAsync();
            LabelDefaultWithPrice = labelSettings.DefaultWithPrice;
            LabelAllowPriceOverride = labelSettings.AllowPriceOverride;
            LabelShowSku = labelSettings.ShowSku;
            LabelNameLines = labelSettings.NameLines is 1 or 2 ? labelSettings.NameLines.ToString() : "all";
            LabelCurrencyDisplay = labelSettings.CurrencyDisplay == "code" ? "code" : "symbol";
            LabelCurrencyCase = labelSettings.CurrencyCase is "upper" or "lower" ? labelSettings.CurrencyCase : "original";
            LabelPriceCurrencyMode = labelSettings.PriceCurrencyMode == "default" ? "default" : "product";
            SaveLocalPrinterSettings();
            RefreshLabelPreview();
        }
        catch { }

        try
        {
            var cfg = await _settingsApi.GetReceiptAsync();
            HeaderText = cfg.HeaderText ?? string.Empty;
            FooterText = cfg.FooterText ?? string.Empty;
            BusinessPaperWidth = cfg.PaperWidth is 42 or 48 ? cfg.PaperWidth : 32;
            SendFormat = SendFormats.Contains(cfg.PaperFormat) ? cfg.PaperFormat : "Thermal";
            ShowBusinessName = cfg.ShowBusinessName;
            ShowBranchName = cfg.ShowBranchName;
            ShowAddress = cfg.ShowAddress;
            ShowPhone = cfg.ShowPhone;
            ShowCashier = cfg.ShowCashier;
            ShowCustomer = cfg.ShowCustomer;
            ShowReceiptNumber = cfg.ShowReceiptNumber;
            ShowPaymentDetails = cfg.ShowPaymentDetails;
            ShowQrCode = cfg.ShowQrCode;
            ShowElectronicLink = cfg.ShowElectronicLink;
            ShowLogo = cfg.ShowLogo;
            ShowCustomerPhone = cfg.ShowCustomerPhone;
            ShowCustomerEmail = cfg.ShowCustomerEmail;
            PublicReceiptBaseUrl = cfg.PublicReceiptBaseUrl;
        }
        catch { }

        _printer.ReceiptOptions = new ReceiptPrintOptions(
            string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            BusinessPaperWidth,
            ShowBusinessName,
            ShowBranchName,
            ShowAddress,
            ShowPhone,
            ShowCashier,
            ShowCustomer,
            ShowReceiptNumber,
            ShowPaymentDetails,
            ShowQrCode,
            ShowElectronicLink,
            PublicReceiptBaseUrl,
            ShowLogo,
            ShowCustomerPhone,
            ShowCustomerEmail);
        SaveLocalPrinterSettings();

        var currentBranch = _branch.SelectedBranch;
        PreviewCashierName = _auth.UserInfo?.FullName
            ?? _auth.UserInfo?.Username
            ?? "Akmal";
        PreviewBranchName = currentBranch?.Name ?? L["receipt_preview_branch"];
        PreviewAddress = currentBranch?.Address ?? L["receipt_preview_address"];
        PreviewPhone = currentBranch?.Phone ?? "+998 90 123 45 67";
        try
        {
            var business = await _businessApi.GetAsync();
            PreviewBusinessName = business.Name;
            PreviewAddress = currentBranch?.Address ?? business.Address ?? PreviewAddress;
            PreviewPhone = currentBranch?.Phone ?? business.Phone ?? PreviewPhone;
        }
        catch
        {
            PreviewBusinessName = L["receipt_preview_business"];
        }

        await LoadNetworkPrintingAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        SaveLocalPrinterSettings();
        if (CanEditLabelContent)
        try
        {
            await _settingsApi.UpdateBarcodeLabelAsync(new UpdateBarcodeLabelSettingsRequest(
                LabelDefaultWithPrice,
                LabelAllowPriceOverride,
                LabelShowSku,
                int.TryParse(LabelNameLines, out var labelNameLines) ? labelNameLines : 0,
                LabelCurrencyDisplay,
                LabelCurrencyCase,
                LabelPriceCurrencyMode));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        if (CanEditReceiptContent)
        try
        {
            await _settingsApi.UpdateReceiptAsync(new UpdateReceiptSettingsRequest(
                string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
                string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
                BusinessPaperWidth,
                SendFormat,
                ShowBusinessName,
                ShowBranchName,
                ShowAddress,
                ShowPhone,
                ShowCashier,
                ShowCustomer,
                ShowReceiptNumber,
                ShowPaymentDetails,
                ShowQrCode,
                ShowElectronicLink,
                ShowLogo,
                ShowCustomerPhone,
                ShowCustomerEmail));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        _printer.ReceiptOptions = new ReceiptPrintOptions(
            string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            BusinessPaperWidth,
            ShowBusinessName,
            ShowBranchName,
            ShowAddress,
            ShowPhone,
            ShowCashier,
            ShowCustomer,
            ShowReceiptNumber,
            ShowPaymentDetails,
            ShowQrCode,
            ShowElectronicLink,
            PublicReceiptBaseUrl,
            ShowLogo,
            ShowCustomerPhone,
            ShowCustomerEmail);
        ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Receipt);
        _toast.Success(L["success"]);
    }

    [RelayCommand]
    private async Task SelectPdfExportPathAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var top = lifetime?.MainWindow;
        if (top == null) return;

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = L["select_pdf_folder"],
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            PdfExportPath = folders[0].Path.LocalPath;
            SaveLocalPrinterSettings();
        }
    }
    [RelayCommand]
    private void ClearPdfExportPath()
    {
        PdfExportPath = string.Empty;
        SaveLocalPrinterSettings();
    }

    [RelayCommand]
    private async Task TestPrint()
    {
        var targetPrinter = IsThermal ? ReceiptPrinter : DocumentPrinter;
        if (string.IsNullOrWhiteSpace(targetPrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            SaveLocalPrinterSettings();
            var isPdf = targetPrinter.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase) ||
                        targetPrinter.Contains("Save to PDF", StringComparison.OrdinalIgnoreCase) ||
                        targetPrinter.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
                        targetPrinter.Contains("OneNote", StringComparison.OrdinalIgnoreCase);

            if (IsDocument || isPdf)
            {
                var sales = await _salesApi.GetAllAsync(
                    fromDate: DateTime.Today.AddDays(-30),
                    toDate:   DateTime.Today.AddDays(1));
                var token = sales
                    .OrderByDescending(s => s.SaleDate)
                    .Select(s => s.ReceiptToken)
                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                if (string.IsNullOrWhiteSpace(token))
                {
                    _toast.Warning("PDF test uchun kamida 1 ta savdo tarixi kerak.");
                    return;
                }
                var physicalPaper     = ReceiptMode is "a4" or "a5" ? ReceiptMode : "a4";
                var documentFormat    = DocumentPrintLayout.ResolveOutputFormat("document", physicalPaper);
                var renderOrientation = DocumentPrintLayout.GetReceiptOrientation(
                    DocumentOrientation is "landscape" ? "landscape" : "portrait",
                    DocumentPagesPerSheet);
                var content = await _receiptApi.GetPrintImagesAsync(token, documentFormat, renderOrientation);
                await using var package = await content.ReadAsStreamAsync();
                using var archive = new System.IO.Compression.ZipArchive(package, System.IO.Compression.ZipArchiveMode.Read);
                var pages = new List<byte[]>(archive.Entries.Count);
                foreach (var entry in archive.Entries.OrderBy(x => x.FullName, StringComparer.Ordinal))
                {
                    await using var input = entry.Open();
                    using var output = new MemoryStream();
                    await input.CopyToAsync(output);
                    pages.Add(output.ToArray());
                }
                _printer.PrintDocumentImages(pages, targetPrinter, (int)ReceiptCopies, PdfExportPath);
            }
            else
            {
                _printer.PrintReceipt(CreatePreviewReceipt(), targetPrinter, (int)ReceiptCopies);
            }
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void TestZPrint()
    {
        var targetPrinter = !string.IsNullOrWhiteSpace(ZReportPrinter)
            ? ZReportPrinter
            : IsZReportThermal
                ? ReceiptPrinter
                : DocumentPrinter;
        if (string.IsNullOrWhiteSpace(targetPrinter)) { _toast.Warning(L["error"]); return; }
        try
        {
            SaveLocalPrinterSettings();
            _printer.PrintZReport(PreviewZReport);
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
            SaveLocalPrinterSettings();
            var settings = _printer.GetSettings();
            var sampleCurrency = _labelPreviewCurrencies.First(currency => !currency.IsBase && currency.Rate is > 0);
            var price = settings.LabelDefaultWithPrice
                ? BarcodeLabelFormatting.FormatProductPrice(
                    12.5m,
                    sampleCurrency.Code,
                    sampleCurrency.Symbol,
                    sampleCurrency.SymbolPosition,
                    sampleCurrency.DecimalDigits,
                    _labelPreviewCurrencies,
                    settings)
                : null;
            _labels.PrintLabels("4780000000000", "Sinov mahsulot", 1, BarcodePrinter, price, "CTX-001");
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CalibrateLabel()
    {
        if (string.IsNullOrWhiteSpace(BarcodePrinter)) { _toast.Warning(L["error"]); return; }

        ApplyPrinterCalibrationProfile();
        if (string.Equals(LabelMode, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            _toast.Warning(L["label_calibrate_tspl_only"]);
            return;
        }
        try
        {
            UsePrinterGapCalibration = true;
            SaveLocalPrinterSettings();
            _printer.PrintRawBytes(BarcodePrinter, TsplLabel.BuildCalibration(LabelSize.Resolve(_printer.GetSettings())));
            _toast.Info(L["label_calibrate_started"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadLabelPreviewCurrenciesAsync()
    {
        try
        {
            var currencies = await _ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            if (currencies.Any(currency => currency.IsBase)
                && currencies.Any(currency => !currency.IsBase && currency.Rate is > 0))
                _labelPreviewCurrencies = currencies
                    .Where(currency => currency.IsBase || currency.Rate is > 0)
                    .ToList();
        }
        catch
        {
        }
    }

    private ReceiptDto CreatePreviewReceipt()
    {
        var items = new List<ReceiptItemDto>
        {
            new(L["receipt_preview_item_one"], 2, L["unit"], 12_500, 25_000),
            new(L["receipt_preview_item_two"], 1, L["unit"], 8_000, 8_000),
            new(L["receipt_preview_item_three"], 1.5m, L["unit"], 14_000, 21_000),
            new(L["receipt_preview_item_four"], 2, L["unit"], 9_500, 19_000),
            new(L["receipt_preview_item_five"], 1, L["unit"], 11_000, 11_000)
        };
        return new ReceiptDto(
            "preview-1048",
            PreviewBusinessName,
            PreviewBranchName,
            PreviewAddress,
            PreviewPhone,
            DateTime.Now,
            84_000,
            0,
            84_000,
            0,
            0,
            0,
            0,
            0,
            PreviewCashierName,
            items,
            [new ReceiptPaymentDto("Cash", "UZS", 84_000, 1, 84_000)],
            1048,
            "Dilshod",
            LocalizationManager.Instance.CurrentLanguage switch
            {
                AppLanguage.Ru => "ru",
                AppLanguage.UzCyrl => "uz-cyrl",
                AppLanguage.En => "en",
                _ => "uz-latn"
            });
    }

    private void ApplyPrinterCalibrationProfile()
    {
        var name = BarcodePrinter;
        if (string.IsNullOrWhiteSpace(name)) return;

        PrinterCalibrationProfile? profile = name.Contains("GP-3120TUD", StringComparison.OrdinalIgnoreCase)
            ? new PrinterCalibrationProfile(Dpi: 203, Rotation: 180, Density: 8, Speed: 3)
            : null;

        if (profile is null) return;

        LabelMode = "tspl";
        LabelDpi = profile.Dpi;
        LabelRotation = profile.Rotation;
        LabelDensity = profile.Density;
        LabelSpeed = profile.Speed;
        LabelShiftXMm = 0;
        LabelShiftYMm = 0;
    }

    [RelayCommand]
    private void NudgeLabelLeft() => LabelShiftXMm = Math.Clamp(LabelShiftXMm + ShiftStepMm, -10, 10);

    [RelayCommand]
    private void NudgeLabelRight() => LabelShiftXMm = Math.Clamp(LabelShiftXMm - ShiftStepMm, -10, 10);

    [RelayCommand]
    private void NudgeLabelUp() => LabelShiftYMm = Math.Clamp(LabelShiftYMm + ShiftStepMm, -10, 10);

    [RelayCommand]
    private void NudgeLabelDown() => LabelShiftYMm = Math.Clamp(LabelShiftYMm - ShiftStepMm, -10, 10);

    [RelayCommand]
    private void UseManualLabelGap()
    {
        UsePrinterGapCalibration = false;
        SaveLocalPrinterSettings();
    }

    private void SaveLocalPrinterSettings()
    {
        _printer.SaveSettings(new PrinterSettings
        {
            ReceiptPrinter = ReceiptPrinter,
            ZReportPrinter = ZReportPrinter,
            BarcodePrinter = BarcodePrinter,
            DocumentPrinter = DocumentPrinter,
            PdfExportPath = PdfExportPath,
            AutoPrintReceipt = AutoPrintReceipt,
            LabelWidthMm = (double)LabelWidthMm,
            LabelHeightMm = (double)LabelHeightMm,
            ReceiptMode = ReceiptMode,
            ReceiptPaperWidth = int.TryParse(ReceiptPaperWidth, out var width) ? width : 0,
            ReceiptCopies = (int)Math.Clamp(ReceiptCopies, 1, 5),
            ReceiptHeaderText = string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            ReceiptFooterText = string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            ReceiptContentWidth = BusinessPaperWidth,
            ReceiptShowBusinessName = ShowBusinessName,
            ReceiptShowBranchName = ShowBranchName,
            ReceiptShowAddress = ShowAddress,
            ReceiptShowPhone = ShowPhone,
            ReceiptShowCashier = ShowCashier,
            ReceiptShowCustomer = ShowCustomer,
            ReceiptShowNumber = ShowReceiptNumber,
            ReceiptShowPaymentDetails = ShowPaymentDetails,
            ReceiptShowQrCode = ShowQrCode,
            ReceiptShowElectronicLink = ShowElectronicLink,
            ReceiptPublicBaseUrl = PublicReceiptBaseUrl,
            AutoPrintZReport = AutoPrintZReport,
            LabelMode = LabelMode,
            LabelGapMm = (double)LabelGapMm,
            LabelDpi = LabelDpi,
            LabelShiftXMm = (double)LabelShiftXMm,
            LabelShiftYMm = (double)LabelShiftYMm,
            LabelRotation = LabelRotation,
            LabelDensity = (int)LabelDensity,
            LabelSpeed = (int)LabelSpeed,
            UsePrinterGapCalibration = UsePrinterGapCalibration,
            LabelCurrencyDisplay = LabelCurrencyDisplay,
            LabelCurrencyCase = LabelCurrencyCase,
            LabelPriceCurrencyMode = LabelPriceCurrencyMode,
            LabelNameLines = int.TryParse(LabelNameLines, out var nameLines) ? nameLines : 0,
            LabelDefaultWithPrice = LabelDefaultWithPrice,
            LabelAllowPriceOverride = LabelAllowPriceOverride,
            LabelShowSku = LabelShowSku,
            DocumentPaperSize = DocumentPaperSize,
            DocumentOrientation = DocumentOrientation,
            DocumentPagesPerSheet = DocumentPagesPerSheet,
            ZReportMode = ZReportMode,
            ZReportPaperWidth = int.TryParse(ZReportPaperWidth, out var zWidth) ? zWidth : 0,
            ZReportDocumentPaperSize = ZReportDocumentPaperSize,
            ZReportDocumentOrientation = ZReportDocumentOrientation,
            ZReportDocumentPagesPerSheet = ZReportDocumentPagesPerSheet
        });
    }
}
