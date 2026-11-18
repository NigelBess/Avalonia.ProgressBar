using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static ProgressBars.Avalonia.Art;

namespace ProgressBars.Avalonia;

/// <summary>Reel strips and payouts. A symbol's weight is how many times it appears on each 32-stop reel.</summary>
internal static class Slots
{
    public const int Stops = 32;

    /// <summary>Any two matching symbols pay this multiple of the wager.</summary>
    public const int PairPays = 2;

    // With these weights 2.6% of spins are three of a kind and 37.9% are a pair, so about 4 spins in 10
    // win. Expected return is 0.952 of the wager (0.194 from triples + 0.759 from pairs); keep it below 1.
    public static readonly (SlotSymbol Symbol, int Weight, int Pays, string Name)[] Table =
    {
        (SlotSymbol.Seven, 1, 200, "sevens"),
        (SlotSymbol.Diamond, 2, 60, "diamonds"),
        (SlotSymbol.Bar, 3, 25, "bars"),
        (SlotSymbol.Bell, 4, 12, "bells"),
        (SlotSymbol.Horseshoe, 4, 12, "horseshoes"),
        (SlotSymbol.Clover, 5, 8, "clovers"),
        (SlotSymbol.Lemon, 6, 5, "lemons"),
        (SlotSymbol.Cherry, 7, 4, "cherries"),
    };

    public static readonly SlotSymbol[][] Strips = Enumerable.Range(0, 3).Select(reel =>
    {
        var strip = Table.SelectMany(row => Enumerable.Repeat(row.Symbol, row.Weight)).ToArray();
        new Random(7 + reel).Shuffle(strip);
        return strip;
    }).ToArray();

    /// <summary>Returns the wager multiplier and a description of the win, or 0 for a loss.</summary>
    public static (int Multiplier, string Name) Evaluate(SlotSymbol a, SlotSymbol b, SlotSymbol c)
    {
        string Name(SlotSymbol symbol) => Table.First(r => r.Symbol == symbol).Name;
        if (a == b && b == c) return (Table.First(r => r.Symbol == a).Pays, $"Three {Name(a)}");
        if (a == b || a == c) return (PairPays, $"Two {Name(a)}");
        return b == c ? (PairPays, $"Two {Name(b)}") : (0, "");
    }
}

internal sealed class CasinoView : Panel
{
    readonly CoinGameView host;
    readonly SlotMachine machine = new();
    readonly TextBlock wagerText = Ui.Label("", 20, "#fde68a");
    long wager = 1;

    public CasinoView(CoinGameView host)
    {
        this.host = host;
        machine.LeverPulled += Pull;
        wagerText.Width = 130;
        wagerText.TextAlignment = TextAlignment.Center;

        var wagerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Step("-10", -10), Step("-1", -1), wagerText, Step("+1", 1), Step("+10", 10),
                Step("MAX", long.MaxValue),
            },
        };

        Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new StackPanel { Spacing = 12, Children = { machine, wagerRow } } },
        });
    }

    GameButton Step(string text, long delta)
    {
        var button = new GameButton(text, 15, delta == long.MaxValue ? "#8f2338" : "#3d4f7a") { MinWidth = 54 };
        button.Click += () =>
        {
            if (machine.Spinning) return;
            wager = delta == long.MaxValue ? host.State.Coins : wager + delta;
            Refresh();
        };
        return button;
    }

    void Refresh()
    {
        // Never offer a wager the player cannot cover.
        wager = Math.Clamp(wager, 1, Math.Max(1, host.State.Coins));
        wagerText.Text = $"Wager: {wager:N0}";
    }

    void Pull()
    {
        if (machine.Spinning) return;
        Refresh();
        var bet = wager;
        if (!host.SpendCoins(machine, SlotMachine.CoinSlot, bet))
        {
            machine.Show("Not enough coins to wager!", false);
            return;
        }

        var stops = new[] { Random.Shared.Next(Slots.Stops), Random.Shared.Next(Slots.Stops), Random.Shared.Next(Slots.Stops) };
        machine.Show("Good luck!", false);
        machine.Spin(stops, () =>
        {
            var (multiplier, name) = Slots.Evaluate(
                Slots.Strips[0][stops[0]], Slots.Strips[1][stops[1]], Slots.Strips[2][stops[2]]);
            if (multiplier > 0)
            {
                var win = bet * multiplier;
                machine.Show($"{name}! You win {win:N0}", true);
                host.FlyCoins(machine, SlotMachine.ReelsCenter, win);
            }
            else
            {
                machine.Show("No luck. Pull again!", false);
            }
            Refresh();
        });
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host.DisplayChanged += Refresh;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        host.DisplayChanged -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }
}

/// <summary>The cabinet, reels and lever. It only animates; <see cref="CasinoView"/> decides the outcome.</summary>
internal sealed class SlotMachine : AnimatedControl
{
    const double Cell = 68, ReelWidth = 112, ReelTop = 50, ReelHeight = 204, CenterY = ReelTop + ReelHeight / 2;
    const double LeverDown = 0.2, LeverTotal = 0.65;
    public static readonly Point ReelsCenter = new(230, CenterY), CoinSlot = new(230, 348);
    static readonly Rect LeverZone = new(440, 36, 100, 270);
    static readonly Cursor Hand = new(StandardCursorType.Hand);

