using Avalonia.Media;

namespace Cartex.UI.Controls;

public sealed record ChartBar(double Height, IBrush Fill);

public sealed record ChartColumn(string Label, string ValueText, IReadOnlyList<ChartBar> Bars);
