using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Avalonia.ProgressBar;

internal enum SlotSymbol { Seven, Diamond, Bar, Bell, Horseshoe, Clover, Lemon, Cherry }

/// <summary>All game graphics, drawn as vectors so the package ships no image assets.</summary>
internal static class Art
{
    static readonly Dictionary<string, ImmutableSolidColorBrush> Brushes = new();
    static readonly Dictionary<(string, double), ImmutablePen> Pens = new();
    static readonly Typeface BoldFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    static readonly Typeface RegularFace = new(FontFamily.Default);

    public static ImmutableSolidColorBrush B(string hex)
    {
        if (!Brushes.TryGetValue(hex, out var brush))
            Brushes[hex] = brush = new ImmutableSolidColorBrush(Color.Parse(hex));
        return brush;
    }

    public static ImmutablePen P(string hex, double thickness)
    {
        if (!Pens.TryGetValue((hex, thickness), out var pen))
            Pens[(hex, thickness)] = pen =
                new ImmutablePen(B(hex), thickness, null, PenLineCap.Round, PenLineJoin.Round);
        return pen;
    }

    public static LinearGradientBrush VGrad(params (string Hex, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        };
        foreach (var (hex, offset) in stops)
            brush.GradientStops.Add(new GradientStop(Color.Parse(hex), offset));
        return brush;
    }

    public static Point Pt(double x, double y) => new(x, y);

