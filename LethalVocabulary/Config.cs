using BepInEx.Configuration;

namespace LethalVocabulary;

public class Config {
    private static readonly string CustomWordTip =
        "Words must be separated by a comma (,). Capitalization does not matter. " +
        "Words in ALL CAPS will not trigger a penalty unless EnableExtendedWords is enabled.";

    public static ConfigEntry<bool> EnableExtendedWords;
    public static ConfigEntry<int> CategoriesPerMoon;
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

    public Config (ConfigFile cfg) {
        EnableExtendedWords = cfg.Bind("Gameplay", "EnableExtendedWords",
            false,
            "Words in ALL CAPS are considered \"extended words\" and will not be included unless this setting is true");
        CategoriesPerMoon = cfg.Bind("Gameplay", "CategoriesPerMoon",
            1,
            "The number of categories that will be banned per moon.");
        SnareFleaWords = cfg.Bind("Categories.Entities", "Snare Flea",
            "snare,flea,fleas,centipede,centipedes,FACE,HUGGER,HUGGERS,HEAD,CRAB,CRABS",
            CategoryDescriptionFor("Snare Flea"));
        BunkerSpiderWords = cfg.Bind("Categories.Entities", "Bunker Spider",
            "bunker,spider,spiders,web,webs,ARACHNID,ARACHNIDS",
            CategoryDescriptionFor("Bunker Spider"));
        HoardingBugWords = cfg.Bind("Categories.Entities", "Hoarding Bug",
            "hoard,hoarding,bug,bugs,loot",
            CategoryDescriptionFor("Hoarding Bug"));
        BrackenWords = cfg.Bind("Categories.Entities", "Bracken",
            "bracken,flower",
            CategoryDescriptionFor("Bracken"));
        ThumperWords = cfg.Bind("Categories.Entities", "Thumper",
            "thumper,thumpers,crawler,crawlers",
            CategoryDescriptionFor("Thumper"));
        HygrodereWords = cfg.Bind("Categories.Entities", "Hygrodere (Slime)",
            "hygrodere,hygroderes,slime,slimes",
            CategoryDescriptionFor("Hygrodere (Slime)"));
        GhostGirlWords = cfg.Bind("Categories.Entities", "Ghost Girl",
            "ghost,ghosts,girl,girls,haunt,haunted",
            CategoryDescriptionFor("Ghost Girl"));
        SporeLizardWords = cfg.Bind("Categories.Entities", "Spore Lizard",
            "spore,spores,lizard,lizards",
            CategoryDescriptionFor("Spore Lizard"));
        NutcrackerWords = cfg.Bind("Categories.Entities", "Nutcracker",
            "nut,nuts,cracker,crackers,shotgun,gun,soldier",
            CategoryDescriptionFor("Nutcracker"));
        CoilHeadWords = cfg.Bind("Categories.Entities", "Coil Head",
            "coil,coils,head,heads",
            CategoryDescriptionFor("Coil Head"));
        JesterWords = cfg.Bind("Categories.Entities", "Jester",
            "jester,winding",
            CategoryDescriptionFor("Jester"));
        MaskedWords = cfg.Bind("Categories.Entities", "Masked",
            "mask,masked,mimic,mimics",
            CategoryDescriptionFor("Masked"));
        EyelessDogWords = cfg.Bind("Categories.Entities", "Eyeless Dog",
            "eye,eyes,eyeless,dog,dogs",
            CategoryDescriptionFor("Eyeless Dog"));
        ForestKeeperWords = cfg.Bind("Categories.Entities", "Forest Keeper (Giant)",
            "forest,keeper,keepers,giant,giants",
            CategoryDescriptionFor("Forest Keeper (Giant)"));
        EarthLeviathanWords = cfg.Bind("Categories.Entities", "Earth Leviathan (Worm)",
            "earth,leviathan,leviathans,worm,worms",
            CategoryDescriptionFor("Earth Leviathan (Worm)"));
        BaboonHawkWords = cfg.Bind("Categories.Entities", "Baboon Hawk",
            "baboon,baboons,hawk,hawks",
            CategoryDescriptionFor("Baboon Hawk"));
    }

    private static string CategoryDescriptionFor (string category) {
        return $"Words that count as mentioning the {category}\n{CustomWordTip}";
    }
}