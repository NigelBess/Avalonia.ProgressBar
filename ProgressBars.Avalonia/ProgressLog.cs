namespace ProgressBars.Avalonia;

public static class ProgressLog
{
    public static string LogIncomplete(long currentProgress, long totalProgress, string operationName) =>
        $"You don't have enough coins for {operationName}. You have {Coins(currentProgress)}, " +
        $"but {operationName} requires {Coins(totalProgress)}.";

    static string Coins(long count) => count == 1 ? "1 coin" : $"{count:N0} coins";
}
