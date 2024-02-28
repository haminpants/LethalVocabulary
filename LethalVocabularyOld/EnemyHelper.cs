using System.Collections.Generic;

namespace LethalVocabulary;

public static class EnemyHelper {
    public const string SnareFlea = "Snare Flea";
    public const string BunkerSpider = "Bunker Spider";
    public const string HoardingBug = "Hoarding Bug";
    public const string Bracken = "Bracken";
    public const string Thumper = "Thumper";
    public const string Hygrodere = "Hygrodere";
    public const string GhostGirl = "Ghost Girl";
    public const string SporeLizard = "Spore Lizard";
    public const string Nutcracker = "Nutcracker";
    public const string CoilHead = "Coil Head";
    public const string Jester = "Jester";
    public const string EyelessDog = "Eyeless Dog";
    public const string ForestKeeper = "Forest Keeper";
    public const string EarthLeviathan = "Earth Leviathan";
    public const string BaboonHawk = "Baboon Hawk";
    public const string Masked = "Masked";

    public static readonly HashSet<string> IgnoredEnemies = new() {
        "Red Locust Bees", "Manticoil", "Docile Locust Bees", "Lasso"
    };

    public static readonly Dictionary<string, string> MappedEnemyNames = new() {
        { "Centipede", SnareFlea },
        { "Bunker Spider", BunkerSpider },
        { "Hoarding bug", HoardingBug },
        { "Flowerman", Bracken },
        { "Crawler", Thumper },
        { "Blob", Hygrodere },
        { "Girl", GhostGirl },
        { "Puffer", SporeLizard },
        { "Nutcracker", Nutcracker },
        { "MouthDog", EyelessDog },
        { "ForestGiant", ForestKeeper },
        { "Earth Leviathan", EarthLeviathan },
        { "Baboon hawk", BaboonHawk },
        { "Spring", CoilHead },
        { "Jester", Jester },
        { "Masked", Masked }
    };

    public static HashSet<SpawnableEnemyWithRarity> GetEnemiesFromLevel (SelectableLevel level) {
        HashSet<SpawnableEnemyWithRarity> enemies = new();
        if (level == null) return enemies;

        enemies.UnionWith(level.Enemies);
        enemies.UnionWith(level.DaytimeEnemies);
        enemies.UnionWith(level.OutsideEnemies);
        foreach (string enemyName in IgnoredEnemies) enemies.RemoveWhere(e => e.enemyType.enemyName.Equals(enemyName));

        return enemies;
    }
}