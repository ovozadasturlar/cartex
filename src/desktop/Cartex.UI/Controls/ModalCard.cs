using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Cartex.UI.Controls;

public class ModalCard : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalCard, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ModalCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<ModalCard, double>(nameof(CardWidth), 480);

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<ModalCard, ICommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<ICommand?> SaveCommandProperty =
        AvaloniaProperty.Register<ModalCard, ICommand?>(nameof(SaveCommand));

    public static readonly StyledProperty<ICommand?> SaveAndNewCommandProperty =
        AvaloniaProperty.Register<ModalCard, ICommand?>(nameof(SaveAndNewCommand));

    public static readonly StyledProperty<string?> SaveTextProperty =
        AvaloniaProperty.Register<ModalCard, string?>(nameof(SaveText));

    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<ModalCard, string?>(nameof(CancelText));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ModalCard, object?>(nameof(Footer));

    public static readonly StyledProperty<bool> ShowFooterProperty =
        AvaloniaProperty.Register<ModalCard, bool>(nameof(ShowFooter), true);

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    public ICommand? SaveCommand { get => GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }
    public ICommand? SaveAndNewCommand { get => GetValue(SaveAndNewCommandProperty); set => SetValue(SaveAndNewCommandProperty, value); }
    public string? SaveText { get => GetValue(SaveTextProperty); set => SetValue(SaveTextProperty, value); }
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public bool ShowFooter { get => GetValue(ShowFooterProperty); set => SetValue(ShowFooterProperty, value); }

    // Oyna ochilishi bilan kursor birinchi maydonda bo'ladi - foydalanuvchi darhol yozishni
    // boshlaydi va sichqonchaga qo'l urmaydi.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty && change.GetNewValue<bool>())
            Dispatcher.UIThread.Post(() => FormBehaviors.FocusFirst(this), DispatcherPriority.Background);
    }
}
