using System.ComponentModel;

using Avalonia;
namespace ProgressBars.Avalonia;

public sealed class Progress : INotifyPropertyChanged
{
    static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));
    readonly GameState state;
    long coins;

    internal Progress(GameState state)
    {
        this.state = state;
        coins = state.Coins;
        state.Changed += Sync;
    }

    public long Value => coins;

    public event PropertyChangedEventHandler? PropertyChanged;

    void Sync()
    {
        if (state.Coins == coins) return;
        coins = state.Coins;
        PropertyChanged?.Invoke(this, ValueChanged);
    }
}