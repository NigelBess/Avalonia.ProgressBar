using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using static Avalonia.ProgressBar.Art;

namespace Avalonia.ProgressBar;

internal enum FarmTool { Hand, WateringCan, Seed }

/// <summary>Buy seeds, plant them, water them in the 30-45 minute window and harvest after an hour.</summary>
internal sealed class FarmView : Panel
{
    const string WateringRule = "it was not watered between 30 and 45 minutes after planting";

    readonly CoinGameView host;
    readonly PlotField plot;
    readonly TextBlock info = Ui.Label("", 14, "#e2e8f0", false);
    readonly TextBlock toast = Ui.Label("", 14, "#fbbf24");
    readonly TextBlock deadBanner = Ui.Label("", 14, "#fca5a5");
    readonly GameButton handButton, canButton;
    readonly GameButton[] seedRows = new GameButton[GameState.CropCount];
    readonly TextBlock[] owned = new TextBlock[GameState.CropCount];
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public FarmView(CoinGameView host)
    {
        this.host = host;
        plot = new PlotField(host);
        plot.InfoChanged += text => info.Text = text;
        plot.Toast += ShowToast;
        plot.DeadCountChanged += dead => deadBanner.Text = dead == 0
            ? ""
            : $"{dead} {(dead == 1 ? "crop has" : "crops have")} died because {WateringRule}. Click a dead crop to clear it.";
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            toast.Text = "";
        };

        foreach (var text in new[] { info, toast, deadBanner })
        {
            text.TextWrapping = TextWrapping.Wrap;
            text.Width = PlotField.FieldWidth;
        }
        info.Height = 38;
        info.VerticalAlignment = VerticalAlignment.Top;

        var left = new StackPanel { Spacing = 6, Children = { plot, info, toast, deadBanner } };

        handButton = ToolButton("Hand", Arrow, () => SetTool(FarmTool.Hand));
        canButton = ToolButton("Watering can", (ctx, r) => Can(ctx, r, 0), () => SetTool(FarmTool.WateringCan));

        var side = new StackPanel
        {
            Width = 320,
            Spacing = 5,
            Children =
            {
                Ui.Label("TOOLS", 13, "#94a3b8"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { handButton, canButton } },
                new Border { Height = 6 },
                Ui.Label("SEED SHOP  -  1 coin per seed", 13, "#94a3b8"),
            },
        };
        foreach (var kind in Enum.GetValues<CropKind>()) side.Children.Add(SeedRow(kind));

        Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { left, side },
        });

        SetTool(FarmTool.Hand);
    }

    static GameButton ToolButton(string text, Action<DrawingContext, Rect> icon, Action click)
    {
        var button = new GameButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new Painted(34, 34, icon), Ui.Label(text, 14) },
        })
        {
            Padding = new Thickness(8, 4, 12, 4),
        };
        button.Click += click;
        return button;
    }

    GameButton SeedRow(CropKind kind)
    {
        var count = owned[(int)kind] = Ui.Label("", 12, "#cbd5e1", false);
        var names = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
            Children = { Ui.Label(kind.ToString(), 15), count },
        };
        Grid.SetColumn(names, 1);

        var buy = new GameButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children = { Ui.Label("Buy", 14), new Painted(18, 18, (ctx, r) => Coin(ctx, r.Center, 9, 0)), Ui.Label("1", 14) },
        }, "#3f7d3a")
        {
            Padding = new Thickness(10, 5),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(buy, 2);
        buy.Click += () =>
        {
            if (host.State.BuySeed(kind)) SetTool(FarmTool.Seed, kind);
            else ShowToast("Not enough coins. Each seed costs 1 coin.");
        };

        var row = seedRows[(int)kind] = new GameButton(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Children = { new Painted(36, 36, (ctx, r) => Packet(ctx, kind, r)), names, buy },
        }, "#2a3350")
        {
            Padding = new Thickness(8, 3),
        };
        row.Click += () => SetTool(FarmTool.Seed, kind);
        return row;
    }

    void SetTool(FarmTool tool, CropKind kind = default)
    {
        plot.SetTool(tool, kind);
        handButton.Selected = tool == FarmTool.Hand;
        canButton.Selected = tool == FarmTool.WateringCan;
        for (var i = 0; i < seedRows.Length; i++)
            seedRows[i].Selected = tool == FarmTool.Seed && i == (int)kind;
    }

    void ShowToast(string text)
    {
        toast.Text = text;
        toastTimer.Stop();
        toastTimer.Start();
    }

    void Refresh()
    {
        foreach (var kind in Enum.GetValues<CropKind>())
            owned[(int)kind].Text = $"{host.State.SeedCount(kind)} in your bag";
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
        toastTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}