    readonly double[] pos = new double[3], from = new double[3], to = new double[3];
    readonly IBrush body = VGrad(("#c0262d", 0), ("#7f1d1d", 1));
    readonly IBrush reelShade = VGrad(("#aa000000", 0), ("#00000000", 0.28), ("#00000000", 0.72), ("#aa000000", 1));
    Action? done;
    double spinTime, lever = -1, time;
    string message = "Set your wager, then pull the lever!";
    bool win;

    public bool Spinning { get; private set; }
    public event Action? LeverPulled;

    public SlotMachine()
    {
        Width = 540;
        Height = 386;
        // Rest on triple sevens to show what the player is hoping for.
        for (var i = 0; i < 3; i++) pos[i] = Array.IndexOf(Slots.Strips[i], SlotSymbol.Seven);
    }

    public void Show(string text, bool isWin)
    {
        message = text;
        win = isWin;
    }

    static double Duration(int reel) => 1.3 + reel * 0.55;

    public void Spin(int[] stops, Action onDone)
    {
        for (var i = 0; i < 3; i++)
        {
            from[i] = pos[i];
            // Travel at least a couple of dozen symbols, then on to the chosen stop.
            var least = pos[i] + 24 + i * 10;
            to[i] = least + ((stops[i] - least) % Slots.Stops + Slots.Stops) % Slots.Stops;
        }
        spinTime = 0;
        done = onDone;
        Spinning = true;
    }

    protected override void Update(double dt)
    {
        time += dt;
        if (lever >= 0)
        {
            lever += dt;
            if (lever > LeverTotal) lever = -1;
        }
        if (!Spinning) return;

        spinTime += dt;
        for (var i = 0; i < 3; i++)
        {
            var t = Math.Min(1, spinTime / Duration(i));
            pos[i] = from[i] + (to[i] - from[i]) * (1 - Math.Pow(1 - t, 3));
        }
        if (spinTime < Duration(2)) return;

        for (var i = 0; i < 3; i++) pos[i] = to[i] % Slots.Stops;
        Spinning = false;
        var callback = done;
        done = null;
        callback?.Invoke();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!LeverZone.Contains(e.GetPosition(this))) return;
        if (lever < 0) lever = 0;
        LeverPulled?.Invoke();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = LeverZone.Contains(e.GetPosition(this)) ? Hand : Cursor.Default;
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var gold = P("#fbbf24", 4);

        // Lever (behind the cabinet edge)
        var pull = lever < 0 ? 0 : lever < LeverDown ? lever / LeverDown : 1 - (lever - LeverDown) / (LeverTotal - LeverDown);
        var knob = Pt(498 + 14 * Math.Sin(Math.PI * pull), 68 + 200 * pull);
        ctx.DrawRectangle(B("#4b5563"), P("#1f2937", 2), new Rect(446, 178, 30, 70), 8, 8);
        ctx.DrawLine(P("#1f2937", 12), Pt(466, 212), knob);
        ctx.DrawLine(P("#d1d5db", 7), Pt(466, 212), knob);
        ctx.DrawEllipse(B("#ef4444"), P("#7f1d1d", 3), knob, 19, 19);
        ctx.DrawEllipse(B("#99ffffff"), null, Pt(knob.X - 6, knob.Y - 7), 5, 4);

        // Cabinet
        ctx.DrawRectangle(body, gold, new Rect(10, 10, 440, 366), 26, 26);
        for (var i = 0; i < 12; i++)
        {
            var lit = ((int)(time * 3) + i) % 2 == 0;
            ctx.DrawEllipse(B(lit ? "#fde68a" : "#92400e"), null, Pt(40 + i * 34.5, 30), 4, 4);
        }

        // Reels
        ctx.DrawRectangle(B("#0b1020"), gold, new Rect(40, ReelTop - 6, 380, ReelHeight + 12), 10, 10);
        for (var reel = 0; reel < 3; reel++)
        {
            var area = new Rect(50 + reel * 124, ReelTop, ReelWidth, ReelHeight);
            using (ctx.PushClip(area))
            {
                ctx.DrawRectangle(B("#f8fafc"), null, area);
                var first = (int)Math.Floor(pos[reel]);
                var fraction = pos[reel] - first;
                for (var k = -2; k <= 2; k++)
                {
                    var symbol = Slots.Strips[reel][((first + k) % Slots.Stops + Slots.Stops) % Slots.Stops];
                    var y = CenterY - (k - fraction) * Cell;
                    Symbol(ctx, symbol, new Rect(area.X + (ReelWidth - 60) / 2, y - 30, 60, 60));
                }
                ctx.DrawRectangle(reelShade, null, area);
            }
        }

        // Pay line
        var lineHex = win && (int)(time * 6) % 2 == 0 ? "#fde68a" : "#ccef4444";
        ctx.DrawLine(P(lineHex, 3), Pt(44, CenterY), Pt(416, CenterY));
        ctx.DrawGeometry(B("#fbbf24"), null, Poly(Pt(30, CenterY - 9), Pt(46, CenterY), Pt(30, CenterY + 9)));
        ctx.DrawGeometry(B("#fbbf24"), null, Poly(Pt(430, CenterY - 9), Pt(414, CenterY), Pt(430, CenterY + 9)));

        // Message and coin tray
        ctx.DrawRectangle(B("#111827"), P("#fbbf24", 2), new Rect(40, 274, 380, 44), 10, 10);
        Text(ctx, message, Pt(230, 296), 18, B(win ? "#fde68a" : "#e2e8f0"));
        ctx.DrawRectangle(B("#450a0a"), P("#fbbf24", 2), new Rect(150, 334, 160, 28), 14, 14);
    }
}
