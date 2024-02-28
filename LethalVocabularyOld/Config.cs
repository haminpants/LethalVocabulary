using System.Collections.Generic;
using BepInEx.Configuration;
using static LethalVocabulary.EnemyHelper;

namespace LethalVocabulary;

public class Config {
    private static readonly string CustomWordTip =
        "Words must be separated by a comma (,). Capitalization does not matter.";

    // --- Gameplay
    public static ConfigEntry<int> SharedCategoriesPerMoon;
    public static ConfigEntry<bool> HideSharedCategories;
    public static ConfigEntry<int> PrivateCategoriesPerMoon;
    public static ConfigEntry<bool> HidePrivateCategories;
    public static ConfigEntry<bool> PunishCurseWords;

    // --- Categories
    // Entities
    public static ConfigEntry<string> SnareFleaWords;
    public static ConfigEntry<string> BunkerSpiderWords;
    public static ConfigEntry<string> HoardingBugWords;
    public static ConfigEntry<string> BrackenWords;
    public static ConfigEntry<string> ThumperWords;
    public static ConfigEntry<string> HygrodereWords;
    public static ConfigEntry<string> GhostGirlWords;
    public static ConfigEntry<string> SporeLizardWords;
    public static ConfigEntry<string> NutcrackerWords;
    public static ConfigEntry<string> CoilHeadWords;
    public static ConfigEntry<string> JesterWords;
    public static ConfigEntry<string> MaskedWords;
    public static ConfigEntry<string> EyelessDogWords;
    public static ConfigEntry<string> ForestKeeperWords;
    public static ConfigEntry<string> EarthLeviathanWords;

    public static ConfigEntry<string> BaboonHawkWords;

    // --- Misc
    public static ConfigEntry<bool> LogRecognitions;

    public Config (ConfigFile cfg) {
        SharedCategoriesPerMoon = cfg.Bind("Gameplay", "Shared Categories Per Moon",
            1,
            "The number of categories that will be picked and shared among all players each moon.\n" +
            "Setting this value to 0 will disable shared categories.");
        HideSharedCategories = cfg.Bind("Gameplay", "Hide Shared Categories",
            false,
            "Enabling this setting will prevent shared categories from being displayed.");
        PrivateCategoriesPerMoon = cfg.Bind("Gameplay", "Private Categories Per Moon",
            0,
            "The number of categories that will be picked privately and randomly for all players each moon.\n" +
            "Setting this value to 0 will disable private categories.");
        HidePrivateCategories = cfg.Bind("Gameplay", "Hide Private Categories",
            false,
            "Enabling this setting will prevent private categories from being displayed.");
        PunishCurseWords = cfg.Bind("Gameplay", "Punish Curse Words",
            false,
            "When enabled, players will be punished for using swear words.\n" +
            "This setting can be toggled mid-game with the chat command /lv_cursewords or /lv_cw");
        SnareFleaWords = cfg.Bind("Categories.Entities", SnareFlea,
            "snare,flea,fleas,centipede,centipedes",
            CategoryDescriptionFor(SnareFlea));
        BunkerSpiderWords = cfg.Bind("Categories.Entities", BunkerSpider,
            "bunker,spider,spiders,web,webs",
            CategoryDescriptionFor(BunkerSpider));
        HoardingBugWords = cfg.Bind("Categories.Entities", HoardingBug,
            "hoard,hoarding,bug,bugs,loot",
            CategoryDescriptionFor(HoardingBug));
        BrackenWords = cfg.Bind("Categories.Entities", Bracken,
            "bracken,flower",
            CategoryDescriptionFor(Bracken));
        ThumperWords = cfg.Bind("Categories.Entities", Thumper,
            "thumper,thumpers,crawler,crawlers",
            CategoryDescriptionFor(Thumper));
        HygrodereWords = cfg.Bind("Categories.Entities", Hygrodere,
            "hygrodere,hygroderes,slime,slimes",
            CategoryDescriptionFor(Hygrodere));
        GhostGirlWords = cfg.Bind("Categories.Entities", GhostGirl,
            "ghost,ghosts,girl,girls,haunt,haunted",
            CategoryDescriptionFor(GhostGirl));
        SporeLizardWords = cfg.Bind("Categories.Entities", SporeLizard,
            "spore,spores,lizard,lizards",
            CategoryDescriptionFor(SporeLizard));
        NutcrackerWords = cfg.Bind("Categories.Entities", Nutcracker,
            "nut,nuts,cracker,crackers,nutcracker,shotgun,gun,soldier",
            CategoryDescriptionFor(Nutcracker));
        CoilHeadWords = cfg.Bind("Categories.Entities", CoilHead,
            "coil,coils,head,heads",
            CategoryDescriptionFor(CoilHead));
        JesterWords = cfg.Bind("Categories.Entities", Jester,
            "jester,winding",
            CategoryDescriptionFor(Jester));
        MaskedWords = cfg.Bind("Categories.Entities", Masked,
            "mask,masked,mimic,mimics",
            CategoryDescriptionFor(Masked));
        EyelessDogWords = cfg.Bind("Categories.Entities", EyelessDog,
            "eye,eyes,eyeless,dog,dogs",
            CategoryDescriptionFor(EyelessDog));
        ForestKeeperWords = cfg.Bind("Categories.Entities", ForestKeeper,
            "forest,keeper,keepers,giant,giants",
            CategoryDescriptionFor(ForestKeeper));
        EarthLeviathanWords = cfg.Bind("Categories.Entities", EarthLeviathan,
            "earth,leviathan,leviathans,worm,worms",
            CategoryDescriptionFor(EarthLeviathan));
        BaboonHawkWords = cfg.Bind("Categories.Entities", BaboonHawk,
            "baboon,baboons,hawk,hawks",
            CategoryDescriptionFor(BaboonHawk));
        LogRecognitions = cfg.Bind("Misc", "Log Recognitions",
            true,
            "Enabling this setting will display recognitions in the console.");
    }