/// <summary>The 6x8 plot. Crop ages come from the saved planting time and the real clock.</summary>
internal sealed class PlotField : AnimatedControl
{
    const double Tile = 66, Pitch = 70, TiltSeconds = 0.8, MaxTilt = -42, BubbleSeconds = 2.5;
    public const double FieldWidth = GameState.Cols * Pitch - (Pitch - Tile);

    readonly CoinGameView host;
    readonly GameState state;
    readonly List<(Point P, Vector V, double Life)> drops = new();
    FarmTool tool;
    CropKind seed;
    Point? pointer;
    Point canAnchor;
    double waterLandsAt;
    (string Text, int Tile, double Life) bubble;
    double time, tilt = -1, dropTimer;
    string lastInfo = "";
    int lastDead = -1;

    public event Action<string>? InfoChanged;
    public event Action<string>? Toast;
    public event Action<int>? DeadCountChanged;

    public PlotField(CoinGameView host)
    {
        this.host = host;
        state = host.State;
        Width = FieldWidth;
        Height = GameState.Rows * Pitch - (Pitch - Tile);
    }

    public void SetTool(FarmTool newTool, CropKind kind)
    {
        tool = newTool;
        seed = kind;
        tilt = -1;
        drops.Clear();
    }

    static Rect TileRect(int index) => new(index % GameState.Cols * Pitch, index / GameState.Cols * Pitch, Tile, Tile);

    static int? TileAt(Point p)
    {
        int col = (int)Math.Floor(p.X / Pitch), row = (int)Math.Floor(p.Y / Pitch);
        if (col < 0 || col >= GameState.Cols || row < 0 || row >= GameState.Rows) return null;
        var index = row * GameState.Cols + col;
        return TileRect(index).Contains(p) ? index : null;
    }

    static string Clock(TimeSpan remaining)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    // ---------------------------------------------------------------- input

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        pointer = e.GetPosition(this);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        pointer = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        pointer = e.GetPosition(this);
        var clicked = TileAt(pointer.Value);
        if (tool == FarmTool.WateringCan)
        {
            // The can pours over the clicked tile and stays there until it has finished.
            tilt = 0;
            // Hold the can high enough that the spout pours onto the middle of the clicked tile.
            canAnchor = clicked is { } t ? TileRect(t).Center + new Vector(12, -24) : pointer.Value;
            waterLandsAt = (clicked is { } hit ? TileRect(hit).Center.Y : pointer.Value.Y) + 16;
        }
        if (clicked is not { } index) return;

