using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace ProgressBars.Avalonia;

/// <summary>One shared frame timer for every animated control; it only runs while something is on screen.</summary>
internal static class Ticker
{
    static readonly List<Action<double>> Subscribers = new();
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static DispatcherTimer? timer;
    static double last;

    public static void Add(Action<double> subscriber)
    {
        Subscribers.Add(subscriber);
        if (timer == null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => OnTick();
        }
        if (!timer.IsEnabled)
        {
            last = Clock.Elapsed.TotalSeconds;
            timer.Start();
        }
    }

    public static void Remove(Action<double> subscriber)
    {
        Subscribers.Remove(subscriber);
        if (Subscribers.Count == 0) timer?.Stop();
    }

    static void OnTick()
    {
        var now = Clock.Elapsed.TotalSeconds;
        var dt = Math.Min(0.1, now - last);
        last = now;
        foreach (var subscriber in Subscribers.ToArray()) subscriber(dt);
    }
}

internal abstract class AnimatedControl : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Ticker.Add(Tick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Ticker.Remove(Tick);
        base.OnDetachedFromVisualTree(e);
    }

    void Tick(double dt)
    {
        Update(dt);
        InvalidateVisual();
    }

    protected abstract void Update(double dt);
}

/// <summary>A fixed-size control that draws itself with a delegate.</summary>
internal sealed class Painted : Control
{
    readonly Action<DrawingContext, Rect> paint;

    public Painted(double width, double height, Action<DrawingContext, Rect> paint)
    {
        Width = width;
        Height = height;
        this.paint = paint;
    }

    public override void Render(DrawingContext context) => paint(context, new Rect(Bounds.Size));
}

internal static class Ui
{
    public static TextBlock Label(string text, double size, string hex = "#ffffff", bool bold = true) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        Foreground = Art.B(hex),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Color Shade(Color c, double amount)
    {
        byte Mix(byte v) => (byte)Math.Clamp(amount >= 0 ? v + (255 - v) * amount : v * (1 + amount), 0, 255);
        return Color.FromArgb(c.A, Mix(c.R), Mix(c.G), Mix(c.B));
    }
}

/// <summary>
/// A self-drawn button, so the game looks the same whatever theme the host app uses.
/// <see cref="IsDown"/> stays true for as long as the pointer is held on it.
/// </summary>
internal sealed class GameButton : Border
{
    static readonly Cursor Hand = new(StandardCursorType.Hand);
    readonly Color color;
    bool hover, selected;

    public bool IsDown { get; private set; }
    public event Action? Click;

    public GameButton(Control content, string color = "#3d4f7a")
    {
        this.color = Color.Parse(color);
        Child = content;
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(14, 8);
        BorderThickness = new Thickness(2);
        Cursor = Hand;
        Apply();
    }

    public GameButton(string text, double size = 16, string color = "#3d4f7a")
        : this(Centered(Ui.Label(text, size)), color)
    {
    }

    static TextBlock Centered(TextBlock label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    public bool Selected
    {
        set
        {
            selected = value;
            Apply();
        }
    }

    void Apply()
    {
        var fill = IsDown ? Ui.Shade(color, -0.2) : hover ? Ui.Shade(color, 0.14) : color;
        Background = new ImmutableSolidColorBrush(fill);
        BorderBrush = selected ? Art.B("#fbbf24") : new ImmutableSolidColorBrush(Ui.Shade(color, 0.22));
        RenderTransform = IsDown ? new TranslateTransform(0, 2) : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        IsDown = true;
        e.Pointer.Capture(this);
        e.Handled = true;
        Apply();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsDown) return;
        IsDown = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        Apply();
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        IsDown = false;
        Apply();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        hover = true;
        Apply();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        hover = false;
        Apply();
    }
}

internal sealed class SpinningCoin : AnimatedControl
{
    double phase, pulse;

    /// <summary>Briefly swells the coin, used when a flying coin lands in the stash.</summary>
    public void Pulse() => pulse = 1;

    protected override void Update(double dt)
    {
        phase += dt * 3.2;
        pulse = Math.Max(0, pulse - dt * 4);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var r = Math.Min(size.Width, size.Height) / 2 * (0.8 + 0.2 * pulse);
        Art.Coin(context, new Point(size.Width / 2, size.Height / 2), r, phase);
    }
}

/// <summary>
/// The coin-flight animation shared by every mini game: coins arc from where they were earned into the
/// stash, or out of the stash to where they are spent.
/// </summary>
internal sealed class CoinOverlay : AnimatedControl
{
    sealed class Flight
    {
        public Point Place, Control;
        public double Delay, T, Spin;
        public long Amount;
        public bool Outbound;
    }

    const double Duration = 0.8;
    readonly List<Flight> flights = new();

    /// <summary>Coins already earned but still in the air, and so not yet shown in the stash.</summary>
    public long Pending { get; private set; }

    public Func<Point> Target { get; set; } = () => default;
    public event Action? Arrived;

    public CoinOverlay() => IsHitTestVisible = false;

    /// <summary>Flies earned coins from <paramref name="from"/> into the stash.</summary>
    public void Fly(Point from, long amount)
    {
        Add(from, amount, false);
        Pending += Math.Max(0, amount);
    }

    /// <summary>Flies spent coins out of the stash to <paramref name="to"/>.</summary>
    public void FlyOut(Point to, long amount) => Add(to, amount, true);

    void Add(Point from, long amount, bool outbound)
    {
        if (amount <= 0) return;
        var count = (int)Math.Min(amount, 12);
        for (var i = 0; i < count; i++)
        {
            var start = count == 1
                ? from
                : from + new Vector(Random.Shared.Next(-30, 31), Random.Shared.Next(-22, 23));
            flights.Add(new Flight
            {
                Place = start,
                Outbound = outbound,
                Control = start + new Vector(Random.Shared.Next(-70, 71), -110 - Random.Shared.Next(60)),
                Delay = i * 0.07,
                Spin = Random.Shared.NextDouble() * 3,
                Amount = amount / count + (i < amount % count ? 1 : 0),
            });
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        flights.Clear();
        Pending = 0;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void Update(double dt)
    {
        for (var i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i];
            if (f.Delay > 0)
            {
                f.Delay -= dt;
                continue;
            }
            f.T += dt / Duration;
            if (f.T < 1) continue;
            flights.RemoveAt(i);
            if (f.Outbound) continue;
            Pending -= f.Amount;
            Arrived?.Invoke();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (flights.Count == 0) return;
        var target = Target();
        foreach (var f in flights)
        {
            if (f.Delay > 0) continue;
            var e = f.T * f.T * (3 - 2 * f.T);
            var (start, end) = f.Outbound ? (target, f.Place) : (f.Place, target);
            var a = start + (f.Control - start) * e;
            var b = f.Control + (end - f.Control) * e;
            var p = a + (b - a) * e;
            Art.Coin(context, p, 15 - 3 * e, f.Spin + f.T * 9);
        }
    }
}
