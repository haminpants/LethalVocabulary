using BepInEx.Configuration;

namespace LethalVocabulary;

public class Config {
    // Gameplay
    public static ConfigEntry<string> ActivePunishment;
    public static ConfigEntry<int> SharedCategoriesPerMoon;
    public static ConfigEntry<int> PrivateCategoriesPerMoon;
    public static ConfigEntry<bool> DisplayCategoryHints;
    public static ConfigEntry<double> ConfidenceThreshold;
    public static ConfigEntry<bool> LogRecognitionOutput;

    // Categories
    public static ConfigEntry<string> BaboonHawkWords;
    public static ConfigEntry<string> BrackenWords;
    public static ConfigEntry<string> BunkerSpiderWords;
    public static ConfigEntry<string> CoilHeadWords;
    public static ConfigEntry<string> EarthLeviathanWords;
    public static ConfigEntry<string> EyelessDogWords;
    public static ConfigEntry<string> ForestKeeperWords;
    public static ConfigEntry<string> GhostGirlWords;
    public static ConfigEntry<string> HoardingBugWords;
    public static ConfigEntry<string> HygrodereWords;
    public static ConfigEntry<string> JesterWords;
    public static ConfigEntry<string> MaskedWords;
    public static ConfigEntry<string> NutcrackerWords;
    public static ConfigEntry<string> SnareFleaWords;
    public static ConfigEntry<string> SporeLizardWords;
    public static ConfigEntry<string> ThumperWords;

    private readonly string CustomWordTip =
        "The following words will trigger a punishment when this category is selected.\n" +
        $"MAX {PunishmentManager.CategoryWordsMaxLength} characters including commas. " +
        $"MAX {PunishmentManager.WordMaxLength} characters per word.\n" +
        "Words must be separated by a comma (,). Capitalization does not matter. ";

    public Config (ConfigFile cfg) {
        #region Bind Gameplay Config

        ActivePunishment = cfg.Bind("Gameplay", "Punishment", Punishment.Random.ToString(),
            new ConfigDescription(
                "The punishment that will happen on the player upon talking about one of the banned categories.",
                new AcceptableValueList<string>(Punishment.Random.ToString(),
                    Punishment.Teleport.ToString(), Punishment.Explode.ToString())));

        SharedCategoriesPerMoon = cfg.Bind("Gameplay", "Shared Categories Per Moon", 1,
            new ConfigDescription(
                "The number of categories that will be shared among all players each moon. " +
                "Set this value to 0 to disable shared categories.",
                new AcceptableValueRange<int>(0, 20)));

        PrivateCategoriesPerMoon = cfg.Bind("Gameplay", "Private Categories Per Moon", 0,
            new ConfigDescription(
                "The number of categories that will be randomly selected for each player each moon. " +
                "Set this value to 0 to disable private categories.",
                new AcceptableValueRange<int>(0, 20)));

        DisplayCategoryHints = cfg.Bind("Gameplay", "Display Category Hints", true,
            new ConfigDescription(
                "Determines whether or not to display a HUD tip telling players what the shared categories are.",
                new AcceptableValueList<bool>(true, false)));

        ConfidenceThreshold = cfg.Bind("Gameplay", "Confidence Threshold", 0.9,
            new ConfigDescription(
                "Speech recognized by the mod return with a confidence value. " +
                "If the confidence of the recognition is above this value, a punishment will trigger.",
                new AcceptableValueRange<double>(0.1, 0.99)));

        LogRecognitionOutput = cfg.Bind("Gameplay", "Log Recognition Output", true,
            new ConfigDescription(
                "Enabling this setting will show the speech recognition output in the console.\n" +
                "Recognitions that are above the Confidence Threshold will log as errors to make them more visible.",
                new AcceptableValueList<bool>(true, false)));

        #endregion

        #region Bind Category Config

        BaboonHawkWords = cfg.Bind("Categories.Entities", "Baboon Hawk",
            "baboon hawk,baboon,hawk",
            CustomWordTip);
        BrackenWords = cfg.Bind("Categories.Entities", "Bracken",
            "bracken,flowerman",
            CustomWordTip);
        BunkerSpiderWords = cfg.Bind("Categories.Entities", "Bunker Spider",
            "bunker spider,bunker,spider,web",
            CustomWordTip);
        CoilHeadWords = cfg.Bind("Categories.Entities", "Coil Head",
            "coil head,coil,head",
            CustomWordTip);
        EarthLeviathanWords = cfg.Bind("Categories.Entities", "Earth Leviathan",
            "earth leviathan,earth,leviathan,worm",
            CustomWordTip);
        EyelessDogWords = cfg.Bind("Categories.Entities", "Eyeless Dog",
            "eyeless dog,eyeless,dog",
            CustomWordTip);
        ForestKeeperWords = cfg.Bind("Categories.Entities", "Forest Keeper",
            "forest keeper,forest giant,keeper,giant",
            CustomWordTip);
        GhostGirlWords = cfg.Bind("Categories.Entities", "Ghost Girl",
            "ghost girl,ghost,girl,haunt,haunted",
            CustomWordTip);
        HoardingBugWords = cfg.Bind("Categories.Entities", "Hoarding Bug",
            "hoarding bug,loot bug,hoard,loot,bug",
            CustomWordTip);
        HygrodereWords = cfg.Bind("Categories.Entities", "Hygrodere",
            "hygrodere,slime,blob",
            CustomWordTip);
        JesterWords = cfg.Bind("Categories.Entities", "Jester",
            "jester,winding",
            CustomWordTip);
        MaskedWords = cfg.Bind("Categories.Entities", "Mimic",
            "mimic,masked",
            CustomWordTip);
        NutcrackerWords = cfg.Bind("Categories.Entities", "Nutcracker",
            "nutcracker,soldier,shotgun,gun,nut",
            CustomWordTip);
        SnareFleaWords = cfg.Bind("Categories.Entities", "Snare Flea",
            "snare flea,snare,flea,centipede,face hugger",
            CustomWordTip);
        SporeLizardWords = cfg.Bind("Categories.Entities", "Spore Lizard",
            "spore lizard,spore,lizard,puffer",
            CustomWordTip);
        ThumperWords = cfg.Bind("Categories.Entities", "Thumper",
            "thumper,crawler",
            CustomWordTip);

        #endregion
    }
}