        var now = DateTime.UtcNow;
        var tile = state.Tile(index);
        switch (state.Status(index, now))
        {
            case TileStatus.Ripe:
                state.Clear(index);
                host.FlyCoins(this, TileRect(index).Center, GameState.HarvestValue);
                break;
            case TileStatus.Dead:
                state.Clear(index);
                Toast?.Invoke($"That {tile!.Crop.ToString().ToLowerInvariant()} died because it was not watered " +
                              "between 30 and 45 minutes after planting. The plot is clear to replant.");
                break;
            case TileStatus.Empty when tool == FarmTool.Seed:
                if (!state.Plant(index, seed))
                    Toast?.Invoke($"You have no {seed.ToString().ToLowerInvariant()} seeds. Buy some in the seed shop for 1 coin each.");
                break;
            case TileStatus.Empty when tool == FarmTool.WateringCan:
                Toast?.Invoke("Nothing is planted there yet.");
                break;
            case TileStatus.Empty:
                Toast?.Invoke("Pick a seed in the seed shop, then click a plot to plant it.");
                break;
            case TileStatus.Thirsty when tool == FarmTool.WateringCan:
                state.Water(index);
                break;
            case TileStatus.Thirsty:
                Toast?.Invoke("This crop needs water. Click the watering can, then click the crop.");
                break;
            case TileStatus.Growing when tool == FarmTool.WateringCan:
                bubble = ("Doesn't need water right now", index, BubbleSeconds);
                break;
            case TileStatus.Watered when tool == FarmTool.WateringCan:
                bubble = ("Already watered", index, BubbleSeconds);
                break;
            case TileStatus.Growing or TileStatus.Watered when tool == FarmTool.Seed:
                Toast?.Invoke("Something is already growing there.");
                break;
        }
    }

    // ---------------------------------------------------------------- simulation

    protected override void Update(double dt)
    {
        time += dt;
        if (tilt >= 0)
        {
            tilt += dt / TiltSeconds;
            if (tilt >= 1) tilt = -1;
        }

        if (tilt is > 0.2 and < 0.8)
        {
            dropTimer -= dt;
            if (dropTimer <= 0)
            {
                dropTimer = 0.025;
                drops.Add((CanSpoutTip(CanRect(canAnchor), TiltAngle()),
                    new Vector(-35 + Random.Shared.Next(50), 50 + Random.Shared.Next(40)), 0.4));
            }
        }
        for (var i = drops.Count - 1; i >= 0; i--)
        {
            var (at, v, life) = drops[i];
            life -= dt;
            if (life <= 0 || at.Y >= waterLandsAt) drops.RemoveAt(i);
            else drops[i] = (at + v * dt, v + new Vector(0, 400 * dt), life);
        }

        bubble.Life = Math.Max(0, bubble.Life - dt);

        var now = DateTime.UtcNow;
        var dead = 0;
        for (var i = 0; i < GameState.Rows * GameState.Cols; i++)
            if (state.Status(i, now) == TileStatus.Dead) dead++;
        if (dead != lastDead) DeadCountChanged?.Invoke(lastDead = dead);

        var info = pointer is { } hover && TileAt(hover) is { } index ? Describe(index, now) : "";
        if (info != lastInfo) InfoChanged?.Invoke(lastInfo = info);
    }

    string Describe(int index, DateTime now)
    {
        var tile = state.Tile(index);
        if (tile == null)
            return tool == FarmTool.Seed
                ? $"Empty plot. Click to plant a {seed.ToString().ToLowerInvariant()} seed."
                : "Empty plot. Pick a seed in the seed shop, then click here to plant it.";

        var age = now - tile.PlantedUtc;
        return state.Status(index, now) switch
        {
            TileStatus.Growing =>
                $"{tile.Crop}: growing. It must be watered between the 30 and 45 minute mark. " +
                $"Watering opens in {Clock(GameState.WaterFrom - age)}.",
            TileStatus.Thirsty =>
                $"{tile.Crop}: thirsty! Water it within {Clock(GameState.WaterUntil - age)} or it will die.",
            TileStatus.Watered => $"{tile.Crop}: watered. Ready to harvest in {Clock(GameState.RipeAt - age)}.",
            TileStatus.Ripe => $"{tile.Crop}: ready! Click to harvest it for {GameState.HarvestValue} coins.",
            _ => $"{tile.Crop}: dead. It was not watered between 30 and 45 minutes after planting. Click to clear the plot.",
        };
    }

    double TiltAngle()
    {
        if (tilt < 0) return 0;
        static double Smooth(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);
        return MaxTilt * Smooth(tilt / 0.25) * Smooth((1 - tilt) / 0.25);
    }

    static Rect CanRect(Point p) => new(p.X - 8, p.Y - 46, 70, 70);

    // ---------------------------------------------------------------- drawing

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var now = DateTime.UtcNow;
        var hover = pointer is { } p ? TileAt(p) : null;
        for (var i = 0; i < GameState.Rows * GameState.Cols; i++) DrawTile(ctx, i, now, hover == i);

        if (tool == FarmTool.WateringCan)
        {
            foreach (var (drop, _, _) in drops) Drop(ctx, drop, 3.5, B("#7dd3fc"));
            // The full-size can only appears over a tile while it pours.
            if (tilt >= 0) Can(ctx, CanRect(canAnchor), TiltAngle());
            // Like the seed packet, a small can rides alongside the regular cursor.
            if (pointer is { } at) Can(ctx, new Rect(at.X + 10, at.Y + 8, 34, 34), 0);
        }
        else if (tool == FarmTool.Seed && pointer is { } at)
        {
            Packet(ctx, seed, new Rect(at.X + 12, at.Y + 14, 34, 34));
        }

        if (bubble.Life > 0) DrawBubble(ctx);
    }

    /// <summary>A short note pinned to a tile, e.g. why watering it did nothing.</summary>
    void DrawBubble(DrawingContext ctx)
    {
        var tile = TileRect(bubble.Tile);
        var text = FT(bubble.Text, 12.5, B("#ffffff"));
        double width = text.Width + 18, height = text.Height + 10;
        var x = Math.Clamp(tile.Center.X - width / 2, 0, Bounds.Width - width);
        // Above the tile, except on the top row where that would leave the field.
        var y = tile.Y >= height + 4 ? tile.Y - height - 4 : tile.Bottom + 4;
        using (ctx.PushOpacity(Math.Min(1, bubble.Life / 0.4)))
        {
            ctx.DrawRectangle(B("#f20f172a"), P("#7dd3fc", 1.5), new Rect(x, y, width, height), 8, 8);
            ctx.DrawText(text, Pt(x + 9, y + 5));
        }
    }

    void DrawTile(DrawingContext ctx, int index, DateTime now, bool hover)
    {
        var r = TileRect(index);
        var tile = state.Tile(index);
        var wet = tile?.WateredUtc != null;
        ctx.DrawRectangle(B(wet ? "#5a391b" : "#a8733f"), P(wet ? "#3b2410" : "#7c5128", 2), r, 8, 8);
        for (var k = 1; k <= 3; k++)
        {
            var y = r.Y + r.Height * k / 4;
            ctx.DrawLine(P(wet ? "#4a2d14" : "#946235", 2), Pt(r.X + 9, y), Pt(r.Right - 9, y));
        }

        if (tile != null)
        {
            var status = state.Status(index, now);
            var age = now - tile.PlantedUtc;
            var pulse = 0.5 + 0.5 * Math.Sin(time * 5);
            var art = new Rect(r.X + 10, r.Y + 3, 46, 46);
            // Every living crop shows one timer: the time left of the hour until harvest.
            string label = Clock(GameState.RipeAt - age), color = "#ffffff";
            switch (status)
            {
                case TileStatus.Growing:
                    Sprout(ctx, art, Math.Clamp(age / GameState.WaterFrom, 0, 1));
                    break;

                case TileStatus.Thirsty:
                    Plant(ctx, art, 0);
                    using (ctx.PushOpacity(0.35 + 0.65 * pulse))
                        ctx.DrawRectangle(null, P("#38bdf8", 3), r.Deflate(1.5), 7, 7);
                    Drop(ctx, Pt(r.Right - 12, r.Y + 13 + 2 * pulse), 6.5, B("#38bdf8"), P("#0c4a6e", 1.5));
                    color = "#7dd3fc";

                    break;
                case TileStatus.Watered:
                    Plant(ctx, art, Math.Clamp((age - GameState.WaterFrom) / (GameState.RipeAt - GameState.WaterFrom), 0, 1));
                    break;

                case TileStatus.Ripe:
                    using (ctx.PushOpacity(0.4 + 0.6 * pulse))
                        ctx.DrawRectangle(null, P("#fbbf24", 3), r.Deflate(1.5), 7, 7);
                    Crop(ctx, tile.Crop, art.Translate(new Vector(0, -2 * pulse)));
                    label = "HARVEST";
                    color = "#fde68a";
                    break;
                default:
                    DeadPlant(ctx, art);
                    label = "DEAD";
                    color = "#fca5a5";
                    break;
            }

            var labelAt = Pt(r.Center.X, r.Bottom - 9);
            Text(ctx, label, labelAt + new Vector(1, 1), 10.5, B("#cc000000"));
            Text(ctx, label, labelAt, 10.5, B(color));
        }

        if (hover) ctx.DrawRectangle(B("#22ffffff"), P("#ffffff", 2), r.Deflate(1), 8, 8);
    }
}
