using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static ProgressBars.Avalonia.Art;

namespace ProgressBars.Avalonia;

/// <summary>Hold the button to mine: one coin for every ten uninterrupted seconds.</summary>
internal sealed class MiningView : Panel
{
    public MiningView(CoinGameView host)
    {
        var button = new GameButton("HOLD TO MINE", 22, "#b45309")
        {
            Width = 320,
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        MinerScene scene = null!;
        scene = new MinerScene(() => button.IsDown, at => host.FlyCoins(scene, at, 1));


        Children.Add(new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { scene, button },
        });
    }
}

internal sealed class MinerScene : AnimatedControl
{
    public const double SecondsPerCoin = 10;
    const double SwingPeriod = 0.75, RaisePortion = 0.7;
    const double IdleAngle = 78, StrikeAngle = 22, RaisedAngle = -110;
    static readonly Point RockCenter = new(460, 215), Impact = new(356, 240);

    readonly Func<bool> isHeld;
    readonly Action<Point> coinMined;
    readonly List<(Point P, Vector V, double Life)> sparks = new();
    readonly IBrush cave = VGrad(("#2a2740", 0), ("#15131f", 1));
    bool mining;
    double progress, swing, shake, time;

    public MinerScene(Func<bool> isHeld, Action<Point> coinMined)
    {
        this.isHeld = isHeld;
        this.coinMined = coinMined;
        Width = 640;
        Height = 360;
    }

    protected override void Update(double dt)
    {
        time += dt;
        shake = Math.Max(0, shake - dt * 6);
        mining = isHeld();
        if (mining)
        {
            var strikesBefore = Math.Floor(swing / SwingPeriod);
            swing += dt;
            if (Math.Floor(swing / SwingPeriod) > strikesBefore) Strike();

            progress += dt;
            if (progress >= SecondsPerCoin)
            {
                progress -= SecondsPerCoin;
                coinMined(RockCenter);
            }
        }
        else
        {
            // Letting go throws away the partial progress.
            progress = 0;
            swing = 0;
        }

        for (var i = sparks.Count - 1; i >= 0; i--)
        {
            var (p, v, life) = sparks[i];
            life -= dt;
            if (life <= 0) sparks.RemoveAt(i);
            else sparks[i] = (p + v * dt, v + new Vector(0, 500 * dt), life);
        }
    }

    void Strike()
    {
        shake = 1;
        for (var i = 0; i < 7; i++)
            sparks.Add((Impact,
                new Vector(-40 - Random.Shared.Next(120), -60 - Random.Shared.Next(160)),
                0.25 + Random.Shared.NextDouble() * 0.25));
    }

    double PickAngle()
    {
        if (!mining) return IdleAngle;
        var u = swing % SwingPeriod / SwingPeriod;
        if (u < RaisePortion)
        {
            var t = u / RaisePortion;
            return StrikeAngle + (RaisedAngle - StrikeAngle) * (1 - (1 - t) * (1 - t));
        }
        var d = (u - RaisePortion) / (1 - RaisePortion);
        return RaisedAngle + (StrikeAngle - RaisedAngle) * d * d;
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(Bounds.Size);
        using var clip = ctx.PushClip(new RoundedRect(bounds, 18));

        // Cave
        ctx.DrawRectangle(cave, null, bounds);
        foreach (var (x, w, h) in new[] { (40, 46, 70), (150, 30, 44), (250, 56, 92), (420, 40, 58), (540, 60, 84) })
            ctx.DrawGeometry(B("#0f0d17"), null, Poly(Pt(x, 0), Pt(x + w, 0), Pt(x + w / 2.0, h)));
        ctx.DrawRectangle(B("#3a2d28"), null, new Rect(0, 290, 640, 70));
        ctx.DrawRectangle(B("#4d3c34"), null, new Rect(0, 290, 640, 5));

        // Rock
        var jolt = shake * 3 * Math.Sin(time * 90);
        using (ctx.PushTransform(Matrix.CreateTranslation(jolt, 0)))
        {
            ctx.DrawGeometry(B("#6b7280"), P("#374151", 3), Poly(Pt(350, 291), Pt(344, 240), Pt(365, 190),
                Pt(410, 160), Pt(470, 150), Pt(530, 175), Pt(566, 225), Pt(572, 291)));
            ctx.DrawGeometry(B("#9ca3af"), null, Poly(Pt(367, 192), Pt(410, 162), Pt(432, 200), Pt(386, 226)));
            ctx.DrawGeometry(B("#4b5563"), null, Poly(Pt(470, 152), Pt(529, 177), Pt(502, 216), Pt(452, 196)));
            ctx.DrawGeometry(B("#565e6b"), null, Poly(Pt(520, 240), Pt(564, 228), Pt(570, 289), Pt(506, 289)));
            var nugget = 0;
            foreach (var (x, y) in new[] { (430, 244), (500, 256), (466, 208), (532, 224), (392, 262) })
            {
                var twinkle = 0.75 + 0.25 * Math.Sin(time * 3 + nugget++);
                ctx.DrawGeometry(B("#f6c343"), P("#a16207", 1.5),
                    Poly(Pt(x - 7, y), Pt(x - 2, y - 6), Pt(x + 6, y - 3), Pt(x + 7, y + 4), Pt(x - 3, y + 6)));
                ctx.DrawEllipse(B("#fff7c2"), null, Pt(x - 1, y - 2), 2 * twinkle, 2 * twinkle);
            }
        }

        DrawMiner(ctx);

        foreach (var (p, v, life) in sparks)
            ctx.DrawLine(P(life > 0.2 ? "#fff7c2" : "#fbbf24", 2.5), p, p - v * 0.03);

        DrawProgress(ctx);
    }

