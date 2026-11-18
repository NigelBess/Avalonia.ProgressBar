using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;

namespace Avalonia.ProgressBar;

public sealed class ProgressTracker
{
    static readonly Dictionary<string, ProgressTracker> Games = new(StringComparer.OrdinalIgnoreCase);
    CoinGameWindow? window;

    ProgressTracker(GameState state)
    {
        State = state;
        Progress = new Progress(state);
    }

    public static ProgressTracker Initialize(string saveDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        // Normalise so the same folder spelled differently (relative, trailing slash) is one game.
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(saveDirectory));
        if (!Games.TryGetValue(directory, out var game))
            Games[directory] = game = new ProgressTracker(GameState.Load(directory));
        return game;
    }

    public Progress Progress { get; }

    internal GameState State { get; }

    public void Start(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (window != null)
        {
            window.Activate();
            return;
        }

        var created = new CoinGameWindow(State);
        created.Closed += (_, _) => window = null;
        window = created;
        created.Show(owner);
    }
}