    public static StreamGeometry Geo(Action<StreamGeometryContext> build)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open()) build(context);
        return geometry;
    }

    public static StreamGeometry Poly(params Point[] points) => Geo(c =>
    {
        c.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++) c.LineTo(points[i]);
        c.EndFigure(true);
    });

    /// <summary>Maps a 100x100 drawing space onto <paramref name="r"/>.</summary>
    public static DrawingContext.PushedState Box(DrawingContext ctx, Rect r) =>
        ctx.PushTransform(Matrix.CreateScale(r.Width / 100, r.Height / 100) * Matrix.CreateTranslation(r.X, r.Y));

    public static DrawingContext.PushedState Rotate(DrawingContext ctx, double degrees, Point pivot) =>
        ctx.PushTransform(Matrix.CreateTranslation(-pivot.X, -pivot.Y)
                          * Matrix.CreateRotation(degrees * Math.PI / 180)
                          * Matrix.CreateTranslation(pivot.X, pivot.Y));

    public static DrawingContext.PushedState Scale(DrawingContext ctx, double sx, double sy, Point pivot) =>
        ctx.PushTransform(Matrix.CreateTranslation(-pivot.X, -pivot.Y)
                          * Matrix.CreateScale(sx, sy)
                          * Matrix.CreateTranslation(pivot.X, pivot.Y));

    public static FormattedText FT(string text, double size, IBrush brush, bool bold = true) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, bold ? BoldFace : RegularFace, size, brush);

    /// <summary>Draws text vertically centred on <paramref name="at"/>; align is -1 left, 0 centre, 1 right.</summary>
    public static void Text(DrawingContext ctx, string text, Point at, double size, IBrush brush,
        bool bold = true, int align = 0)
    {
        var ft = FT(text, size, brush, bold);
        var x = align < 0 ? at.X : align > 0 ? at.X - ft.Width : at.X - ft.Width / 2;
        ctx.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    public static void Leaf(DrawingContext ctx, Point from, Point to, double width, IBrush fill, IPen? pen = null)
    {
        var d = to - from;
        var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (length < 0.01) return;
        var normal = new Vector(-d.Y / length * width, d.X / length * width);
        var mid = from + d * 0.5;
        ctx.DrawGeometry(fill, pen, Geo(c =>
        {
            c.BeginFigure(from, true);
            c.QuadraticBezierTo(mid + normal, to);
            c.QuadraticBezierTo(mid - normal, from);
            c.EndFigure(true);
        }));
    }

    // ---------------------------------------------------------------- coin

    /// <summary>A coin spinning about its vertical axis; <paramref name="phase"/> is the spin angle in radians.</summary>
    public static void Coin(DrawingContext ctx, Point c, double r, double phase)
    {
        var w = Math.Abs(Math.Cos(phase));
        ctx.DrawEllipse(B("#a16207"), null, c, Math.Max(r * w, r * 0.14), r);
        if (w <= 0.14) return;
        using (Scale(ctx, w, 1, c))
        {
            ctx.DrawEllipse(B("#f6c343"), P("#c98a0c", r * 0.1), c, r * 0.93, r * 0.93);
            ctx.DrawEllipse(null, P("#fde68a", r * 0.08), c, r * 0.68, r * 0.68);
            Text(ctx, "$", c, r * 1.05, B("#a16207"));
            ctx.DrawEllipse(B("#88ffffff"), null, Pt(c.X - r * 0.38, c.Y - r * 0.45), r * 0.16, r * 0.1);
        }
    }

    // ---------------------------------------------------------------- slot symbols

    public static void Symbol(DrawingContext ctx, SlotSymbol symbol, Rect r)
    {
        using var _ = Box(ctx, r);
        switch (symbol)
        {
            case SlotSymbol.Seven:
            {
                var ft = FT("7", 96, B("#e11d2e"));
                var g = ft.BuildGeometry(Pt(50 - ft.Width / 2, 50 - ft.Height / 2));
                if (g == null) break;
                ctx.DrawGeometry(null, P("#7f1d1d", 10), g);
                ctx.DrawGeometry(null, P("#fbbf24", 6), g);
                ctx.DrawGeometry(B("#e11d2e"), null, g);
                break;
            }
            case SlotSymbol.Cherry:
                ctx.DrawGeometry(null, P("#15803d", 4), Geo(c =>
                {
                    c.BeginFigure(Pt(58, 12), false);
                    c.QuadraticBezierTo(Pt(38, 28), Pt(32, 54));
                    c.EndFigure(false);
                    c.BeginFigure(Pt(58, 12), false);
                    c.QuadraticBezierTo(Pt(66, 34), Pt(68, 58));
                    c.EndFigure(false);
                }));
                Leaf(ctx, Pt(58, 12), Pt(88, 16), 9, B("#22c55e"), P("#15803d", 2));
                ctx.DrawEllipse(B("#dc2626"), P("#7f1d1d", 2.5), Pt(32, 70), 19, 19);
                ctx.DrawEllipse(B("#dc2626"), P("#7f1d1d", 2.5), Pt(68, 74), 19, 19);
                ctx.DrawEllipse(B("#aaffffff"), null, Pt(25, 63), 5, 4);
                ctx.DrawEllipse(B("#aaffffff"), null, Pt(61, 67), 5, 4);
                break;
            case SlotSymbol.Horseshoe:
            {
                var shoe = Geo(c =>
                {
                    c.BeginFigure(Pt(26, 16), false);
                    c.LineTo(Pt(26, 52));
                    c.ArcTo(Pt(74, 52), new Size(24, 24), 0, false, SweepDirection.CounterClockwise);
                    c.LineTo(Pt(74, 16));
                    c.EndFigure(false);
                });
                ctx.DrawGeometry(null, P("#475569", 20), shoe);
                ctx.DrawGeometry(null, P("#94a3b8", 15), shoe);
                ctx.DrawGeometry(null, P("#cbd5e1", 5), shoe);
                foreach (var (x, y) in new[] { (26, 26), (26, 44), (32, 64), (50, 76), (68, 64), (74, 44), (74, 26) })
                    ctx.DrawEllipse(B("#1e293b"), null, Pt(x, y), 2.4, 2.4);
                break;
            }
            case SlotSymbol.Clover:
                ctx.DrawGeometry(null, P("#166534", 5), Geo(c =>
                {
                    c.BeginFigure(Pt(50, 48), false);
                    c.QuadraticBezierTo(Pt(54, 78), Pt(66, 92));
                    c.EndFigure(false);
                }));
                foreach (var (x, y) in new[] { (33, 27), (67, 27), (33, 61), (67, 61) })
                    ctx.DrawEllipse(B("#22a34a"), P("#166534", 2.5), Pt(x, y), 18, 18);
                foreach (var (x, y) in new[] { (33, 27), (67, 27), (33, 61), (67, 61) })
                    ctx.DrawLine(P("#86efac", 2.5), Pt(50, 44), Pt(50 + (x - 50) * 0.75, 44 + (y - 44) * 0.75));
                ctx.DrawEllipse(B("#166534"), null, Pt(50, 44), 4, 4);
                break;
            case SlotSymbol.Bell:
                ctx.DrawEllipse(B("#b45309"), null, Pt(50, 86), 8, 8);
                ctx.DrawEllipse(B("#f59e0b"), P("#92400e", 2), Pt(50, 13), 6, 6);
                ctx.DrawGeometry(B("#fbbf24"), P("#92400e", 3), Geo(c =>
                {
                    c.BeginFigure(Pt(16, 74), true);
                    c.QuadraticBezierTo(Pt(30, 62), Pt(30, 42));
                    c.CubicBezierTo(Pt(30, 14), Pt(70, 14), Pt(70, 42));
                    c.QuadraticBezierTo(Pt(70, 62), Pt(84, 74));
                    c.EndFigure(true);
                }));
                ctx.DrawRectangle(B("#f59e0b"), P("#92400e", 2.5), new Rect(12, 71, 76, 9), 4.5, 4.5);
                ctx.DrawGeometry(null, P("#fef3c7", 4), Geo(c =>
                {
                    c.BeginFigure(Pt(40, 58), false);
                    c.QuadraticBezierTo(Pt(40, 32), Pt(48, 27));
                    c.EndFigure(false);
                }));
                break;
            case SlotSymbol.Bar:
                ctx.DrawRectangle(B("#111827"), P("#fbbf24", 5), new Rect(6, 28, 88, 44), 9, 9);
                Text(ctx, "BAR", Pt(50, 50), 32, B("#ffffff"));
                break;
            case SlotSymbol.Lemon:
                using (Rotate(ctx, -18, Pt(50, 54)))
                {
                    ctx.DrawEllipse(B("#facc15"), P("#a16207", 2.5), Pt(14, 54), 8, 7);
                    ctx.DrawEllipse(B("#facc15"), P("#a16207", 2.5), Pt(86, 54), 8, 7);
                    ctx.DrawEllipse(B("#fde047"), P("#a16207", 3), Pt(50, 54), 34, 26);
                    ctx.DrawEllipse(B("#99ffffff"), null, Pt(36, 42), 9, 5);
                }
                Leaf(ctx, Pt(60, 26), Pt(84, 10), 8, B("#22c55e"), P("#15803d", 2));
                break;
            case SlotSymbol.Diamond:
                ctx.DrawGeometry(B("#38bdf8"), null,
                    Poly(Pt(28, 20), Pt(72, 20), Pt(92, 42), Pt(50, 88), Pt(8, 42)));
                ctx.DrawGeometry(B("#bae6fd"), null, Poly(Pt(28, 20), Pt(72, 20), Pt(92, 42), Pt(8, 42)));
                ctx.DrawGeometry(B("#0ea5e9"), null, Poly(Pt(36, 42), Pt(64, 42), Pt(50, 88)));
                var facet = P("#0369a1", 2);
                ctx.DrawLine(facet, Pt(8, 42), Pt(92, 42));
                ctx.DrawLine(facet, Pt(28, 20), Pt(36, 42));
                ctx.DrawLine(facet, Pt(72, 20), Pt(64, 42));
                ctx.DrawLine(facet, Pt(36, 42), Pt(50, 88));
                ctx.DrawLine(facet, Pt(64, 42), Pt(50, 88));
                ctx.DrawLine(facet, Pt(50, 20), Pt(36, 42));
                ctx.DrawLine(facet, Pt(50, 20), Pt(64, 42));
                ctx.DrawGeometry(null, P("#0369a1", 3),
                    Poly(Pt(28, 20), Pt(72, 20), Pt(92, 42), Pt(50, 88), Pt(8, 42)));
                break;
        }
    }

    // ---------------------------------------------------------------- crops

    static readonly string Green = "#3fa34d", DarkGreen = "#1f6b2c";

    /// <summary>A young sprout; <paramref name="growth"/> 0..1 scales it up from the soil.</summary>
    public static void Sprout(DrawingContext ctx, Rect r, double growth)
    {
        using var _ = Box(ctx, r);
        var s = 0.55 + 0.45 * growth;
        using var __ = Scale(ctx, s, s, Pt(50, 90));
        ctx.DrawLine(P(DarkGreen, 5), Pt(50, 90), Pt(50, 58));
        Leaf(ctx, Pt(50, 62), Pt(24, 42), 11, B(Green), P(DarkGreen, 2));
        Leaf(ctx, Pt(50, 62), Pt(76, 42), 11, B(Green), P(DarkGreen, 2));
    }

    /// <summary>A leafy half-grown plant.</summary>
    public static void Plant(DrawingContext ctx, Rect r, double growth)
    {
        using var _ = Box(ctx, r);
        var s = 0.75 + 0.25 * growth;
        using var __ = Scale(ctx, s, s, Pt(50, 92));
        ctx.DrawLine(P(DarkGreen, 5), Pt(50, 92), Pt(50, 34));
        Leaf(ctx, Pt(50, 76), Pt(18, 62), 11, B(Green), P(DarkGreen, 2));
        Leaf(ctx, Pt(50, 76), Pt(82, 62), 11, B(Green), P(DarkGreen, 2));
        Leaf(ctx, Pt(50, 56), Pt(24, 34), 11, B("#56b85f"), P(DarkGreen, 2));
        Leaf(ctx, Pt(50, 56), Pt(76, 34), 11, B("#56b85f"), P(DarkGreen, 2));
        Leaf(ctx, Pt(50, 40), Pt(50, 8), 10, B("#6fcf78"), P(DarkGreen, 2));
    }

    public static void DeadPlant(DrawingContext ctx, Rect r)
    {
        using var _ = Box(ctx, r);
        ctx.DrawGeometry(null, P("#5b4526", 5), Geo(c =>
        {
            c.BeginFigure(Pt(48, 92), false);
            c.CubicBezierTo(Pt(46, 60), Pt(52, 40), Pt(72, 44));
            c.EndFigure(false);
        }));
        Leaf(ctx, Pt(50, 62), Pt(28, 82), 8, B("#8a6a3b"), P("#5b4526", 2));
        Leaf(ctx, Pt(60, 44), Pt(46, 26), 7, B("#8a6a3b"), P("#5b4526", 2));
        Leaf(ctx, Pt(72, 44), Pt(84, 70), 8, B("#75572f"), P("#5b4526", 2));
    }

    /// <summary>The fully grown crop.</summary>
    public static void Crop(DrawingContext ctx, CropKind kind, Rect r)
    {
        using var _ = Box(ctx, r);
        var leaf = B(Green);
        var leafPen = P(DarkGreen, 2);
        switch (kind)
        {
            case CropKind.Radish:
                Leaf(ctx, Pt(50, 46), Pt(26, 14), 9, leaf, leafPen);
                Leaf(ctx, Pt(50, 46), Pt(74, 14), 9, leaf, leafPen);
                Leaf(ctx, Pt(50, 46), Pt(50, 6), 9, B("#56b85f"), leafPen);
                ctx.DrawGeometry(B("#fbcfe8"), P("#9d174d", 2), Poly(Pt(42, 78), Pt(58, 78), Pt(50, 98)));
                ctx.DrawEllipse(B("#e11d74"), P("#9d174d", 2.5), Pt(50, 62), 23, 22);
                ctx.DrawEllipse(B("#88ffffff"), null, Pt(41, 54), 6, 4);
                break;
            case CropKind.Potato:
                using (Rotate(ctx, -12, Pt(50, 56)))
                {
                    ctx.DrawEllipse(B("#c9a063"), P("#7c5a2c", 3), Pt(50, 56), 38, 26);
                    ctx.DrawEllipse(B("#55ffffff"), null, Pt(36, 44), 10, 5);
                    foreach (var (x, y) in new[] { (30, 60), (48, 50), (62, 66), (72, 50), (44, 70) })
                        ctx.DrawEllipse(B("#7c5a2c"), null, Pt(x, y), 2.6, 1.8);
                }
                break;
            case CropKind.Watermelon:
                ctx.DrawGeometry(null, P(DarkGreen, 4), Geo(c =>
                {
                    c.BeginFigure(Pt(50, 26), false);
                    c.QuadraticBezierTo(Pt(52, 12), Pt(66, 12));
                    c.EndFigure(false);
                }));
                ctx.DrawEllipse(B("#3fae52"), null, Pt(50, 58), 40, 31);
                foreach (var dx in new[] { -25.0, -9, 9, 25 })
                    ctx.DrawGeometry(null, P("#14532d", 5), Geo(c =>
                    {
                        c.BeginFigure(Pt(50 + dx * 0.55, 30), false);
                        c.QuadraticBezierTo(Pt(50 + dx * 1.75, 58), Pt(50 + dx * 0.55, 86));
                        c.EndFigure(false);
                    }));
                ctx.DrawEllipse(null, P("#14532d", 3), Pt(50, 58), 40, 31);
                ctx.DrawEllipse(B("#66ffffff"), null, Pt(34, 42), 9, 4);
                break;
            case CropKind.Carrot:
                Leaf(ctx, Pt(50, 34), Pt(30, 6), 7, leaf, leafPen);
                Leaf(ctx, Pt(50, 34), Pt(70, 6), 7, leaf, leafPen);
                Leaf(ctx, Pt(50, 34), Pt(50, 2), 7, B("#56b85f"), leafPen);
                ctx.DrawGeometry(B("#f97316"), P("#c2410c", 2.5), Geo(c =>
                {
                    c.BeginFigure(Pt(30, 36), true);
                    c.QuadraticBezierTo(Pt(50, 24), Pt(70, 36));
                    c.LineTo(Pt(54, 92));
                    c.QuadraticBezierTo(Pt(50, 98), Pt(46, 92));
                    c.EndFigure(true);
                }));
                ctx.DrawLine(P("#c2410c", 2), Pt(38, 48), Pt(50, 50));
                ctx.DrawLine(P("#c2410c", 2), Pt(52, 62), Pt(61, 60));
                ctx.DrawLine(P("#c2410c", 2), Pt(44, 74), Pt(52, 76));
                break;
            case CropKind.Corn:
                ctx.DrawEllipse(B("#facc15"), P("#a16207", 2.5), Pt(50, 46), 18, 40);
                for (var row = 0; row < 7; row++)
                for (var col = -1; col <= 1; col++)
                    ctx.DrawEllipse(B("#ca8a04"), null, Pt(50 + col * 9, 16 + row * 9), 2.2, 2.2);
                Leaf(ctx, Pt(50, 96), Pt(16, 30), 12, B("#65a30d"), P("#3f6212", 2));
                Leaf(ctx, Pt(50, 96), Pt(84, 30), 12, B("#65a30d"), P("#3f6212", 2));
                break;
            case CropKind.Pumpkin:
                ctx.DrawRectangle(B("#4d7c0f"), P("#365314", 2), new Rect(45, 20, 10, 16), 3, 3);
                ctx.DrawEllipse(B("#ea580c"), P("#9a3412", 2.5), Pt(28, 62), 20, 26);
                ctx.DrawEllipse(B("#ea580c"), P("#9a3412", 2.5), Pt(72, 62), 20, 26);
                ctx.DrawEllipse(B("#f97316"), P("#9a3412", 2.5), Pt(50, 62), 21, 30);
                ctx.DrawEllipse(B("#66ffffff"), null, Pt(42, 46), 5, 8);
                break;
            case CropKind.Tomato:
                ctx.DrawEllipse(B("#ef4444"), P("#991b1b", 2.5), Pt(50, 58), 33, 31);
                ctx.DrawEllipse(B("#77ffffff"), null, Pt(36, 46), 8, 5);
                foreach (var (x, y) in new[] { (28, 30), (40, 18), (60, 18), (72, 30), (50, 44) })
                    Leaf(ctx, Pt(50, 28), Pt(x, y), 5, leaf, leafPen);
                break;
            case CropKind.Strawberry:
                ctx.DrawGeometry(B("#e11d48"), P("#881337", 2.5), Geo(c =>
                {
                    c.BeginFigure(Pt(20, 38), true);
                    c.CubicBezierTo(Pt(18, 16), Pt(82, 16), Pt(80, 38));
                    c.CubicBezierTo(Pt(80, 62), Pt(60, 88), Pt(50, 96));
                    c.CubicBezierTo(Pt(40, 88), Pt(20, 62), Pt(20, 38));
                    c.EndFigure(true);
                }));
                foreach (var (x, y) in new[] { (34, 42), (50, 38), (66, 42), (41, 56), (59, 56), (50, 70), (36, 68), (64, 68) })
                    ctx.DrawEllipse(B("#fde68a"), null, Pt(x, y), 2, 2.8);
                foreach (var (x, y) in new[] { (24, 26), (38, 8), (62, 8), (76, 26), (50, 4) })
                    Leaf(ctx, Pt(50, 24), Pt(x, y), 6, leaf, leafPen);
                break;
        }
    }

    /// <summary>A seed packet showing the crop it grows.</summary>
    public static void Packet(DrawingContext ctx, CropKind kind, Rect r)
    {
        using (Box(ctx, r))
        {
            ctx.DrawRectangle(B("#fef3c7"), P("#92400e", 4), new Rect(12, 4, 76, 92), 8, 8);
            ctx.DrawRectangle(B("#f59e0b"), null, new Rect(14, 6, 72, 14), 6, 6);
        }
        Crop(ctx, kind, new Rect(r.X + r.Width * 0.22, r.Y + r.Height * 0.28, r.Width * 0.56, r.Height * 0.56));
    }

    // ---------------------------------------------------------------- tools and icons

    static readonly Point CanPivot = new(60, 61), CanSpout = new(8, 33);

    /// <summary>A watering can with its spout on the left; negative angles tip it into a pour.</summary>
    public static void Can(DrawingContext ctx, Rect r, double degrees)
    {
        using var _ = Box(ctx, r);
        using var __ = Rotate(ctx, degrees, CanPivot);
        ctx.DrawGeometry(null, P("#1e3a8a", 5), Geo(c =>
        {
            c.BeginFigure(Pt(44, 44), false);
            c.ArcTo(Pt(78, 44), new Size(17, 16), 0, false, SweepDirection.Clockwise);
            c.EndFigure(false);
            c.BeginFigure(Pt(84, 50), false);
            c.ArcTo(Pt(84, 76), new Size(13, 13), 0, false, SweepDirection.Clockwise);
            c.EndFigure(false);
        }));
        ctx.DrawLine(P("#1e3a8a", 13), Pt(42, 68), CanSpout);
        ctx.DrawLine(P("#3b82f6", 8), Pt(42, 68), CanSpout);
        ctx.DrawEllipse(B("#93c5fd"), P("#1e3a8a", 2.5), CanSpout, 8, 8);
        ctx.DrawRectangle(B("#3b82f6"), P("#1e3a8a", 3), new Rect(38, 42, 46, 42), 8, 8);
        ctx.DrawRectangle(B("#93c5fd"), null, new Rect(45, 50, 6, 26), 3, 3);
    }

    /// <summary>Where the water leaves a can drawn with the same arguments.</summary>
    public static Point CanSpoutTip(Rect r, double degrees)
    {
        var a = degrees * Math.PI / 180;
        var d = CanSpout - CanPivot;
        var x = CanPivot.X + d.X * Math.Cos(a) - d.Y * Math.Sin(a);
        var y = CanPivot.Y + d.X * Math.Sin(a) + d.Y * Math.Cos(a);
        return new Point(r.X + x * r.Width / 100, r.Y + y * r.Height / 100);
    }

    public static void Drop(DrawingContext ctx, Point c, double size, IBrush brush, IPen? pen = null)
    {
        var top = Pt(c.X, c.Y - size);
        ctx.DrawGeometry(brush, pen, Geo(g =>
        {
            g.BeginFigure(top, true);
            g.CubicBezierTo(Pt(c.X + size * 0.95, c.Y + size * 0.2), Pt(c.X + size * 0.65, c.Y + size),
                Pt(c.X, c.Y + size));
            g.CubicBezierTo(Pt(c.X - size * 0.65, c.Y + size), Pt(c.X - size * 0.95, c.Y + size * 0.2), top);
            g.EndFigure(true);
        }));
    }

    public static void Pickaxe(DrawingContext ctx, Rect r)
    {
        using var _ = Box(ctx, r);
        ctx.DrawLine(P("#5b3a1a", 10), Pt(20, 88), Pt(66, 32));
        ctx.DrawLine(P("#a16207", 6), Pt(20, 88), Pt(66, 32));
        ctx.DrawGeometry(B("#cbd5e1"), P("#475569", 3), Geo(c =>
        {
            c.BeginFigure(Pt(30, 14), true);
            c.QuadraticBezierTo(Pt(78, 12), Pt(92, 60));
            c.QuadraticBezierTo(Pt(68, 34), Pt(30, 14));
            c.EndFigure(true);
        }));
    }

    public static void Arrow(DrawingContext ctx, Rect r)
    {
        using var _ = Box(ctx, r);
        ctx.DrawGeometry(B("#ffffff"), P("#1f2937", 5),
            Poly(Pt(28, 10), Pt(28, 80), Pt(45, 64), Pt(57, 92), Pt(69, 87), Pt(57, 60), Pt(80, 60)));
    }
}
