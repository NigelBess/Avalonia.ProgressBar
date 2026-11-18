using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.ProgressBar;

internal enum CropKind { Radish, Potato, Watermelon, Carrot, Corn, Pumpkin, Tomato, Strawberry }

internal enum TileStatus { Empty, Growing, Thirsty, Watered, Ripe, Dead }

internal sealed class PlotTile
{
    public CropKind Crop { get; set; }
    public DateTime PlantedUtc { get; set; }
    public DateTime? WateredUtc { get; set; }
}

internal sealed class SaveData
{
    public long Coins { get; set; } = GameState.StartingCoins;
    public int[] Seeds { get; set; } = new int[GameState.CropCount];
    public PlotTile?[] Plots { get; set; } = new PlotTile?[GameState.Rows * GameState.Cols];
}

[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJson : JsonSerializerContext;

/// <summary>The player's coins, seeds and farm plot, persisted to disk after every change.</summary>
internal sealed class GameState
{
    public const int Rows = 6, Cols = 8, CropCount = 8;
    public const long StartingCoins = 50;
    public const int SeedPrice = 1, HarvestValue = 2;
    public static readonly TimeSpan WaterFrom = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan WaterUntil = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan RipeAt = TimeSpan.FromMinutes(60);

    readonly SaveData data;
    readonly string path;

    public event Action? Changed;

    GameState(SaveData data, string path)
    {
        this.data = data;
        this.path = path;
    }

    /// <summary>Loads the game saved in <paramref name="directory"/>, or starts a new one there.</summary>
    public static GameState Load(string directory)
    {
        var path = Path.Combine(directory, "save.json");
        SaveData? data = null;
        try
        {
            if (File.Exists(path))
                data = JsonSerializer.Deserialize(File.ReadAllText(path), SaveJson.Default.SaveData);
        }
        catch (Exception)
        {
            // A corrupt or unreadable save starts a fresh game.
        }

        data ??= new SaveData();
        if (data.Coins < 0) data.Coins = 0;
        if (data.Seeds is not { Length: CropCount }) data.Seeds = Resize(data.Seeds, CropCount);
        if (data.Plots is not { Length: Rows * Cols }) data.Plots = Resize(data.Plots, Rows * Cols);
        return new GameState(data, path);
    }

    static T[] Resize<T>(T[]? source, int length)
    {
        var result = new T[length];
        if (source != null) Array.Copy(source, result, Math.Min(source.Length, length));
        return result;
    }

    void Commit()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, SaveJson.Default.SaveData));
            File.Move(tmp, path, true);
        }
        catch (Exception)
        {
            // Saving is best effort; the game keeps running from memory.
        }
        Changed?.Invoke();
    }

    public long Coins => data.Coins;

    public void AddCoins(long amount)
    {
        data.Coins += amount;
        Commit();
    }

    public bool TrySpend(long amount)
    {
        if (amount < 0 || data.Coins < amount) return false;
        data.Coins -= amount;
        Commit();
        return true;
    }

    public int SeedCount(CropKind kind) => data.Seeds[(int)kind];

    public bool BuySeed(CropKind kind)
    {
        if (data.Coins < SeedPrice) return false;
        data.Coins -= SeedPrice;
        data.Seeds[(int)kind]++;
        Commit();
        return true;
    }

    public PlotTile? Tile(int index) => data.Plots[index];

    public TileStatus Status(int index, DateTime utcNow)
    {
        var tile = data.Plots[index];
        if (tile == null) return TileStatus.Empty;
        var age = utcNow - tile.PlantedUtc;
        if (tile.WateredUtc == null)
            return age < WaterFrom ? TileStatus.Growing : age < WaterUntil ? TileStatus.Thirsty : TileStatus.Dead;
        return age >= RipeAt ? TileStatus.Ripe : TileStatus.Watered;
    }

    public bool Plant(int index, CropKind kind)
    {
        if (data.Plots[index] != null || data.Seeds[(int)kind] <= 0) return false;
        data.Seeds[(int)kind]--;
        data.Plots[index] = new PlotTile { Crop = kind, PlantedUtc = DateTime.UtcNow };
        Commit();
        return true;
    }

    public bool Water(int index)
    {
        var now = DateTime.UtcNow;
        if (Status(index, now) != TileStatus.Thirsty) return false;
        data.Plots[index]!.WateredUtc = now;
        Commit();
        return true;
    }

    /// <summary>
    /// Testing aid: makes every planted crop older by <paramref name="by"/>, as if that much real time
    /// had passed. The shift is saved, so it survives a restart.
    /// </summary>
    public void AdvanceTime(TimeSpan by)
    {
        foreach (var tile in data.Plots)
        {
            if (tile == null) continue;
            tile.PlantedUtc -= by;
            if (tile.WateredUtc != null) tile.WateredUtc -= by;
        }
        Commit();
    }

    /// <summary>Removes whatever is on the tile (a harvested or dead crop).</summary>
    public void Clear(int index)
    {
        data.Plots[index] = null;
        Commit();
    }
}
