using System;
using Avalonia.Controls;

namespace Cartex.Desktop;

public partial class MainWindow : Window
{
    private const double PreferredWidth = 1400;
    private const double PreferredHeight = 900;

    public MainWindow()
    {
        InitializeComponent();
        FitToScreen();
    }

    // Oyna o'lchami ekranning ish maydoniga (masshtab va vazifalar paneli hisobga olingan holda)
    // sig'dirilmasa, sarlavha va oyna tugmalari ekran tashqarisiga chiqib ketadi.
    private void FitToScreen()
    {
        var screen = Screens.Primary ?? (Screens.ScreenCount > 0 ? Screens.All[0] : null);
        if (screen is null)
        {
            Width = PreferredWidth;
            Height = PreferredHeight;
            return;
        }

        var scaling = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var available = screen.WorkingArea;
        var maxWidth = available.Width / scaling - 40;
        var maxHeight = available.Height / scaling - 40;

        Width = Math.Max(MinWidth, Math.Min(PreferredWidth, maxWidth));
        Height = Math.Max(MinHeight, Math.Min(PreferredHeight, maxHeight));

        if (maxHeight < MinHeight || maxWidth < MinWidth)
            WindowState = WindowState.Maximized;
    }
}
