using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProgressBars.Avalonia;

public static class ProgressUpdate
{
    public static bool Update(ProgressTracker tracker, long progress, Window owner)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(progress);

        if (tracker.State.TrySpend(progress)) return true;

        // Not enough coins: say so, and offer the game as the way to earn more. The purchase has
        // already failed, so the caller gets its answer now rather than waiting on the dialog.
        _ = new NotEnoughCoinsDialog(tracker, progress, owner).ShowDialog(owner);
        return false;
    }
}

/// <summary>Tells the player what an item costs and what they have, and offers to open the game.</summary>
internal sealed class NotEnoughCoinsDialog : Window
{
    public NotEnoughCoinsDialog(ProgressTracker tracker, long cost, Window owner)
    {
        Title = "Not enough coins";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Art.VGrad(("#232a44", 0), ("#141826", 1));

        var question = Ui.Label("Do you want to earn more coins?", 17, "#cbd5e1", false);
        question.HorizontalAlignment = HorizontalAlignment.Center;
        question.Margin = new Thickness(0, 10, 0, 4);

        var yes = new GameButton("Yes", 16, "#3f7d3a") { MinWidth = 110 };
        yes.Click += () =>
        {
            Close();
            tracker.Start(owner);
        };
        var no = new GameButton("No", 16) { MinWidth = 110 };
        no.Click += Close;

        Content = new StackPanel
        {
            Margin = new Thickness(36, 28),
            Spacing = 10,
            Children =
            {
                Amount("This costs", cost),
                Amount("You have", tracker.Progress.Value),
                question,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { yes, no },
                },
            },
        };
    }

    static Grid Amount(string label, long coins)
    {
        var amount = new ProgressBarDisplay
        {
            Value = coins,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = Art.B("#fde68a"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(amount, 1);
        return new Grid
        {
            MinWidth = 280,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { Ui.Label(label, 18), amount },
        };
    }

    // Use the host theme's Window template rather than looking for one keyed to this subclass.
    protected override Type StyleKeyOverride => typeof(Window);
}
