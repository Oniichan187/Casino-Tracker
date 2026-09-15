namespace CasinoTracker.Controls;

public enum SeriesKind
{
    Line,
    Step,
    Bars,
    Scatter,
}

public sealed class ChartPoint
{
    public ChartPoint(double x, double y, string? label = null)
    {
        X = x;
        Y = y;
        Label = label;
    }

    public double X { get; }
    public double Y { get; }
    public string? Label { get; }
}

public sealed class ChartSeries
{
    public string Name { get; init; } = string.Empty;
    public Color Color { get; init; } = Color.FromArgb("#2E7D32");
    public Color NegativeColor { get; init; } = Color.FromArgb("#D32F2F");
    public SeriesKind Kind { get; init; } = SeriesKind.Line;
    public float StrokeSize { get; init; } = 2.5f;
    public bool ShowMarkers { get; init; }
    public bool ShowLabels { get; init; }
    public bool Fill { get; init; }
    public IReadOnlyList<ChartPoint> Points { get; init; } = Array.Empty<ChartPoint>();
}

public sealed class BarItem
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public Color? Color { get; init; }
}
