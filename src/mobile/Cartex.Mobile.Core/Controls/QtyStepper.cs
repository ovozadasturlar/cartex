using System.Windows.Input;

namespace Cartex.Mobile.Core.Controls;

public sealed class QtyStepper : ContentView
{
    public static readonly BindableProperty QuantityProperty =
        BindableProperty.Create(nameof(Quantity), typeof(decimal), typeof(QtyStepper), 1m,
            BindingMode.TwoWay, propertyChanged: OnQuantityChanged);

    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(decimal), typeof(QtyStepper), 1m);

    public static readonly BindableProperty UnitProperty =
        BindableProperty.Create(nameof(Unit), typeof(string), typeof(QtyStepper), string.Empty, propertyChanged: OnQuantityChanged);

    public static readonly BindableProperty ChangedCommandProperty =
        BindableProperty.Create(nameof(ChangedCommand), typeof(ICommand), typeof(QtyStepper));

    public decimal Quantity { get => (decimal)GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }
    public decimal Step { get => (decimal)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public ICommand? ChangedCommand { get => (ICommand?)GetValue(ChangedCommandProperty); set => SetValue(ChangedCommandProperty, value); }

    private readonly Label _value = new()
    {
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        FontFamily = "OpenSansSemibold",
        FontSize = 15,
        MinimumWidthRequest = 56,
    };

    public QtyStepper()
    {
        var minus = Key("4", () => Change(-Step));
        var plus = Key("5", () => Change(Step));

        Content = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)],
            ColumnSpacing = 2,
            Children = { minus, _value, plus },
        };
        Grid.SetColumn(_value, 1);
        Grid.SetColumn(plus, 2);
        Render();
    }

    private Border Key(string glyph, Action action)
    {
        var label = new Label
        {
            Text = glyph,
            FontFamily = "MDI",
            FontSize = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        label.SetAppTheme(Label.TextColorProperty, Color.FromArgb("#255D3A"), Color.FromArgb("#4ADE80"));

        var border = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = label,
        };
        border.SetAppTheme(BackgroundColorProperty, Color.FromArgb("#DCFCE7"), Color.FromArgb("#12301F"));
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(action) });
        return border;
    }

    private void Change(decimal delta)
    {
        var next = Quantity + delta;
        Quantity = next < 0 ? 0 : next;
        ChangedCommand?.Execute(Quantity);
    }

    private static void OnQuantityChanged(BindableObject bindable, object oldValue, object newValue) => ((QtyStepper)bindable).Render();

    private void Render() => _value.Text = Money.QuantityWithUnit(Quantity, Unit);
}
