namespace CasinoTracker.Controls;

/// <summary>
/// Dependency-free chart control based on GraphicsView.
/// Set <see cref="Bars"/> for a categorical bar chart or <see cref="Series"/> for
/// time based line / step / bar / scatter series.
/// </summary>
public class ChartView : GraphicsView
{
    public static readonly BindableProperty SeriesProperty = BindableProperty.Create(
        nameof(Series), typeof(IReadOnlyList<ChartSeries>), typeof(ChartView), null, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty BarsProperty = BindableProperty.Create(
        nameof(Bars), typeof(IReadOnlyList<BarItem>), typeof(ChartView), null, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(ChartView), Colors.Gray, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty GridColorProperty = BindableProperty.Create(
        nameof(GridColor), typeof(Color), typeof(ChartView), Colors.LightGray, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty XAxisIsTimeProperty = BindableProperty.Create(
        nameof(XAxisIsTime), typeof(bool), typeof(ChartView), true, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty ValueFormatProperty = BindableProperty.Create(
        nameof(ValueFormat), typeof(string), typeof(ChartView), "N0", propertyChanged: OnVisualChanged);

    public static readonly BindableProperty EmptyTextProperty = BindableProperty.Create(
        nameof(EmptyText), typeof(string), typeof(ChartView), "No data yet", propertyChanged: OnVisualChanged);

    public static readonly BindableProperty MinStepProperty = BindableProperty.Create(
        nameof(MinStep), typeof(double), typeof(ChartView), 0d, propertyChanged: OnVisualChanged);

    public ChartView()
    {
        Drawable = new ChartDrawable(this);
        HeightRequest = 220;
    }

    public IReadOnlyList<ChartSeries>? Series
    {
        get => (IReadOnlyList<ChartSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public IReadOnlyList<BarItem>? Bars
    {
        get => (IReadOnlyList<BarItem>?)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    /// <summary>When true X values are Unix timestamps (seconds) and formatted as time.</summary>
    public bool XAxisIsTime
    {
        get => (bool)GetValue(XAxisIsTimeProperty);
        set => SetValue(XAxisIsTimeProperty, value);
    }

    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>
    /// Smallest allowed distance between two value grid lines. 0 = automatic:
    /// integer formats such as "N0" enforce a step of 1 so grid labels never repeat.
    /// </summary>
    public double MinStep
    {
        get => (double)GetValue(MinStepProperty);
        set => SetValue(MinStepProperty, value);
    }

    private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ChartView view)
            view.Invalidate();
    }
}
