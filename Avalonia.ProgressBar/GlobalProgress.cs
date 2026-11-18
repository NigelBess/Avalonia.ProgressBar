using System;

namespace Avalonia.ProgressBar;

public static class GlobalProgress
{
    /// <summary>Where "now" comes from; replaceable so tests can pick the date.</summary>
    internal static Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    public static bool IsReady()
    {
        var today = Clock();
        return today is { Month: 4, Day: 1 };
    }
}
