using System.Collections.Generic;

namespace LethalVocabulary;

public enum Category {
    BaboonHawk,
    Bracken,
    BunkerSpider,
    CoilHead,
    EarthLeviathan,
    EyelessDog,
    ForestKeeper,
    GhostGirl,
    HoardingBug,
    Hygrodere,
    Jester,
    Masked,
    Nutcracker,
    SnareFlea,
    SporeLizard,
    Thumper
}

public static class CategoryHelper {
    public static readonly Dictionary<string, Category> EnemyCategories = new() {
        { "Baboon hawk", Category.BaboonHawk },
        { "Flowerman", Category.Bracken },
        { "Bunker Spider", Category.BunkerSpider },
        { "Spring", Category.CoilHead },
        { "Earth Leviathan", Category.EarthLeviathan },
        { "MouthDog", Category.EyelessDog },
        { "ForestGiant", Category.ForestKeeper },
        { "Girl", Category.GhostGirl },
        { "Hoarding bug", Category.HoardingBug },
        { "Blob", Category.Hygrodere },
        { "Jester", Category.Jester },
        { "Masked", Category.Masked },
        { "Nutcracker", Category.Nutcracker },
        { "Centipede", Category.SnareFlea },
        { "Puffer", Category.SporeLizard },
        { "Crawler", Category.Thumper }
    };

    public static readonly Dictionary<Category, string> CategoryToConfigWords = new() {
        { Category.BaboonHawk, Config.BaboonHawkWords.Value },
        { Category.Bracken, Config.BrackenWords.Value },
        { Category.BunkerSpider, Config.BunkerSpiderWords.Value },
        { Category.CoilHead, Config.CoilHeadWords.Value },
        { Category.EarthLeviathan, Config.EarthLeviathanWords.Value },
        { Category.EyelessDog, Config.EyelessDogWords.Value },
        { Category.ForestKeeper, Config.ForestKeeperWords.Value },
        { Category.GhostGirl, Config.GhostGirlWords.Value },
        { Category.HoardingBug, Config.HoardingBugWords.Value },
        { Category.Hygrodere, Config.HygrodereWords.Value },
        { Category.Jester, Config.JesterWords.Value },
        { Category.Masked, Config.MaskedWords.Value },
        { Category.Nutcracker, Config.NutcrackerWords.Value },
        { Category.SnareFlea, Config.SnareFleaWords.Value },
        { Category.SporeLizard, Config.SporeLizardWords.Value },
        { Category.Thumper, Config.ThumperWords.Value }
    };

    public static readonly Dictionary<Category, string> SpacedCategoryNames = new() {
        { Category.BaboonHawk, "Baboon Hawk" },
        { Category.BunkerSpider, "Bunker Spider" },
        { Category.CoilHead, "Coil Head" },
        { Category.EarthLeviathan, "Earth Leviathan" },
        { Category.EyelessDog, "Eyeless Dog" },
        { Category.ForestKeeper, "Forest Keeper" },
        { Category.GhostGirl, "Ghost Girl" },
        { Category.HoardingBug, "Hoarding Bug" },
        { Category.SnareFlea, "Snare Flea" },
        { Category.SporeLizard, "Spore Lizard" }
    };
}