    public static Dictionary<string, HashSet<string>> GetAllCategories () {
        Dictionary<string, HashSet<string>> categories = new() {
            { SnareFlea, Plugin.ReadSeparatedString(SnareFleaWords.Value) },
            { BunkerSpider, Plugin.ReadSeparatedString(BunkerSpiderWords.Value) },
            { HoardingBug, Plugin.ReadSeparatedString(HoardingBugWords.Value) },
            { Bracken, Plugin.ReadSeparatedString(BrackenWords.Value) },
            { Thumper, Plugin.ReadSeparatedString(ThumperWords.Value) },
            { Hygrodere, Plugin.ReadSeparatedString(HygrodereWords.Value) },
            { GhostGirl, Plugin.ReadSeparatedString(GhostGirlWords.Value) },
            { SporeLizard, Plugin.ReadSeparatedString(SporeLizardWords.Value) },
            { Nutcracker, Plugin.ReadSeparatedString(NutcrackerWords.Value) },
            { CoilHead, Plugin.ReadSeparatedString(CoilHeadWords.Value) },
            { Jester, Plugin.ReadSeparatedString(JesterWords.Value) },
            { Masked, Plugin.ReadSeparatedString(MaskedWords.Value) },
            { EyelessDog, Plugin.ReadSeparatedString(EyelessDogWords.Value) },
            { ForestKeeper, Plugin.ReadSeparatedString(ForestKeeperWords.Value) },
            { EarthLeviathan, Plugin.ReadSeparatedString(EarthLeviathanWords.Value) },
            { BaboonHawk, Plugin.ReadSeparatedString(BaboonHawkWords.Value) }
        };
        return categories;
    }

    private static string CategoryDescriptionFor (string category) {
        return $"Words that count as mentioning the {category}\n{CustomWordTip}";
    }
}