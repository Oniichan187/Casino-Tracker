using System.Globalization;
using Font = Microsoft.Maui.Graphics.Font;

namespace CasinoTracker.Controls;

internal sealed class ChartDrawable : IDrawable
{
    private const float PadLeft = 58f;
    private const float PadRight = 14f;
    private const float PadTop = 14f;
    private const float PadBottom = 40f;
    private const float LegendRowHeight = 20f;
    private const float LabelHeadroom = 16f;
    private const float FontSize = 11f;
    private const float LabelFontSize = 10f;
    private const int MaxGridLines = 40;

    private readonly ChartView _view;

    public ChartDrawable(ChartView view)
    {
        _view = view;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.Antialias = true;
        canvas.Font = Font.Default;
        canvas.FontSize = FontSize;
        canvas.FontColor = _view.TextColor;

        var bars = _view.Bars;
        var series = _view.Series;

        if (bars is { Count: > 0 })
            DrawBars(canvas, dirtyRect, bars);
        else if (series is { Count: > 0 } && series.Any(s => s.Points.Count > 0))
            DrawSeries(canvas, dirtyRect, series);
        else
            DrawEmpty(canvas, dirtyRect);

        canvas.RestoreState();
    }

    // ------------------------------------------------------------------ empty

    private void DrawEmpty(ICanvas canvas, RectF rect)
    {
        canvas.FontSize = 13f;
        canvas.DrawString(_view.EmptyText, rect.Left, rect.Top, rect.Width, rect.Height,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    // ------------------------------------------------------------------ categorical bars

    private void DrawBars(ICanvas canvas, RectF rect, IReadOnlyList<BarItem> bars)
    {
        var plot = new RectF(rect.Left + PadLeft, rect.Top + PadTop, rect.Width - PadLeft - PadRight,
            rect.Height - PadTop - PadBottom);
        if (plot.Width <= 10 || plot.Height <= 10) return;

        double min = Math.Min(0, bars.Min(b => b.Value));
        double max = Math.Max(0, bars.Max(b => b.Value));
        if (max - min < 1e-9) max = min + 1;
        var (nMin, nMax, step) = NiceScale(min, max, 5, MinStep());

        float Y(double v) => (float)(plot.Bottom - (v - nMin) / (nMax - nMin) * plot.Height);

        DrawHorizontalGrid(canvas, rect, plot, nMin, nMax, step, Y);

        int n = bars.Count;
        float slot = plot.Width / n;
        float barWidth = Math.Clamp(slot * 0.62f, 6f, 64f);
        float zeroY = Y(0);

        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
            float cx = plot.Left + slot * (i + 0.5f);
            float y = Y(bar.Value);
            float top = Math.Min(y, zeroY);
            float height = Math.Max(2f, Math.Abs(y - zeroY));

            canvas.FillColor = bar.Color ?? (bar.Value >= 0 ? Color.FromArgb("#2E7D32") : Color.FromArgb("#D32F2F"));
            canvas.FillRoundedRectangle(cx - barWidth / 2, top, barWidth, height, 3f);

            // Value label: above positive bars, and above the zero line for negative bars
            // (that column is always free, so it never collides with the category label).
            canvas.FontColor = _view.TextColor;
            canvas.FontSize = LabelFontSize;
            float valueY = bar.Value >= 0 ? top - 4f : zeroY - 4f;
            canvas.DrawString(FormatValue(bar.Value, step), cx, valueY, HorizontalAlignment.Center);

            // Category label (truncated to the slot width)
            canvas.FontSize = FontSize;
            canvas.DrawString(Truncate(canvas, bar.Label, slot - 4f), cx - slot / 2, plot.Bottom + 6f, slot, 30f,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    // ------------------------------------------------------------------ time series

    private void DrawSeries(ICanvas canvas, RectF rect, IReadOnlyList<ChartSeries> series)
    {
        var visible = series.Where(s => s.Points.Count > 0).ToList();
        bool legend = visible.Count > 1 || visible.Any(s => !string.IsNullOrEmpty(s.Name));
        bool labels = visible.Any(s => s.ShowLabels && s.Points.Any(p => !string.IsNullOrEmpty(p.Label)));

        // Legend layout (wrapping rows) is measured first so the plot can make room for it.
        float legendStart = rect.Left + 8f;
        float legendLimit = rect.Right - PadRight;
        int legendRows = 0;
        if (legend)
        {
            legendRows = 1;
            float x = legendStart;
            foreach (var s in visible)
            {
                float w = 14f + canvas.GetStringSize(s.Name, Font.Default, FontSize).Width;
                if (x > legendStart && x + w > legendLimit)
                {
                    legendRows++;
                    x = legendStart;
                }
                x += w + 18f;
            }
        }

        float top = rect.Top + PadTop + LegendRowHeight * legendRows + (labels ? LabelHeadroom : 0f);
        var plot = new RectF(rect.Left + PadLeft, top, rect.Width - PadLeft - PadRight, rect.Bottom - PadBottom - top);
        if (plot.Width <= 10 || plot.Height <= 10) return;

        double minX = visible.Min(s => s.Points.Min(p => p.X));
        double maxX = visible.Max(s => s.Points.Max(p => p.X));
        double minY = Math.Min(0, visible.Min(s => s.Points.Min(p => p.Y)));
        double maxY = Math.Max(0, visible.Max(s => s.Points.Max(p => p.Y)));

        if (maxX - minX < 1e-9)
        {
            double pad = _view.XAxisIsTime ? 1800 : 1;
            minX -= pad;
            maxX += pad;
        }
        else
        {
            double pad = (maxX - minX) * 0.03;
            minX -= pad;
            maxX += pad;
        }

        if (maxY - minY < 1e-9) maxY = minY + 1;
        var (nMin, nMax, step) = NiceScale(minY, maxY, 5, MinStep());

        float X(double x) => (float)(plot.Left + (x - minX) / (maxX - minX) * plot.Width);
        float Y(double y) => (float)(plot.Bottom - (y - nMin) / (nMax - nMin) * plot.Height);

        DrawHorizontalGrid(canvas, rect, plot, nMin, nMax, step, Y);

        // X axis labels
        int xTicks = Math.Max(2, Math.Min(6, (int)(plot.Width / 70f)));
        double span = maxX - minX;
        for (int i = 0; i < xTicks; i++)
        {
            double x = minX + span * i / (xTicks - 1);
            float px = X(x);
            canvas.StrokeColor = _view.GridColor;
            canvas.StrokeSize = 1f;
            canvas.DrawLine(px, plot.Bottom, px, plot.Bottom + 4f);

            var align = i == 0 ? HorizontalAlignment.Left
                : i == xTicks - 1 ? HorizontalAlignment.Right
                : HorizontalAlignment.Center;
            canvas.FontColor = _view.TextColor;
            canvas.FontSize = FontSize;
            canvas.DrawString(FormatX(x, span), px, plot.Bottom + 18f, align);
        }

        float zeroY = Y(0);
        var ordered = visible.Select(s => (Series: s, Points: s.Points.OrderBy(p => p.X).ToList())).ToList();

        canvas.SaveState();
        float clipTop = plot.Top - 8f - (labels ? LabelHeadroom : 0f);
        canvas.ClipRectangle(plot.Left - 8f, clipTop, plot.Width + 16f, plot.Bottom + 8f - clipTop);

        // Pass 1: area fills, so no fill can cover another series' stroke.
        foreach (var (s, pts) in ordered)
        {
            if (!s.Fill || s.Kind is SeriesKind.Bars or SeriesKind.Scatter || pts.Count < 2) continue;
            var fill = BuildPath(s, pts, X, Y);
            fill.LineTo(X(pts[^1].X), zeroY);
            fill.LineTo(X(pts[0].X), zeroY);
            fill.Close();
            canvas.FillColor = s.Color.WithAlpha(0.15f);
            canvas.FillPath(fill);
        }

        // Pass 2: bars, strokes and markers in list order (last series ends up on top).
        foreach (var (s, pts) in ordered)
        {
            switch (s.Kind)
            {
                case SeriesKind.Bars:
                {
                    const float w = 8f;
                    foreach (var p in pts)
                    {
                        float x = X(p.X);
                        float y = Y(p.Y);
                        canvas.FillColor = p.Y >= 0 ? s.Color : s.NegativeColor;
                        canvas.FillRoundedRectangle(x - w / 2, Math.Min(y, zeroY), w, Math.Max(2f, Math.Abs(y - zeroY)), 2f);
                    }
                    break;
                }
                case SeriesKind.Scatter:
                    break; // markers only, drawn below
                default:
                {
                    canvas.StrokeColor = s.Color;
                    canvas.StrokeSize = s.StrokeSize;
                    canvas.StrokeLineJoin = LineJoin.Round;
                    canvas.StrokeLineCap = LineCap.Round;
                    canvas.DrawPath(BuildPath(s, pts, X, Y));
                    break;
                }
            }

            if (s.ShowMarkers || s.Kind == SeriesKind.Scatter)
            {
                float radius = s.Kind == SeriesKind.Scatter ? 5f : 3.5f;
                foreach (var p in pts)
                {
                    canvas.FillColor = p.Y >= 0 || s.Kind is SeriesKind.Line or SeriesKind.Step ? s.Color : s.NegativeColor;
                    canvas.FillCircle(X(p.X), Y(p.Y), radius);
                }
            }
        }
        canvas.RestoreState();

        // Point labels are drawn outside the clip so the top-most label is never cut off.
        if (labels)
        {
            canvas.FontSize = LabelFontSize;
            canvas.FontColor = _view.TextColor;
            foreach (var (s, pts) in ordered)
            {
                if (!s.ShowLabels) continue;
                foreach (var p in pts)
                {
                    if (string.IsNullOrEmpty(p.Label)) continue;

                    // Long names (menu items) are truncated first, so the centre always fits into the view.
                    var text = Truncate(canvas, p.Label, rect.Width - 4f, LabelFontSize);
                    float half = canvas.GetStringSize(text, Font.Default, LabelFontSize).Width / 2f;
                    float lo = rect.Left + half + 2f;
                    float hi = rect.Right - half - 2f;
                    float lx = lo <= hi ? Math.Clamp(X(p.X), lo, hi) : rect.Center.X;
                    canvas.DrawString(text, lx, Y(p.Y) - 9f, HorizontalAlignment.Center);
                }
            }
            canvas.FontSize = FontSize;
        }

        // Legend (wrapping)
        if (legend)
        {
            float lx = legendStart;
            float ly = rect.Top + PadTop;
            canvas.FontSize = FontSize;
            canvas.FontColor = _view.TextColor;
            foreach (var s in visible)
            {
                float w = 14f + canvas.GetStringSize(s.Name, Font.Default, FontSize).Width;
                if (lx > legendStart && lx + w > legendLimit)
                {
                    lx = legendStart;
                    ly += LegendRowHeight;
                }
                canvas.FillColor = s.Color;
                canvas.FillRoundedRectangle(lx, ly, 10f, 10f, 2f);
                canvas.DrawString(s.Name, lx + 14f, ly + 9f, HorizontalAlignment.Left);
                lx += w + 18f;
            }
        }
    }

    private static PathF BuildPath(ChartSeries s, List<ChartPoint> pts, Func<double, float> X, Func<double, float> Y)
    {
        var path = new PathF();
        for (int i = 0; i < pts.Count; i++)
        {
            float x = X(pts[i].X);
            float y = Y(pts[i].Y);
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else if (s.Kind == SeriesKind.Step)
            {
                path.LineTo(x, Y(pts[i - 1].Y));
                path.LineTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }
        return path;
    }

    // ------------------------------------------------------------------ helpers

    private void DrawHorizontalGrid(ICanvas canvas, RectF rect, RectF plot, double nMin, double nMax, double step,
        Func<double, float> y)
    {
        canvas.StrokeSize = 1f;
        canvas.FontSize = FontSize;
        canvas.FontColor = _view.TextColor;

        int lines = 0;
        for (double v = nMin; v <= nMax + step / 2 && lines < MaxGridLines; v += step, lines++)
        {
            float py = y(v);
            canvas.StrokeColor = Math.Abs(v) < step / 1000 ? _view.TextColor.WithAlpha(0.55f) : _view.GridColor;
            canvas.DrawLine(plot.Left, py, plot.Right, py);
            canvas.DrawString(FormatValue(v, step), rect.Left, py - 8f, PadLeft - 8f, 16f,
                HorizontalAlignment.Right, VerticalAlignment.Center);
        }
    }

    /// <summary>Minimum grid step: explicit, or 1 when the value format cannot show decimals.</summary>
    private double MinStep()
    {
        if (_view.MinStep > 0) return _view.MinStep;
        var format = (_view.ValueFormat ?? string.Empty).Trim().ToUpperInvariant();
        return format is "N0" or "F0" or "D" or "0" or "#" ? 1 : 0;
    }

    /// <summary>
    /// Formats a grid/bar value. Only ever adds decimals: when the grid step is finer than the
    /// configured format can show, the format is widened so neighbouring labels cannot collapse
    /// into the same text. A format that is already precise enough is kept unchanged.
    /// </summary>
    private string FormatValue(double v, double step)
    {
        var format = _view.ValueFormat;
        if (step >= 1 || step <= 0 || double.IsNaN(step))
            return v.ToString(format, CultureInfo.CurrentCulture);

        int stepDecimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 1, 4);
        int formatDecimals = DecimalsOf(format);
        if (formatDecimals < 0 || formatDecimals >= stepDecimals)
            return v.ToString(format, CultureInfo.CurrentCulture);

        return v.ToString("N" + stepDecimals, CultureInfo.CurrentCulture);
    }

    /// <summary>Decimals a standard numeric format such as "N0" or "C2" asks for, or -1 when unknown.</summary>
    private static int DecimalsOf(string? format)
    {
        if (string.IsNullOrEmpty(format) || format.Length < 2) return -1;
        return int.TryParse(format.AsSpan(1), out var decimals) ? decimals : -1;
    }

    private string FormatX(double x, double span)
    {
        if (!_view.XAxisIsTime) return x.ToString("N0", CultureInfo.CurrentCulture);

        var dt = DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(x)).ToLocalTime();
        if (span <= 36 * 3600) return dt.ToString("HH:mm");
        if (span <= 400 * 86400) return dt.ToString("dd.MM");
        return dt.ToString("MM.yy");
    }

    private static string Truncate(ICanvas canvas, string text, float maxWidth, float fontSize = FontSize)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (canvas.GetStringSize(text, Font.Default, fontSize).Width <= maxWidth) return text;

        for (int len = text.Length - 1; len > 0; len--)
        {
            var candidate = text[..len] + "…";
            if (canvas.GetStringSize(candidate, Font.Default, fontSize).Width <= maxWidth)
                return candidate;
        }
        return "…";
    }

    private static (double Min, double Max, double Step) NiceScale(double min, double max, int maxTicks, double minStep)
    {
        double range = NiceNumber(max - min, false);
        double step = Math.Max(minStep, NiceNumber(range / Math.Max(1, maxTicks - 1), true));
        double niceMin = Math.Floor(min / step) * step;
        double niceMax = Math.Ceiling(max / step) * step;
        if (niceMax - niceMin < step) niceMax = niceMin + step;
        return (niceMin, niceMax, step);
    }

    private static double NiceNumber(double range, bool round)
    {
        if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range)) return 1;

        double exponent = Math.Floor(Math.Log10(range));
        double fraction = range / Math.Pow(10, exponent);
        double nice;
        if (round)
            nice = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        else
            nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * Math.Pow(10, exponent);
    }
}
