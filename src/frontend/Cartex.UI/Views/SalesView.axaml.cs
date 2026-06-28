using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Material.Icons;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    private const string PanelDragFormat = "PanelId";
    private string[] _panelSlots = LayoutCycleEvent.PanelSlots;
    private int _layoutMode = LayoutCycleEvent.LayoutMode;
    private string[] _touchPanelSlots = LayoutCycleEvent.TouchPanelSlots;
    private int _touchLayoutMode = LayoutCycleEvent.TouchLayoutMode;

    public SalesView()
    {
        InitializeComponent();
        WireScrollButtons();
        WireDragDrop();
        WireTouchDragDrop();
        WireResponsiveGrid();
        WireTouchAutoLayout();
        LayoutCycleEvent.Requested += OnLayoutCycleRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutCycleEvent.Requested -= OnLayoutCycleRequested;
        SaveGridSizes();
        SaveTouchGridSizes();
    }

    private void SaveGridSizes()
    {
        if (DesktopGrid is null) return;
        LayoutCycleEvent.SavedLayoutMode = _layoutMode;
        LayoutCycleEvent.SavedColumnWidths = DesktopGrid.ColumnDefinitions.Select(c => c.Width).ToArray();
        LayoutCycleEvent.SavedRowHeights = DesktopGrid.RowDefinitions.Select(r => r.Height).ToArray();
    }

    private void RestoreGridSizes()
    {
        if (DesktopGrid is null) return;
        if (LayoutCycleEvent.SavedLayoutMode != _layoutMode) return;

        var cols = LayoutCycleEvent.SavedColumnWidths;
        var rows = LayoutCycleEvent.SavedRowHeights;
        if (cols is not null && cols.Length == DesktopGrid.ColumnDefinitions.Count)
            for (var i = 0; i < cols.Length; i++)
                DesktopGrid.ColumnDefinitions[i].Width = cols[i];

        if (rows is not null && rows.Length == DesktopGrid.RowDefinitions.Count)
            for (var i = 0; i < rows.Length; i++)
                DesktopGrid.RowDefinitions[i].Height = rows[i];
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SalesViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            ApplyLayout(rebuildGrid: true);
            ApplyTouchLayout(rebuildGrid: true);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void OnLayoutCycleRequested()
    {
        var vm = DataContext as SalesViewModel;
        if (vm?.IsTouchMode == true)
        {
            _touchLayoutMode = (_touchLayoutMode + 1) % 5;
            PersistTouchLayout();
            ApplyTouchLayout(rebuildGrid: true);
            LayoutCycleEvent.NotifyIconChanged(_touchLayoutMode switch
            {
                0 => MaterialIconKind.ViewColumn,
                1 => MaterialIconKind.ViewModule,
                2 => MaterialIconKind.ViewStream,
                3 => MaterialIconKind.ViewAgenda,
                4 => MaterialIconKind.ViewParallel,
                _ => MaterialIconKind.ViewColumn
            });
        }
        else
        {
            _layoutMode = (_layoutMode + 1) % 5;
            PersistLayout();
            ApplyLayout(rebuildGrid: true);
            LayoutCycleEvent.NotifyIconChanged(_layoutMode switch
            {
                0 => MaterialIconKind.ViewColumn,
                1 => MaterialIconKind.ViewModule,
                2 => MaterialIconKind.ViewStream,
                3 => MaterialIconKind.ViewAgenda,
                4 => MaterialIconKind.ViewParallel,
                _ => MaterialIconKind.ViewColumn
            });
        }
    }

    private void PersistLayout()
    {
        LayoutCycleEvent.LayoutMode = _layoutMode;
        LayoutCycleEvent.PanelSlots = (string[])_panelSlots.Clone();
    }

    private Dictionary<string, Border> GetPanels() => new()
    {
        ["products"] = ProductsPanel!,
        ["cart"] = CartPanel!,
        ["payment"] = PaymentPanel!
    };

    private void ApplyLayout(bool rebuildGrid)
    {
        if (DesktopGrid is null || ProductsPanel is null || CartPanel is null || PaymentPanel is null) return;

        var panels = GetPanels();

        if (rebuildGrid)
        {
            DesktopGrid.ColumnDefinitions.Clear();
            DesktopGrid.RowDefinitions.Clear();

            switch (_layoutMode)
            {
                case 1: BuildThreeColumnGrid(); break;
                case 2: BuildTwoRowBottomGrid(); break;
                case 3: BuildTwoRowTopGrid(); break;
                case 4: BuildTwoColumnRightGrid(); break;
                default: BuildTwoColumnGrid(); break;
            }

            RestoreGridSizes();
        }

        PositionPanels(panels);

        DesktopGrid.InvalidateMeasure();
        DesktopGrid.InvalidateArrange();
    }

    private void PositionPanels(Dictionary<string, Border> panels)
    {
        switch (_layoutMode)
        {
            case 1: PositionThreeColumn(panels); break;
            case 2: PositionTwoRowBottom(panels); break;
            case 3: PositionTwoRowTop(panels); break;
            case 4: PositionTwoColumnRight(panels); break;
            default: PositionTwoColumn(panels); break;
        }
    }

    private void BuildTwoColumnGrid()
    {
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 300 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(420)) { MinWidth = 280 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        ConfigureSplitter(MainSplitter, col: 1, row: 0, rowSpan: 3, colSpan: 1, isColumn: true);
        ConfigureSplitter(SecondSplitter, col: 2, row: 1, rowSpan: 1, colSpan: 1, isColumn: false);
    }

    private void PositionTwoColumn(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_panelSlots[0]], col: 0, row: 0, colSpan: 1, rowSpan: 3);
        SetPanel(panels[_panelSlots[1]], col: 2, row: 0);
        SetPanel(panels[_panelSlots[2]], col: 2, row: 2);
    }

    private void BuildThreeColumnGrid()
    {
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        ConfigureSplitter(MainSplitter, col: 1, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
        ConfigureSplitter(SecondSplitter, col: 3, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
    }

    private void PositionThreeColumn(Dictionary<string, Border> panels)
    {
        for (var i = 0; i < 3; i++)
            SetPanel(panels[_panelSlots[i]], col: i * 2, row: 0);
    }

    private void BuildTwoRowBottomGrid()
    {
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        ConfigureSplitter(MainSplitter, col: 1, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
        ConfigureSplitter(SecondSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 3, isColumn: false);
    }

    private void PositionTwoRowBottom(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_panelSlots[0]], col: 0, row: 0);
        SetPanel(panels[_panelSlots[1]], col: 2, row: 0);
        SetPanel(panels[_panelSlots[2]], col: 0, row: 2, colSpan: 3);
    }

    private void BuildTwoRowTopGrid()
    {
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 200 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        ConfigureSplitter(MainSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 3, isColumn: false);
        ConfigureSplitter(SecondSplitter, col: 1, row: 2, rowSpan: 1, colSpan: 1, isColumn: true);
    }

    private void PositionTwoRowTop(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_panelSlots[0]], col: 0, row: 0, colSpan: 3);
        SetPanel(panels[_panelSlots[1]], col: 0, row: 2);
        SetPanel(panels[_panelSlots[2]], col: 2, row: 2);
    }

    private void BuildTwoColumnRightGrid()
    {
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(420)) { MinWidth = 280 });
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        DesktopGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 300 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DesktopGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 150 });
        ConfigureSplitter(MainSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 1, isColumn: false);
        ConfigureSplitter(SecondSplitter, col: 1, row: 0, rowSpan: 3, colSpan: 1, isColumn: true);
    }

    private void PositionTwoColumnRight(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_panelSlots[0]], col: 0, row: 0);
        SetPanel(panels[_panelSlots[1]], col: 0, row: 2);
        SetPanel(panels[_panelSlots[2]], col: 2, row: 0, rowSpan: 3);
    }

    private static void SetPanel(Border panel, int col, int row, int colSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(panel, col);
        Grid.SetRow(panel, row);
        Grid.SetColumnSpan(panel, colSpan);
        Grid.SetRowSpan(panel, rowSpan);
    }

    private static void ConfigureSplitter(GridSplitter? splitter, int col, int row, int rowSpan, int colSpan, bool isColumn)
    {
        if (splitter is null) return;
        Grid.SetColumn(splitter, col);
        Grid.SetRow(splitter, row);
        Grid.SetRowSpan(splitter, rowSpan);
        Grid.SetColumnSpan(splitter, colSpan);
        splitter.IsVisible = true;

        if (isColumn)
        {
            splitter.Width = 6;
            splitter.Height = double.NaN;
            splitter.ResizeDirection = GridResizeDirection.Columns;
            splitter.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }
        else
        {
            splitter.Width = double.NaN;
            splitter.Height = 6;
            splitter.ResizeDirection = GridResizeDirection.Rows;
            splitter.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        }
    }

    private void WireDragDrop()
    {
        WireGrip(ProductsDragGrip, "products");
        WireGrip(CartDragGrip, "cart");
        WireGrip(PaymentDragGrip, "payment");
        WireDropTarget(ProductsPanel);
        WireDropTarget(CartPanel);
        WireDropTarget(PaymentPanel);
    }

    private void WireGrip(Border? grip, string panelId)
    {
        if (grip is null) return;
        grip.PointerPressed += async (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            var data = new DataObject();
            data.Set(PanelDragFormat, panelId);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        };
    }

    private void WireDropTarget(Border? target)
    {
        WireDropTarget(target, isTouch: false);
    }

    private void WireDropTarget(Border? target, bool isTouch)
    {
        if (target is null) return;
        target.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Move;
            if (target.Tag?.ToString() != e.Data.Get(PanelDragFormat)?.ToString())
                target.Opacity = 0.7;
        });
        target.AddHandler(DragDrop.DragLeaveEvent, (_, _) => target.Opacity = 1.0);
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            target.Opacity = 1.0;
            var source = e.Data.Get(PanelDragFormat)?.ToString();
            var dest = target.Tag?.ToString();
            if (source is null || dest is null || source == dest) return;

            var slots = isTouch ? _touchPanelSlots : _panelSlots;
            var srcIdx = Array.IndexOf(slots, source);
            var dstIdx = Array.IndexOf(slots, dest);
            if (srcIdx < 0 || dstIdx < 0) return;

            (slots[srcIdx], slots[dstIdx]) = (slots[dstIdx], slots[srcIdx]);

            if (isTouch)
            {
                PersistTouchLayout();
                ApplyTouchLayout(rebuildGrid: false);
            }
            else
            {
                PersistLayout();
                ApplyLayout(rebuildGrid: false);
            }
        });
    }

    private void WireScrollButtons()
    {
        WirePair(CatScrollLeft, CatScrollRight, CatScroll);
        WirePair(TouchCatLeft, TouchCatRight, TouchCatScroll);
    }

    private static void WirePair(Button? left, Button? right, ScrollViewer? sv)
    {
        if (sv is null) return;
        UpdateArrowStates(left, right, sv);
        sv.ScrollChanged += (_, _) => UpdateArrowStates(left, right, sv);
        left?.AddHandler(Button.ClickEvent, (_, _) =>
        {
            sv.Offset = sv.Offset.WithX(Math.Max(0, sv.Offset.X - 150));
            UpdateArrowStates(left, right, sv);
        });
        right?.AddHandler(Button.ClickEvent, (_, _) =>
        {
            sv.Offset = sv.Offset.WithX(sv.Offset.X + 150);
            UpdateArrowStates(left, right, sv);
        });
    }

    private static void UpdateArrowStates(Button? left, Button? right, ScrollViewer sv)
    {
        if (left is not null) left.IsEnabled = sv.Offset.X > 0;
        if (right is not null) right.IsEnabled = sv.Offset.X + sv.Viewport.Width < sv.Extent.Width - 1;
    }

    private void WireResponsiveGrid()
    {
        if (ProductsGrid is null) return;
        ProductsGrid.SizeChanged += (_, e) =>
        {
            if (ProductsGrid.Columns.Count >= 2)
                ProductsGrid.Columns[1].IsVisible = e.NewSize.Width > 400;
        };
    }

    // ===== TOUCH LAYOUT MANAGEMENT =====

    private void WireTouchAutoLayout()
    {
    }

    private Dictionary<string, Border> GetTouchPanels() => new()
    {
        ["t-products"] = TouchProductsPanel!,
        ["t-cart"] = TouchCartPanel!,
        ["t-payment"] = TouchPaymentPanel!
    };

    private void ApplyTouchLayout(bool rebuildGrid)
    {
        if (TouchGrid is null || TouchProductsPanel is null || TouchCartPanel is null || TouchPaymentPanel is null) return;

        var panels = GetTouchPanels();

        if (rebuildGrid)
        {
            TouchGrid.ColumnDefinitions.Clear();
            TouchGrid.RowDefinitions.Clear();

            switch (_touchLayoutMode)
            {
                case 1: BuildTouchThreeColumnGrid(); break;
                case 2: BuildTouchTwoRowBottomGrid(); break;
                case 3: BuildTouchTwoRowTopGrid(); break;
                case 4: BuildTouchTwoColumnRightGrid(); break;
                default: BuildTouchTwoColumnGrid(); break;
            }

            RestoreTouchGridSizes();
        }

        PositionTouchPanels(panels);
        TouchGrid.InvalidateMeasure();
        TouchGrid.InvalidateArrange();
    }

    private void PositionTouchPanels(Dictionary<string, Border> panels)
    {
        switch (_touchLayoutMode)
        {
            case 1: PositionTouchThreeColumn(panels); break;
            case 2: PositionTouchTwoRowBottom(panels); break;
            case 3: PositionTouchTwoRowTop(panels); break;
            case 4: PositionTouchTwoColumnRight(panels); break;
            default: PositionTouchTwoColumn(panels); break;
        }
    }

    private void BuildTouchTwoColumnGrid()
    {
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 280 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(420)) { MinWidth = 300 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        ConfigureSplitter(TouchMainSplitter, col: 1, row: 0, rowSpan: 3, colSpan: 1, isColumn: true);
        ConfigureSplitter(TouchSecondSplitter, col: 2, row: 1, rowSpan: 1, colSpan: 1, isColumn: false);
    }

    private void PositionTouchTwoColumn(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_touchPanelSlots[0]], col: 0, row: 0, colSpan: 1, rowSpan: 3);
        SetPanel(panels[_touchPanelSlots[1]], col: 2, row: 0);
        SetPanel(panels[_touchPanelSlots[2]], col: 2, row: 2);
    }

    private void BuildTouchThreeColumnGrid()
    {
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        ConfigureSplitter(TouchMainSplitter, col: 1, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
        ConfigureSplitter(TouchSecondSplitter, col: 3, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
    }

    private void PositionTouchThreeColumn(Dictionary<string, Border> panels)
    {
        for (var i = 0; i < 3; i++)
            SetPanel(panels[_touchPanelSlots[i]], col: i * 2, row: 0);
    }

    private void BuildTouchTwoRowBottomGrid()
    {
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        ConfigureSplitter(TouchMainSplitter, col: 1, row: 0, rowSpan: 1, colSpan: 1, isColumn: true);
        ConfigureSplitter(TouchSecondSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 3, isColumn: false);
    }

    private void PositionTouchTwoRowBottom(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_touchPanelSlots[0]], col: 0, row: 0);
        SetPanel(panels[_touchPanelSlots[1]], col: 2, row: 0);
        SetPanel(panels[_touchPanelSlots[2]], col: 0, row: 2, colSpan: 3);
    }

    private void BuildTouchTwoRowTopGrid()
    {
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 250 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        ConfigureSplitter(TouchMainSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 3, isColumn: false);
        ConfigureSplitter(TouchSecondSplitter, col: 1, row: 2, rowSpan: 1, colSpan: 1, isColumn: true);
    }

    private void PositionTouchTwoRowTop(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_touchPanelSlots[0]], col: 0, row: 0, colSpan: 3);
        SetPanel(panels[_touchPanelSlots[1]], col: 0, row: 2);
        SetPanel(panels[_touchPanelSlots[2]], col: 2, row: 2);
    }

    private void BuildTouchTwoColumnRightGrid()
    {
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(420)) { MinWidth = 300 });
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        TouchGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star) { MinWidth = 280 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        TouchGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star) { MinHeight = 200 });
        ConfigureSplitter(TouchMainSplitter, col: 0, row: 1, rowSpan: 1, colSpan: 1, isColumn: false);
        ConfigureSplitter(TouchSecondSplitter, col: 1, row: 0, rowSpan: 3, colSpan: 1, isColumn: true);
    }

    private void PositionTouchTwoColumnRight(Dictionary<string, Border> panels)
    {
        SetPanel(panels[_touchPanelSlots[0]], col: 0, row: 0);
        SetPanel(panels[_touchPanelSlots[1]], col: 0, row: 2);
        SetPanel(panels[_touchPanelSlots[2]], col: 2, row: 0, rowSpan: 3);
    }

    private void PersistTouchLayout()
    {
        LayoutCycleEvent.TouchLayoutMode = _touchLayoutMode;
        LayoutCycleEvent.TouchPanelSlots = (string[])_touchPanelSlots.Clone();
    }

    private void SaveTouchGridSizes()
    {
        if (TouchGrid is null) return;
        LayoutCycleEvent.TouchSavedLayoutMode = _touchLayoutMode;
        LayoutCycleEvent.TouchSavedColumnWidths = TouchGrid.ColumnDefinitions.Select(c => c.Width).ToArray();
        LayoutCycleEvent.TouchSavedRowHeights = TouchGrid.RowDefinitions.Select(r => r.Height).ToArray();
    }

    private void RestoreTouchGridSizes()
    {
        if (TouchGrid is null) return;
        if (LayoutCycleEvent.TouchSavedLayoutMode != _touchLayoutMode) return;

        var cols = LayoutCycleEvent.TouchSavedColumnWidths;
        var rows = LayoutCycleEvent.TouchSavedRowHeights;
        if (cols is not null && cols.Length == TouchGrid.ColumnDefinitions.Count)
            for (var i = 0; i < cols.Length; i++)
                TouchGrid.ColumnDefinitions[i].Width = cols[i];

        if (rows is not null && rows.Length == TouchGrid.RowDefinitions.Count)
            for (var i = 0; i < rows.Length; i++)
                TouchGrid.RowDefinitions[i].Height = rows[i];
    }

    private void WireTouchDragDrop()
    {
        WireGrip(TouchProductsDragGrip, "t-products");
        WireGrip(TouchCartDragGrip, "t-cart");
        WireGrip(TouchPaymentDragGrip, "t-payment");
        WireDropTarget(TouchProductsPanel, isTouch: true);
        WireDropTarget(TouchCartPanel, isTouch: true);
        WireDropTarget(TouchPaymentPanel, isTouch: true);
    }
}
