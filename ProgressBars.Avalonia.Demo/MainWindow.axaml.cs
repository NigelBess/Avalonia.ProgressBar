using System;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ProgressBars.Avalonia.Demo
{
    public partial class MainWindow : Window
    {
        readonly ProgressTracker game = ProgressTracker.Initialize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Avalonia.ProgressBar"));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = game.Progress;
        }

        void StartGame(object? sender, RoutedEventArgs e) => game.Start(this);

        const long ItemCost = 100;

        // Spends the coins if there are enough; otherwise the library shows its own dialog.
        void BuySomething(object? sender, RoutedEventArgs e) =>
            BuyResult.Text = ProgressUpdate.Update(game, ItemCost, this) ? "Bought it!" : "";

        // The testing aids below use the library's internals, which only this demo can see.

        void AddCoins(object? sender, RoutedEventArgs e) => game.State.AddCoins(TagOf(sender));

        void AdvanceTime(object? sender, RoutedEventArgs e) =>
            game.State.AdvanceTime(TimeSpan.FromMinutes(TagOf(sender)));

        static int TagOf(object? sender) =>
            int.Parse((string)((Button)sender!).Tag!, CultureInfo.InvariantCulture);
    }
}