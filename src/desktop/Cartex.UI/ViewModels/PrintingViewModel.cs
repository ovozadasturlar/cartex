using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Localization;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Printing;
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
    private bool _isLoadingReceiptSettings;

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
    private readonly PrintPolicyCache _printPolicyCache;
    private readonly PrintLogoCache _logoCache;
    private List<CurrencyDto> _labelPreviewCurrencies =
    [
        new("UZS", "Uzbek so'mi", true, true, true, true, 1, DateTime.UtcNow, "so'm", "Suffix", 0),
        new("USD", "US Dollar", true, true, false, false, 12_500, DateTime.UtcNow, "$", "Prefix", 2)
    ];

    public bool CanEditReceiptContent => _auth.HasPermission("settings.receipt");
    public bool CanEditLabelContent => _auth.HasPermission("settings.barcodeLabel");
    public bool CanEditAutoPrint => _auth.HasPermission("printing.routes.edit") && RemotePrintingActive;

    /// RUXSAT-04: wildcard foydalanuvchining ruxsatlari modul o'chiq bo'lsa ham qolaveradi,
    /// server esa yopiq — shuning uchun server bilan ishlaydigan har bir yo'l modul holatini
    /// alohida so'raydi.
    private static bool RemotePrintingActive => SettingsService.Instance.IsFeatureOn("remote_printing");

    public ObservableCollection<string> Printers { get; } = [];

    [ObservableProperty] private string _sectionKey = "receipt";
    public bool IsReceiptSection => SectionKey == "receipt";
    public bool IsCartSection => SectionKey == "cart";
    public bool IsBarcodeSection => SectionKey == "barcode";
    public bool IsZSection => SectionKey == "zreport";
    public bool IsNetworkSection => SectionKey == "network";

    partial void OnSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsReceiptSection));
        OnPropertyChanged(nameof(IsCartSection));
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
    private void SelectCartMode(string mode) => CartMode = mode switch
    {
        "a4" or "a5" => mode,
        "document" => IsCartDocument ? CartMode : "a4",
        _ => "thermal"
    };

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
    [ObservableProperty] private string _receiptPaperWidth = "auto";
    [ObservableProperty] private string _cartMode = "thermal";
    [ObservableProperty] private string _cartPaperWidth = "32";
    [ObservableProperty] private string _cartHeaderText = string.Empty;
    [ObservableProperty] private string _cartFooterText = string.Empty;
    [ObservableProperty] private bool _cartShowBusinessName = true;
    [ObservableProperty] private bool _cartShowAddress = true;
    [ObservableProperty] private bool _cartShowPhone = true;
    [ObservableProperty] private bool _cartShowSeller = true;
    [ObservableProperty] private bool _cartShowCustomer = true;
    [ObservableProperty] private bool _cartShowNote = true;
    [ObservableProperty] private bool _cartShowCartCode = true;
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
    [ObservableProperty] private string _businessPaperWidth = "32";
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
    [ObservableProperty] private bool _useBranchReceiptOverride;
    [ObservableProperty] private bool _printingSettingsOffline;
    [ObservableProperty] private DateTime? _printingLastSyncedAt;
    [ObservableProperty] private string? _publicReceiptBaseUrl;
    [ObservableProperty] private string _previewBusinessName = string.Empty;
    [ObservableProperty] private string _previewBranchName = string.Empty;
    [ObservableProperty] private string _previewAddress = string.Empty;
    [ObservableProperty] private string _previewPhone = string.Empty;
    [ObservableProperty] private string _previewCashierName = "Akmal";
    [ObservableProperty] private IReadOnlyList<ReceiptPreviewLine> _receiptPreviewBeforeQr = [];
    [ObservableProperty] private IReadOnlyList<ReceiptPreviewLine> _receiptPreviewAfterQr = [];
    [ObservableProperty] private bool _documentPrinterSupportsColor;
    [ObservableProperty] private bool _zReportPrinterSupportsColor;
    [ObservableProperty] private Bitmap? _labelPreview;
    [ObservableProperty] private bool _labelPreviewMayClip;

    public bool IsThermal => ReceiptMode == "thermal";
    public bool IsDocument => !IsThermal;
    public bool IsPdfReceiptOutput => IsDocument && IsPdfPrinter(DocumentPrinter);
    public bool IsCartThermal => CartMode == "thermal";
    public bool IsCartDocument => !IsCartThermal;
    public bool IsCartPaperA4 => CartMode == "a4";
    public bool IsCartPaperA5 => CartMode == "a5";
    public double CartPreviewPaperWidth => IsCartThermal ? 250 : IsCartPaperA5 ? 270 : 310;
    public double CartPreviewPaperHeight => IsCartThermal ? 470 : IsCartPaperA5 ? 380 : 438;
    private ProformaPrintOptions CartOptions() => new(
        null, null, CartWidthSetting, IsCartThermal ? "Thermal" : IsCartPaperA5 ? "A5" : "A4");
    public string CartPreviewPrinterName => _printer.ProformaTarget(CartOptions()).Printer ?? L["printer_not_set"];
    public bool IsCartFallbackActive
    {
        get
        {
            var target = _printer.ProformaTarget(CartOptions());
            return target.Printer is not null && target.IsDocument == IsCartThermal;
        }
    }
    public string CartFallbackText => string.Format(L["cart_fallback_hint"], CartPreviewPrinterName);
    public bool IsCartPreviewColor => IsCartDocument && DocumentPrinterSupportsColor;
    public bool IsCartPreviewMonochrome => !IsCartPreviewColor;
    public IBrush CartPreviewInkBrush => IsCartPreviewColor ? ColorInkBrush : MonochromeInkBrush;
    public IBrush CartPreviewMutedBrush => IsCartPreviewColor ? ColorMutedBrush : MonochromeMutedBrush;
    public IBrush CartPreviewLineBrush => IsCartPreviewColor ? ColorLineBrush : MonochromeLineBrush;
    public IBrush CartPreviewFaintBrush => IsCartPreviewColor ? ColorFaintBrush : MonochromeFaintBrush;
    public IBrush CartPreviewAccentBrush => IsCartPreviewColor ? ColorAccentBrush : MonochromeInkBrush;
    public string CartPreviewHeaderText =>
        string.IsNullOrWhiteSpace(CartHeaderText) ? HeaderText : CartHeaderText;
    public string CartPreviewFooterText =>
        string.IsNullOrWhiteSpace(CartFooterText)
            ? string.IsNullOrWhiteSpace(FooterText) ? L["receipt_footer_example"] : FooterText
            : CartFooterText;
    public string PrintingSyncText => PrintingSettingsOffline
        ? PrintingLastSyncedAt is { } cached
            ? $"Offline · oxirgi sinxronizatsiya {cached.ToLocalTime():dd.MM HH:mm}"
            : "Offline · qurilmadagi sozlamalar"
        : PrintingLastSyncedAt is { } synced
            ? $"Server bilan sinxron · {synced.ToLocalTime():HH:mm}"
            : "Server bilan sinxron";
    public bool HasPublicReceiptBaseUrl => !string.IsNullOrWhiteSpace(PublicReceiptBaseUrl);
    public bool CanPreviewQrCode => ShowQrCode && HasPublicReceiptBaseUrl;
    public bool CanPreviewElectronicLink => ShowElectronicLink && HasPublicReceiptBaseUrl;
    public bool IsForcedUzbekCyrillicText =>
        ReceiptLanguageOption?.Key == "uz-cyrl" && ReceiptPrintModeOption?.Key == "text";
    public bool IsNarrowTableTemplate =>
        ReceiptTemplateOption?.Key == "table" && EffectiveReceiptWidth <= 42;
    public bool IsReceiptGraphicOutput
    {
        get
        {
            var document = PrinterService.FormatReceiptDocument(CreatePreviewReceipt(), PreviewReceiptOptions());
            return NeedsGraphicForTextSize || EscPos.ResolveOutputMode(
                ReceiptPrintModeOption?.Key,
                document.Text,
                ReceiptCharsetOption,
                ReceiptFoldCyrillic) == "graphic";
        }
    }
    public string PreviewElectronicReceiptLink => HasPublicReceiptBaseUrl
        ? $"{PublicReceiptBaseUrl!.TrimEnd('/')}/r/1048"
        : string.Empty;
    public string PreviewCustomerPhone => "+998 90 555 12 34";
    public string PreviewCustomerEmail => "dilshod@example.uz";
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
        ? 240 + (ReceiptPaper.Sanitize(int.TryParse(ZReportPaperWidth, out var w) ? w : 0) - 32) * 6.25
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
    /// Preview varag'i belgi soniga qarab kengayadi: 48 belgili jadval shabloni 32 belgi
    /// uchun o'lchangan kenglikka sig'may, oxirgi ustunni kesib qo'yardi.
    public double ReceiptPreviewSheetWidth => NominalReceiptWidth * 5.2 + 40;

    public double PreviewPaperWidth => IsThermal
        ? 250 + (NominalReceiptWidth - 32) * 5
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

    /// Qog'ozning o'z kengligi: yozuv o'lchami belgilar sonini kamaytiradi, qog'ozni emas.
    private int NominalReceiptWidth =>
        _printer.DriverReceiptPaper(ReceiptPrinter)?.Columns ?? BusinessWidth;

    private bool NeedsGraphicForTextSize =>
        ReceiptPrintModeOption?.Key != "text"
        && ReceiptPaper.ApplyTextSize(48, ReceiptTextSizeOption?.Key) != 48;

    private int EffectiveReceiptWidth =>
        int.TryParse(ReceiptPaperWidth, out var width) && ReceiptPaper.IsValid(width)
            ? width
            : ReceiptPaper.Sanitize(NeedsGraphicForTextSize
                ? ReceiptPaper.ApplyTextSize(NominalReceiptWidth, ReceiptTextSizeOption?.Key)
                : NominalReceiptWidth / ReceiptPaper.TextMagnification(ReceiptTextSizeOption?.Key));

    partial void OnReceiptModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsThermal));
        OnPropertyChanged(nameof(IsDocument));
        NotifyPreviewColorChanged();
        NotifyPreviewLayoutChanged();
        OnPropertyChanged(nameof(IsPdfReceiptOutput));
    }

    partial void OnCartModeChanged(string value) => NotifyCartPreviewChanged();
    partial void OnCartHeaderTextChanged(string value) => OnPropertyChanged(nameof(CartPreviewHeaderText));
    partial void OnCartFooterTextChanged(string value) => OnPropertyChanged(nameof(CartPreviewFooterText));
    private void NotifyCartPreviewChanged()
    {
        OnPropertyChanged(nameof(IsCartThermal));
        OnPropertyChanged(nameof(IsCartDocument));
        OnPropertyChanged(nameof(IsCartPaperA4));
        OnPropertyChanged(nameof(IsCartPaperA5));
        OnPropertyChanged(nameof(CartPreviewPaperWidth));
        OnPropertyChanged(nameof(CartPreviewPaperHeight));
        OnPropertyChanged(nameof(CartPreviewPrinterName));
        OnPropertyChanged(nameof(IsCartFallbackActive));
        OnPropertyChanged(nameof(CartFallbackText));
        OnPropertyChanged(nameof(IsCartPreviewColor));
        OnPropertyChanged(nameof(IsCartPreviewMonochrome));
        OnPropertyChanged(nameof(CartPreviewInkBrush));
        OnPropertyChanged(nameof(CartPreviewMutedBrush));
        OnPropertyChanged(nameof(CartPreviewLineBrush));
        OnPropertyChanged(nameof(CartPreviewFaintBrush));
        OnPropertyChanged(nameof(CartPreviewAccentBrush));
    }

    partial void OnReceiptPrinterChanged(string? value)
    {
        OnPropertyChanged(nameof(AutoWidthText));
        OnPropertyChanged(nameof(ZReportAutoWidthText));
        if (IsThermal)
            OnPropertyChanged(nameof(PreviewPrinterName));
        if (IsZReportThermal)
            RefreshZReportPrinterPreview();
        OnPropertyChanged(nameof(CartPreviewPrinterName));
        RefreshReceiptPreview();
    }

    partial void OnZReportPrinterChanged(string? value)
    {
        OnPropertyChanged(nameof(ZReportAutoWidthText));
        RefreshZReportPrinterPreview();
    }

    partial void OnDocumentPrinterChanged(string? value)
    {
        DocumentPrinterSupportsColor = _printer.GetPrinterCapabilities(value).SupportsColor;
        if (IsDocument)
            OnPropertyChanged(nameof(PreviewPrinterName));
        if (IsZReportDocument && string.IsNullOrWhiteSpace(ZReportPrinter))
            RefreshZReportPrinterPreview();
        OnPropertyChanged(nameof(IsPdfReceiptOutput));
        NotifyCartPreviewChanged();
    }

    partial void OnPrintingSettingsOfflineChanged(bool value) => OnPropertyChanged(nameof(PrintingSyncText));
    partial void OnPrintingLastSyncedAtChanged(DateTime? value) => OnPropertyChanged(nameof(PrintingSyncText));

    partial void OnDocumentPrinterSupportsColorChanged(bool value) => NotifyPreviewColorChanged();

    partial void OnPublicReceiptBaseUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPublicReceiptBaseUrl));
        OnPropertyChanged(nameof(CanPreviewQrCode));
        OnPropertyChanged(nameof(CanPreviewElectronicLink));
        OnPropertyChanged(nameof(PreviewElectronicReceiptLink));
        RefreshReceiptPreview();
    }

    partial void OnShowQrCodeChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPreviewQrCode));
        RefreshReceiptPreview();
    }

    partial void OnShowElectronicLinkChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPreviewElectronicLink));
        RefreshReceiptPreview();
    }

    partial void OnFooterTextChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewFooterText));
        OnPropertyChanged(nameof(CartPreviewFooterText));
        RefreshReceiptPreview();
    }

    partial void OnHeaderTextChanged(string value)
    {
        OnPropertyChanged(nameof(CartPreviewHeaderText));
        RefreshReceiptPreview();
    }

    partial void OnReceiptPaperWidthChanged(string value) => RefreshReceiptPreview();
    partial void OnShowBusinessNameChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowBranchNameChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowAddressChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowPhoneChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowCashierChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowCustomerChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowReceiptNumberChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowPaymentDetailsChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowLogoChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowCustomerPhoneChanged(bool value) => RefreshReceiptPreview();
    partial void OnShowCustomerEmailChanged(bool value) => RefreshReceiptPreview();
    partial void OnPreviewBusinessNameChanged(string value) => RefreshReceiptPreview();
    partial void OnPreviewBranchNameChanged(string value) => RefreshReceiptPreview();
    partial void OnPreviewAddressChanged(string value) => RefreshReceiptPreview();
    partial void OnPreviewPhoneChanged(string value) => RefreshReceiptPreview();
    partial void OnPreviewCashierNameChanged(string value) => RefreshReceiptPreview();

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
    public string[] ReceiptPaperWidths { get; } = ["auto", .. ReceiptPaper.CommonWidths.Select(w => w.ToString())];
    public string[] BusinessPaperWidths { get; } = ["auto", .. ReceiptPaper.CommonWidths.Select(w => w.ToString())];
    public string[] CartPaperWidths { get; } = ["auto", .. ReceiptPaper.CommonWidths.Select(w => w.ToString())];
    public string[] SendFormats { get; } = ["Thermal", "A5", "A4"];
    [ObservableProperty] private string _sendFormat = "Thermal";
    public IReadOnlyList<EscPosCharset> CharsetOptions { get; } = EscPos.Charsets;
    public IReadOnlyList<PrintChoice> CutModeOptions { get; }
    public IReadOnlyList<PrintChoice> ReceiptPrintModeOptions { get; }
    public IReadOnlyList<PrintChoice> ReceiptTemplateOptions { get; }
    public IReadOnlyList<PrintChoice> ReceiptDarknessOptions { get; }
    public IReadOnlyList<PrintChoice> ReceiptTextSizeOptions { get; }
    public IReadOnlyList<PrintChoice> ReceiptQualityOptions { get; }
    [ObservableProperty] private EscPosCharset _receiptCharsetOption = EscPos.Charsets[0];
    [ObservableProperty] private PrintChoice? _receiptCutModeOption;
    [ObservableProperty] private PrintChoice? _receiptPrintModeOption;
    [ObservableProperty] private PrintChoice? _receiptDarknessOption;
    [ObservableProperty] private PrintChoice? _receiptTextSizeOption;
    [ObservableProperty] private PrintChoice? _receiptQualityOption;
    [ObservableProperty] private bool _receiptFoldCyrillic;
    [ObservableProperty] private PrintChoice? _receiptTemplateOption;
    [ObservableProperty] private decimal _receiptCodeTable;
    [ObservableProperty] private decimal _receiptRasterWidthDots;
    [ObservableProperty] private decimal _receiptFeedBeforeCut = 4;

    public bool IsCodeTableVisible => ReceiptCharsetOption.CodePage != 65001;

    public IReadOnlyList<PrintChoice> ReceiptLanguageOptions { get; } =
    [
        new PrintChoice("uz-latn", "O'zbek (lotin)"),
        new PrintChoice("uz-cyrl", "Ўзбек (кирилл)"),
        new PrintChoice("ru", "Русский"),
        new PrintChoice("en", "English")
    ];
    public IReadOnlyList<PrintChoice> ZReportLanguageOptions { get; }
    [ObservableProperty] private PrintChoice? _receiptLanguageOption;
    [ObservableProperty] private PrintChoice? _zReportLanguageOption;

    partial void OnReceiptCharsetOptionChanged(EscPosCharset value)
    {
        ReceiptCodeTable = Math.Max(0, value.CodeTable);
        OnPropertyChanged(nameof(IsCodeTableVisible));
        RefreshReceiptPreview();
    }

    partial void OnReceiptLanguageOptionChanged(PrintChoice? value)
    {
        if (!_isLoadingReceiptSettings && value is not null)
            ApplyLanguageCharset(value.Key);
        RefreshReceiptPreview();
    }

    partial void OnReceiptPrintModeOptionChanged(PrintChoice? value) => RefreshReceiptPreview();
    partial void OnReceiptTemplateOptionChanged(PrintChoice? value) => RefreshReceiptPreview();
    partial void OnReceiptTextSizeOptionChanged(PrintChoice? value) => RefreshReceiptPreview();
    partial void OnReceiptQualityOptionChanged(PrintChoice? value) => RefreshReceiptPreview();
    partial void OnReceiptFoldCyrillicChanged(bool value) => RefreshReceiptPreview();

    partial void OnReceiptCodeTableChanged(decimal value) => RefreshReceiptPreview();

    private int BusinessWidthSetting => int.TryParse(BusinessPaperWidth, out var width) && ReceiptPaper.IsValid(width) ? width : 0;
    private int BusinessWidth => ReceiptPaper.Sanitize(BusinessWidthSetting);
    private int CartWidthSetting => int.TryParse(CartPaperWidth, out var width) && ReceiptPaper.IsValid(width) ? width : 0;

    public string AutoWidthText => BuildAutoWidthText(ReceiptPrinter);

    public string ZReportAutoWidthText =>
        BuildAutoWidthText(string.IsNullOrWhiteSpace(ZReportPrinter) ? ReceiptPrinter : ZReportPrinter);

    private string BuildAutoWidthText(string? printerName)
    {
        var paper = _printer.DriverReceiptPaper(printerName);
        var auto = paper is null
            ? string.Format(L["receipt_width_auto_fallback_fmt"], BusinessWidth)
            : string.Format(L["receipt_width_auto_fmt"], paper.WidthMm, paper.Columns);
        return $"{auto} {L["receipt_width_hint"]}";
    }

    partial void OnBusinessPaperWidthChanged(string value)
    {
        OnPropertyChanged(nameof(AutoWidthText));
        RefreshReceiptPreview();
    }

    partial void OnSelectedLabelPresetChanged(string? value)
    {
        if (value is null || value == "custom") return;
        // Preset o'lchamlari kodda yozilgan qiymatlar, foydalanuvchi kiritgan matn emas —
        // shuning uchun til o'zgarganda ham bir xil o'qilishi kerak.
        var parts = value.Split('×');
        LabelWidthMm = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        LabelHeightMm = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
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
        ISalesApi salesApi,
        PrintPolicyCache printPolicyCache,
        PrintLogoCache logoCache)
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
        _printPolicyCache = printPolicyCache;
        _logoCache = logoCache;
        CutModeOptions =
        [
            new PrintChoice("partial", L["cut_partial"]),
            new PrintChoice("full", L["cut_full"]),
            new PrintChoice("none", L["cut_none"])
        ];
        ReceiptPrintModeOptions =
        [
            new PrintChoice("auto", L["receipt_print_mode_auto"]),
            new PrintChoice("text", L["receipt_print_mode_text"]),
            new PrintChoice("graphic", L["receipt_print_mode_graphic"])
        ];
        ReceiptTemplateOptions =
        [
            new PrintChoice("auto", L["receipt_template_auto"]),
            new PrintChoice("lines", L["receipt_template_lines"]),
            new PrintChoice("compact", L["receipt_template_compact"]),
            new PrintChoice("table", L["receipt_template_table"])
        ];
        ReceiptQualityOptions =
        [
            new PrintChoice("fast", L["receipt_quality_fast"]),
            new PrintChoice("standard", L["receipt_quality_standard"]),
            new PrintChoice("high", L["receipt_quality_high"])
        ];
        ReceiptTextSizeOptions =
        [
            new PrintChoice("normal", L["receipt_text_size_normal"]),
            new PrintChoice("large", L["receipt_text_size_large"]),
            new PrintChoice("xlarge", L["receipt_text_size_xlarge"]),
            new PrintChoice("double", L["receipt_text_size_double"])
        ];
        ReceiptDarknessOptions =
        [
            new PrintChoice("light", L["receipt_darkness_light"]),
            new PrintChoice("medium", L["receipt_darkness_medium"]),
            new PrintChoice("dark", L["receipt_darkness_dark"])
        ];
        ReceiptDarknessOption = ReceiptDarknessOptions[1];
        ReceiptTextSizeOption = ReceiptTextSizeOptions[0];
        ReceiptQualityOption = ReceiptQualityOptions[1];
        ReceiptCutModeOption = CutModeOptions[0];
        ReceiptPrintModeOption = ReceiptPrintModeOptions[0];
        ReceiptTemplateOption = ReceiptTemplateOptions[0];
        ZReportLanguageOptions = [new PrintChoice("interface", L["interface_language"]), .. ReceiptLanguageOptions];
        ReceiptLanguageOption = ReceiptLanguageOptions[0];
        ZReportLanguageOption = ZReportLanguageOptions[0];
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanViewPrintNetwork));
        _printer.EnsureAutoSetup();
        // Tanlovlar ro'yxatdan OLDIN o'rnatilsa Avalonia bo'sh ItemsSource'ga qarab tanlovni
        // nullga tushiradi va keyingi saqlash printerni o'chirib yuboradi — ro'yxat birinchi.
        var installed = _printer.GetInstalledPrinters();
        Printers.Clear();
        foreach (var name in installed) Printers.Add(name);
        var s = _printer.GetSettings();

        _isLoadingLabelSettings = true;
        _isLoadingReceiptSettings = true;
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
            ReceiptPaperWidth = ReceiptPaper.IsValid(s.ReceiptPaperWidth) ? s.ReceiptPaperWidth.ToString() : "auto";
            HeaderText = s.ReceiptHeaderText ?? string.Empty;
            FooterText = s.ReceiptFooterText ?? string.Empty;
            BusinessPaperWidth = s.ReceiptContentWidth == 0 ? "auto" : ReceiptPaper.Sanitize(s.ReceiptContentWidth).ToString();
            ReceiptLanguageOption = ReceiptLanguageOptions.FirstOrDefault(x => x.Key == s.ReceiptLanguage) ?? ReceiptLanguageOptions[0];
            ZReportLanguageOption = ZReportLanguageOptions.FirstOrDefault(x => x.Key == s.ZReportLanguage) ?? ZReportLanguageOptions[0];
            ReceiptCharsetOption = string.IsNullOrWhiteSpace(s.ReceiptCharset)
                ? CharsetForLanguage(ReceiptLanguageOption?.Key)
                : EscPos.ResolveCharset(s.ReceiptCharset);
            ReceiptCodeTable = s.ReceiptCodeTable is >= 0 and <= 255
                ? s.ReceiptCodeTable
                : Math.Max(0, ReceiptCharsetOption.CodeTable);
            ReceiptPrintModeOption = ReceiptPrintModeOptions.FirstOrDefault(x => x.Key == s.ReceiptPrintMode)
                ?? ReceiptPrintModeOptions[0];
            ReceiptTemplateOption = ReceiptTemplateOptions.FirstOrDefault(x => x.Key == s.ReceiptTemplate)
                ?? ReceiptTemplateOptions[0];
            ReceiptRasterWidthDots = s.ReceiptRasterWidthDots is >= 128 and <= 2048
                ? s.ReceiptRasterWidthDots
                : 0;
            ReceiptDarknessOption = ReceiptDarknessOptions.FirstOrDefault(x => x.Key == s.ReceiptDarkness)
                ?? ReceiptDarknessOptions[1];
            ReceiptTextSizeOption = ReceiptTextSizeOptions.FirstOrDefault(x => x.Key == s.ReceiptTextSize)
                ?? ReceiptTextSizeOptions[0];
            ReceiptQualityOption = ReceiptQualityOptions.FirstOrDefault(x => x.Key == s.ReceiptQuality)
                ?? ReceiptQualityOptions[1];
            ReceiptFoldCyrillic = s.ReceiptFoldCyrillic;
            ReceiptCutModeOption = CutModeOptions.FirstOrDefault(x => x.Key == s.ReceiptCutMode) ?? CutModeOptions[0];
            ReceiptFeedBeforeCut = Math.Clamp(s.ReceiptFeedBeforeCut, 0, 12);
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
            ShowLogo = s.ReceiptShowLogo;
            ShowCustomerPhone = s.ReceiptShowCustomerPhone;
            ShowCustomerEmail = s.ReceiptShowCustomerEmail;
            PublicReceiptBaseUrl = s.ReceiptPublicBaseUrl;
            PrintingLastSyncedAt = s.PrintingLastSyncedAtUtc;
            DocumentPaperSize = s.DocumentPaperSize == "a5" ? "a5" : "a4";
            DocumentOrientation = s.DocumentOrientation == "landscape" ? "landscape" : "portrait";
            DocumentPagesPerSheet = s.DocumentPagesPerSheet is 2 or 4 ? s.DocumentPagesPerSheet : 1;
            ZReportMode = s.ZReportMode is "a4" or "a5" ? s.ZReportMode : "thermal";
            ZReportPaperWidth = ReceiptPaper.IsValid(s.ZReportPaperWidth)
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
            ApplyProformaSettings(new ProformaSettingsDto(
                s.ProformaHeaderText,
                s.ProformaFooterText,
                s.ProformaPaperWidth,
                s.ProformaPaperFormat ?? "Thermal",
                s.ProformaShowBusinessName,
                s.ProformaShowAddress,
                s.ProformaShowPhone,
                s.ProformaShowSeller,
                s.ProformaShowCustomer,
                s.ProformaShowNote,
                s.ProformaShowCartCode));
        }
        finally
        {
            _isLoadingLabelSettings = false;
            _isLoadingReceiptSettings = false;
        }

        // Cached values are applied before the first await, so opening this page
        // never flashes default/off values while network calls are in flight.
        PrintingBootstrapDto? bootstrap = null;
        if (RemotePrintingActive
            && _auth.HasPermission("printing.routes.view|printing.receipts.print|printing.remote.use")
            && _branch.CurrentBranchId is { } bootstrapBranchId)
        {
            try
            {
                bootstrap = await _printingApi.GetBootstrapAsync(bootstrapBranchId, _auth.DeviceId);
                ApplyReceiptSettings(bootstrap.EffectiveReceipt);
                UseBranchReceiptOverride = bootstrap.ReceiptPolicy.ReceiptOverride is not null;
                AutoPrintReceipt = bootstrap.ReceiptPolicy.AutoPrintOnSale;
                ReceiptCopies = Math.Clamp(bootstrap.ReceiptPolicy.DefaultCopies, 1, 5);
                PrintingLastSyncedAt = bootstrap.ServerTimeUtc;
                PrintingSettingsOffline = false;
            }
            catch
            {
                PrintingSettingsOffline = true;
            }
        }

        await LoadLabelPreviewCurrenciesAsync();
        RefreshZReportPrinterPreview();
        NotifyZReportPreviewChanged();
        RefreshLabelPreview();
        RefreshReceiptPreview();

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

        if (bootstrap is null)
        try
        {
            var cfg = await _settingsApi.GetReceiptAsync();
            ApplyReceiptSettings(cfg);
        }
        catch { }

        try
        {
            ApplyProformaSettings(await _settingsApi.GetProformaAsync());
            SaveLocalPrinterSettings();
        }
        catch { }

        _printer.ReceiptOptions = new ReceiptPrintOptions(
            string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            BusinessWidthSetting,
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
            ShowCustomerEmail,
            Template: ReceiptTemplateOption?.Key ?? "auto");
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
            var imageKey = !string.IsNullOrWhiteSpace(business.MonochromeLogoImageKey)
                ? business.MonochromeLogoImageKey
                : business.LogoImageKey;
            var target = _printer.ReceiptTarget();
            if (target.Printer is not null)
                _ = _logoCache.WarmAsync(
                    imageKey,
                    _printer.ReceiptRasterWidth(target.Printer, _printer.ReceiptOptions?.Width));
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
            await _settingsApi.UpdateProformaAsync(new UpdateProformaSettingsRequest(
                string.IsNullOrWhiteSpace(CartHeaderText) ? null : CartHeaderText.Trim(),
                string.IsNullOrWhiteSpace(CartFooterText) ? null : CartFooterText.Trim(),
                CartWidthSetting,
                IsCartThermal ? "Thermal" : IsCartPaperA5 ? "A5" : "A4",
                CartShowBusinessName,
                CartShowAddress,
                CartShowPhone,
                CartShowSeller,
                CartShowCustomer,
                CartShowNote,
                CartShowCartCode));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        if (CanEditReceiptContent && !UseBranchReceiptOverride)
        try
        {
            await _settingsApi.UpdateReceiptAsync(new UpdateReceiptSettingsRequest(
                string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
                string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
                BusinessWidthSetting,
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
                ShowCustomerEmail,
                ReceiptLanguageOption?.Key ?? "uz-latn"));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        if (CanEditAutoPrint && _branch.CurrentBranchId is { } branchId)
        try
        {
            var template = CurrentReceiptSettings();
            var policy = await _printingApi.SetReceiptPolicyAsync(branchId,
                new UpdateReceiptPrintPolicyRequest(
                    AutoPrintReceipt,
                    (int)Math.Clamp(ReceiptCopies, 1, 5),
                    UseBranchReceiptOverride,
                    UseBranchReceiptOverride ? template : null));
            ReceiptCopies = policy.DefaultCopies;
            PrintingLastSyncedAt = DateTime.UtcNow;
            PrintingSettingsOffline = false;
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        _printer.ReceiptOptions = new ReceiptPrintOptions(
            string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            BusinessWidthSetting,
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
            ShowCustomerEmail,
            Template: ReceiptTemplateOption?.Key ?? "auto");
        ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Receipt);
        await _printPolicyCache.RefreshAsync();
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

    private void RefreshReceiptPreview()
    {
        var document = PrinterService.FormatReceiptDocument(CreatePreviewReceipt(), PreviewReceiptOptions());
        var mode = NeedsGraphicForTextSize
            ? "graphic"
            : EscPos.ResolveOutputMode(
                ReceiptPrintModeOption?.Key, document.Text, ReceiptCharsetOption, ReceiptFoldCyrillic);
        ReceiptPreviewBeforeQr = PreviewLines(document.BeforeQr, mode);
        ReceiptPreviewAfterQr = PreviewLines(document.AfterQr, mode);
        OnPropertyChanged(nameof(IsForcedUzbekCyrillicText));
        OnPropertyChanged(nameof(IsNarrowTableTemplate));
        OnPropertyChanged(nameof(IsReceiptGraphicOutput));
        OnPropertyChanged(nameof(PreviewPaperWidth));
        OnPropertyChanged(nameof(PreviewPaperHeight));
        OnPropertyChanged(nameof(ReceiptPreviewSheetWidth));
    }

    private IReadOnlyList<ReceiptPreviewLine> PreviewLines(
        IReadOnlyList<ReceiptTextLine> lines,
        string mode) => lines.Select(line => new ReceiptPreviewLine(
            EscPos.PreviewText(line.Text, mode, ReceiptCharsetOption, ReceiptFoldCyrillic),
            line.Style == ReceiptTextStyle.Total ? Brushes.White : Brushes.Black,
            line.Style == ReceiptTextStyle.Total ? Brushes.Black : Brushes.Transparent,
            FittedPreviewFontSize(line),
            line.Style is ReceiptTextStyle.Title or ReceiptTextStyle.Total or ReceiptTextStyle.Strong
                ? FontWeight.Bold
                : FontWeight.Normal,
            line.Centered ? TextAlignment.Center : TextAlignment.Left)).ToArray();

    /// Ko'rinish qog'ozni ko'rsatishi kerak, shuning uchun shrift rasterdagi kabi
    /// ustunlar sonidan hisoblanadi: bir qatorga aynan shuncha belgi sig'adi.
    /// Uslub nisbatlari raster bilan bir xil (21 asosida: sarlavha +8, jami +3, qalin +1).
    /// 0.98 — hinting sababli haqiqiy belgi eni nazariydan bir oz katta chiqadi va
    /// zaxirasiz oxirgi belgi kesilib qolardi.
    private double PreviewNormalFontSize =>
        (ReceiptPreviewSheetWidth - 38) * 0.98 / (Math.Max(1, EffectiveReceiptWidth) * ConsolasAdvanceRatio);

    private double FittedPreviewFontSize(ReceiptTextLine line)
    {
        var size = PreviewNormalFontSize * line.Style switch
        {
            ReceiptTextStyle.Title => 29d / 21d,
            ReceiptTextStyle.Total => 24d / 21d,
            ReceiptTextStyle.Strong => 22d / 21d,
            _ => 1d
        };
        if (line.Text.Length == 0) return size;
        var available = ReceiptPreviewSheetWidth - 38;
        return Math.Min(size, available / (line.Text.Length * ConsolasAdvanceRatio));
    }

    private const double ConsolasAdvanceRatio = 0.5498;

    private ReceiptPrintOptions PreviewReceiptOptions() => new(
        string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
        string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
        EffectiveReceiptWidth,
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
        ShowCustomerEmail,
        Template: ReceiptTemplateOption?.Key ?? "auto");

    private void ApplyLanguageCharset(string language)
    {
        ReceiptCharsetOption = CharsetForLanguage(language);
        ReceiptCodeTable = Math.Max(0, ReceiptCharsetOption.CodeTable);
    }

    private static EscPosCharset CharsetForLanguage(string? language) => language switch
    {
        "uz-latn" => EscPos.ResolveCharset("cp1252"),
        "uz-cyrl" => EscPos.ResolveCharset("cp1251"),
        "ru" => EscPos.ResolveCharset("cp866"),
        "en" => EscPos.ResolveCharset("cp437"),
        _ => EscPos.ResolveCharset("cp1252")
    };

    private ReceiptDto CreatePreviewReceipt()
    {
        var language = ReceiptLanguageOption?.Key ?? "uz-latn";
        var items = new List<ReceiptItemDto>
        {
            new(ReceiptTexts.Get("sample_item_one", language), 2, ReceiptTexts.Get("unit_piece", language), 12_500, 25_000),
            new(ReceiptTexts.Get("sample_item_two", language), 1, ReceiptTexts.Get("unit_piece", language), 8_000, 8_000),
            new(ReceiptTexts.Get("sample_item_three", language), 1.5m, ReceiptTexts.Get("unit_piece", language), 14_000, 21_000, 3_000),
            new(ReceiptTexts.Get("sample_item_four", language), 2, ReceiptTexts.Get("unit_piece", language), 9_500, 19_000),
            new(ReceiptTexts.Get("sample_item_five", language), 1, ReceiptTexts.Get("unit_piece", language), 11_000, 11_000)
        };
        return new ReceiptDto(
            "preview-1048",
            PreviewBusinessName,
            PreviewBranchName,
            PreviewAddress,
            PreviewPhone,
            DateTime.Now,
            81_000,
            3_000,
            81_000,
            0,
            0,
            0,
            0,
            0,
            PreviewCashierName,
            items,
            [new ReceiptPaymentDto("Cash", "UZS", 81_000, 1, 81_000)],
            1048,
            "Dilshod",
            PreviewCustomerPhone,
            PreviewCustomerEmail,
            language,
            PreviewPhone);
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
            ReceiptPaperWidth = int.TryParse(ReceiptPaperWidth, out var width) ? ReceiptPaper.Sanitize(width, 0) : 0,
            ReceiptCopies = (int)Math.Clamp(ReceiptCopies, 1, 5),
            ReceiptHeaderText = string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
            ReceiptFooterText = string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
            ReceiptContentWidth = BusinessWidthSetting,
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
            ReceiptShowLogo = ShowLogo,
            ReceiptShowCustomerPhone = ShowCustomerPhone,
            ReceiptShowCustomerEmail = ShowCustomerEmail,
            ReceiptPublicBaseUrl = PublicReceiptBaseUrl,
            CachedPrintingBranchId = _branch.CurrentBranchId,
            CachedPrintingRevision = null,
            PrintingLastSyncedAtUtc = PrintingLastSyncedAt,
            CentralAutoPrint = RemotePrintingActive && !PrintingSettingsOffline && AutoPrintReceipt,
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
            ZReportPaperWidth = int.TryParse(ZReportPaperWidth, out var zWidth) ? ReceiptPaper.Sanitize(zWidth, 0) : 0,
            ZReportDocumentPaperSize = ZReportDocumentPaperSize,
            ZReportDocumentOrientation = ZReportDocumentOrientation,
            ZReportDocumentPagesPerSheet = ZReportDocumentPagesPerSheet,
            ProformaPaperFormat = IsCartThermal ? "Thermal" : IsCartPaperA5 ? "A5" : "A4",
            ProformaPaperWidth = CartWidthSetting,
            ProformaHeaderText = string.IsNullOrWhiteSpace(CartHeaderText) ? null : CartHeaderText.Trim(),
            ProformaFooterText = string.IsNullOrWhiteSpace(CartFooterText) ? null : CartFooterText.Trim(),
            ProformaShowBusinessName = CartShowBusinessName,
            ProformaShowAddress = CartShowAddress,
            ProformaShowPhone = CartShowPhone,
            ProformaShowSeller = CartShowSeller,
            ProformaShowCustomer = CartShowCustomer,
            ProformaShowNote = CartShowNote,
            ProformaShowCartCode = CartShowCartCode,
            ReceiptLanguage = ReceiptLanguageOption?.Key,
            ZReportLanguage = ZReportLanguageOption is { Key: not "interface" } zLang ? zLang.Key : null,
            ReceiptCharset = ReceiptCharsetOption.Key,
            ReceiptCodeTable = (int)Math.Clamp(ReceiptCodeTable, 0, 255),
            ReceiptPrintMode = ReceiptPrintModeOption?.Key,
            ReceiptTemplate = ReceiptTemplateOption?.Key,
            ReceiptDarkness = ReceiptDarknessOption?.Key,
            ReceiptTextSize = ReceiptTextSizeOption?.Key,
            ReceiptQuality = ReceiptQualityOption?.Key,
            ReceiptFoldCyrillic = ReceiptFoldCyrillic,
            ReceiptRasterWidthDots = ReceiptRasterWidthDots is >= 128 and <= 2048
                ? (int)ReceiptRasterWidthDots / 8 * 8
                : 0,
            ReceiptCutMode = ReceiptCutModeOption?.Key,
            ReceiptFeedBeforeCut = (int)Math.Clamp(ReceiptFeedBeforeCut, 0, 12),
            AutoSetupSignature = _printer.GetSettings().AutoSetupSignature
        });
    }

    private void ApplyProformaSettings(ProformaSettingsDto cfg)
    {
        CartMode = cfg.PaperFormat == "A4" ? "a4" : cfg.PaperFormat == "A5" ? "a5" : "thermal";
        CartPaperWidth = cfg.PaperWidth == 0 ? "auto" : ReceiptPaper.Sanitize(cfg.PaperWidth).ToString();
        CartHeaderText = cfg.HeaderText ?? string.Empty;
        CartFooterText = cfg.FooterText ?? string.Empty;
        CartShowBusinessName = cfg.ShowBusinessName;
        CartShowAddress = cfg.ShowAddress;
        CartShowPhone = cfg.ShowPhone;
        CartShowSeller = cfg.ShowSeller;
        CartShowCustomer = cfg.ShowCustomer;
        CartShowNote = cfg.ShowNote;
        CartShowCartCode = cfg.ShowCartCode;
    }

    [RelayCommand]
    private async Task TestProformaPrintAsync()
    {
        try
        {
            SaveLocalPrinterSettings();
            Cartex.Shared.Models.Business.BusinessDto? business = null;
            try { business = await _businessApi.GetAsync(); } catch { }
            _printer.PrintProforma(CreatePreviewProforma(), "S-1024", business);
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private PreviewDocument CreatePreviewProforma() => new(
        DateTime.Now,
        PreviewCashierName,
        "Dilshod",
        [
            new PreviewLine(L["receipt_preview_item_one"], 2, L["unit"], 12_500, 25_000),
            new PreviewLine(L["receipt_preview_item_two"], 1, L["unit"], 8_000, 8_000),
            new PreviewLine(L["receipt_preview_item_three"], 1.5m, L["unit"], 14_000, 21_000),
            new PreviewLine(L["receipt_preview_item_four"], 2, L["unit"], 9_500, 19_000),
            new PreviewLine(L["receipt_preview_item_five"], 1, L["unit"], 11_000, 11_000)
        ],
        0,
        84_000,
        null);

    private void ApplyReceiptSettings(ReceiptSettingsDto cfg)
    {
        HeaderText = cfg.HeaderText ?? string.Empty;
        FooterText = cfg.FooterText ?? string.Empty;
        BusinessPaperWidth = cfg.PaperWidth == 0 ? "auto" : ReceiptPaper.Sanitize(cfg.PaperWidth).ToString();
        ReceiptLanguageOption = ReceiptLanguageOptions.FirstOrDefault(x => x.Key == cfg.Language) ?? ReceiptLanguageOptions[0];
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

    private ReceiptSettingsDto CurrentReceiptSettings() => new(
        string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
        string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
        BusinessWidthSetting,
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
        PublicReceiptBaseUrl,
        ShowLogo,
        ShowCustomerPhone,
        ShowCustomerEmail,
        ReceiptLanguageOption?.Key ?? "uz-latn");

    private static bool IsPdfPrinter(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Save to PDF", StringComparison.OrdinalIgnoreCase)
            || value.Contains("XPS", StringComparison.OrdinalIgnoreCase));
}

public sealed record PrintChoice(string Key, string Label);

public sealed record ReceiptPreviewLine(
    string Text,
    IBrush Foreground,
    IBrush Background,
    double FontSize,
    FontWeight FontWeight,
    TextAlignment TextAlignment);
