using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Avalonia.ProgressBar;

internal enum Screen { Menu, Farming, Casino, Mining }

/// <summary>The game in its own window.</summary>
internal sealed class CoinGameWindow : Window
{
    public CoinGameWindow(GameState state)
    {
        Title = "Coin Game";
        Width = 960;
        Height = 640;
        MinWidth = 480;
        MinHeight = 320;
        Background = Art.B("#141826");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new CoinGameView(state);
    }

    // Use the host theme's Window template rather than looking for one keyed to this subclass.
    protected override Type StyleKeyOverride => typeof(Window);
}

/// <summary>The whole game: header, stash, the current screen and the coin-flight overlay.</summary>
internal sealed class CoinGameView : Decorator
{
    readonly Border content = new();
    readonly CoinOverlay overlay = new();
    readonly SpinningCoin stashCoin = new() { Width = 40, Height = 40 };
    readonly TextBlock stashText = Ui.Label("", 24, "#fde68a");
    readonly TextBlock title = Ui.Label("", 22);
    readonly GameButton back = new("<  Menu", 15) { Margin = new Thickness(16, 0, 0, 0) };
    readonly Border stash;

    internal GameState State { get; }

    /// <summary>Raised whenever coins or seeds change, including when a flying coin lands.</summary>
    internal event Action? DisplayChanged;

    public CoinGameView(GameState state)
    {
        State = state;
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.VerticalAlignment = VerticalAlignment.Center;
        back.Click += () => Navigate(Screen.Menu);
        title.HorizontalAlignment = HorizontalAlignment.Center;

        stash = new Border
        {
            Background = Art.B("#66000000"),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(6, 4, 18, 4),
            Margin = new Thickness(0, 0, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { stashCoin, stashText },
            },
        };

        var background = new Border { Background = Art.VGrad(("#232a44", 0), ("#141826", 1)) };
        Grid.SetRowSpan(background, 2);
        var header = new Panel { Children = { back, title, stash } };
        Grid.SetRow(content, 1);
        Grid.SetRowSpan(overlay, 2);

        var root = new Grid
        {
            Width = 960,
            Height = 640,
            RowDefinitions = new RowDefinitions("60,*"),
            Children = { background, header, content, overlay },
        };

        overlay.Target = () => stashCoin.TranslatePoint(new Point(20, 20), overlay) ?? new Point(900, 30);
        overlay.Arrived += () =>
        {
            stashCoin.Pulse();
            Refresh();
        };

        Child = new Viewbox { Child = root };
        Navigate(Screen.Menu);
    }

    /// <summary>What the stash shows: coins in mid-flight are not counted until they land.</summary>
    internal long DisplayedCoins => State.Coins - overlay.Pending;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        State.Changed += Refresh;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        State.Changed -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }

    void Refresh()
    {
        stashText.Text = DisplayedCoins.ToString("N0");
        DisplayChanged?.Invoke();
    }

    internal void Navigate(Screen screen)
    {
        var menu = screen == Screen.Menu;
        back.IsVisible = !menu;
        // The menu shows the balance itself; keep the stash laid out so in-flight coins still have a target.
        stash.Opacity = menu ? 0 : 1;
        title.Text = menu ? "" : screen.ToString();
        content.Child = screen switch
        {
            Screen.Farming => new FarmView(this),
            Screen.Casino => new CasinoView(this),
            Screen.Mining => new MiningView(this),
            _ => new MenuView(this),
        };
    }

    /// <summary>
    /// Awards coins: they are saved immediately, then fly from <paramref name="at"/> (in
    /// <paramref name="from"/>'s coordinates) into the stash, which counts up as each one lands.
    /// </summary>
    internal void FlyCoins(Visual from, Point at, long amount)
    {
        overlay.Fly(from.TranslatePoint(at, overlay) ?? new Point(480, 320), amount);
        State.AddCoins(amount);
    }

    /// <summary>
    /// Spends coins if the player can afford it, and shows them leaving the stash for
    /// <paramref name="at"/> (in <paramref name="to"/>'s coordinates).
    /// </summary>
    internal bool SpendCoins(Visual to, Point at, long amount)
    {
        if (!State.TrySpend(amount)) return false;
        overlay.FlyOut(to.TranslatePoint(at, overlay) ?? new Point(480, 320), amount);
        return true;
    }
}

/// <summary>The opening dialog: coin balance and the choice of how to earn more.</summary>
internal sealed class MenuView : Panel
{
    readonly CoinGameView host;
    readonly TextBlock count = Ui.Label("", 40, "#fde68a");

    public MenuView(CoinGameView host)
    {
        this.host = host;
        count.HorizontalAlignment = HorizontalAlignment.Center;

        var question = Ui.Label("How do you want to earn more coins?", 20, "#cbd5e1", false);
        question.HorizontalAlignment = HorizontalAlignment.Center;
        question.Margin = new Thickness(0, 14, 0, 0);

        Children.Add(new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 40),
            Children =
            {
                new SpinningCoin { Width = 130, Height = 130, HorizontalAlignment = HorizontalAlignment.Center },
                count,
                question,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 20,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children =
                    {
                        Card(Screen.Farming, "#3f7d3a", (ctx, r) => Art.Crop(ctx, CropKind.Radish, r)),
                        Card(Screen.Casino, "#8f2338", (ctx, r) => Art.Symbol(ctx, SlotSymbol.Seven, r)),
                        Card(Screen.Mining, "#7a5230", Art.Pickaxe),
                    },
                },
            },
        });
    }

    GameButton Card(Screen screen, string color, Action<DrawingContext, Rect> icon)
    {
        var name = Ui.Label(screen.ToString(), 22);
        name.HorizontalAlignment = HorizontalAlignment.Center;


        var card = new GameButton(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new Painted(84, 84, icon) { HorizontalAlignment = HorizontalAlignment.Center },
                name,
            },
        }, color)
        {
            Width = 210,
            Padding = new Thickness(14, 18),
        };
        card.Click += () => host.Navigate(screen);
        return card;
    }

    void Refresh()
    {
        var coins = host.DisplayedCoins;
        count.Text = coins == 1 ? "You have 1 coin" : $"You have {coins:N0} coins";
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