    void DrawMiner(DrawingContext ctx)
    {
        // A slow breathing bob while idle, a quicker one while working.
        var dy = mining ? Math.Sin(swing / SwingPeriod * 2 * Math.PI) * 1.5 : Math.Sin(time * 2) * 1.5;
        var skin = B("#f2c9a0");

        ctx.DrawEllipse(B("#55000000"), null, Pt(276, 292), 40, 6);
        ctx.DrawRectangle(B("#374151"), null, new Rect(258, 245, 13, 40));
        ctx.DrawRectangle(B("#374151"), null, new Rect(278, 245, 13, 40));
        ctx.DrawRectangle(B("#1f2937"), null, new Rect(254, 281, 20, 10), 4, 4);
        ctx.DrawRectangle(B("#1f2937"), null, new Rect(276, 281, 22, 10), 4, 4);

        ctx.DrawLine(P("#f2c9a0", 9), Pt(260, 208 + dy), Pt(254, 238 + dy));
        ctx.DrawRectangle(B("#2563eb"), P("#1e3a8a", 2), new Rect(252, 192 + dy, 46, 58), 10, 10);
        ctx.DrawRectangle(B("#92400e"), null, new Rect(252, 238 + dy, 46, 6));

        ctx.DrawEllipse(skin, P("#c99a6b", 1.5), Pt(276, 172 + dy), 17, 17);
        ctx.DrawEllipse(skin, P("#c99a6b", 1.5), Pt(293, 175 + dy), 3.5, 3.5);
        ctx.DrawEllipse(B("#1f2937"), null, Pt(284, 170 + dy), 2.2, 2.2);
        ctx.DrawLine(P("#7c4a21", 2), Pt(281, 181 + dy), Pt(289, 180 + dy));

        ctx.DrawGeometry(B("#facc15"), P("#a16207", 2), Geo(c =>
        {
            c.BeginFigure(Pt(257, 166 + dy), true);
            c.ArcTo(Pt(295, 166 + dy), new Size(19, 19), 0, false, SweepDirection.Clockwise);
            c.EndFigure(true);
        }));
        ctx.DrawRectangle(B("#eab308"), P("#a16207", 1.5), new Rect(254, 163 + dy, 48, 6), 3, 3);
        ctx.DrawEllipse(B("#33fff3a8"), null, Pt(300, 156 + dy), 15, 15);
        ctx.DrawEllipse(B("#fff7c2"), P("#a16207", 1.5), Pt(298, 157 + dy), 5, 5);

        // Front arm and pickaxe swing about the shoulder.
        var shoulder = Pt(282, 208 + dy);
        using (ctx.PushTransform(Matrix.CreateRotation(PickAngle() * Math.PI / 180)
                                 * Matrix.CreateTranslation(shoulder.X, shoulder.Y)))
        {
            ctx.DrawLine(P("#5b3a1a", 8), Pt(18, 0), Pt(88, 0));
            ctx.DrawLine(P("#a16207", 5), Pt(18, 0), Pt(88, 0));
            ctx.DrawGeometry(B("#cbd5e1"), P("#475569", 2), Geo(c =>
            {
                c.BeginFigure(Pt(80, -27), true);
                c.QuadraticBezierTo(Pt(100, 0), Pt(80, 27));
                c.QuadraticBezierTo(Pt(89, 0), Pt(80, -27));
                c.EndFigure(true);
            }));
            ctx.DrawLine(P("#1e3a8a", 13), Pt(0, 0), Pt(26, 0));
            ctx.DrawLine(P("#2563eb", 10), Pt(0, 0), Pt(26, 0));
            ctx.DrawEllipse(skin, P("#c99a6b", 1.5), Pt(31, 0), 6.5, 6.5);
        }
    }

    void DrawProgress(DrawingContext ctx)
    {
        var track = new Rect(140, 316, 360, 22);
        ctx.DrawRectangle(B("#0f0d17"), P("#6b7280", 2), track, 11, 11);
        var fraction = Math.Clamp(progress / SecondsPerCoin, 0, 1);
        if (fraction > 0)
        {
            using (ctx.PushClip(new RoundedRect(track.Deflate(3), 8)))
                ctx.DrawRectangle(B("#f6c343"), null,
                    new Rect(track.X, track.Y, track.Width * fraction, track.Height));
        }

    }
}
