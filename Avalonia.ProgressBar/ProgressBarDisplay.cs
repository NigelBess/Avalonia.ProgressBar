using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Avalonia.ProgressBar;

public sealed class ProgressBarDisplay : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressBarDisplay, double>(nameof(Value));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<ProgressBarDisplay>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ProgressBarDisplay>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<ProgressBarDisplay>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ProgressBarDisplay>();

    public static readonly StyledProperty<bool> OnlyWhenReadyProperty =
        AvaloniaProperty.Register<ProgressBarDisplay, bool>(nameof(OnlyWhenReady));

    double phase;
    bool collapsed;

    static ProgressBarDisplay()
    {
        AffectsMeasure<ProgressBarDisplay>(ValueProperty, FontSizeProperty, FontFamilyProperty, FontWeightProperty,
            OnlyWhenReadyProperty);
        AffectsRender<ProgressBarDisplay>(ForegroundProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool OnlyWhenReady
    {
        get => GetValue(OnlyWhenReadyProperty);
        set => SetValue(OnlyWhenReadyProperty, value);
    }

    bool ShouldCollapse => OnlyWhenReady && !GlobalProgress.IsReady();

    double CoinSize => FontSize * 1.35;
    double Gap => FontSize * 0.3;

    FormattedText Number() => new(
        // Whole numbers show no decimals; fractional amounts show up to two.
        Value.ToString("#,0.##", CultureInfo.CurrentCulture),
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground);

    protected override Size MeasureOverride(Size availableSize)
    {
        collapsed = ShouldCollapse;
        if (collapsed) return default;
        var number = Number();
        return new Size(number.Width + Gap + CoinSize, Math.Max(number.Height, CoinSize));
    }

    public override void Render(DrawingContext context)
    {
        if (collapsed) return;
        var number = Number();
        var middle = Bounds.Height / 2;
        context.DrawText(number, new Point(0, middle - number.Height / 2));
        Art.Coin(context, new Point(number.Width + Gap + CoinSize / 2, middle), CoinSize / 2, phase);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Ticker.Add(Spin);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Ticker.Remove(Spin);
        base.OnDetachedFromVisualTree(e);
    }

    void Spin(double dt)
    {
        // Re-check the date as the app runs, so the control appears or collapses when the day changes.
        if (collapsed != ShouldCollapse) InvalidateMeasure();
        phase += dt * 3.2;
        InvalidateVisual();
    }
}
