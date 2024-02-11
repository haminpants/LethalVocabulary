using BepInEx.Configuration;

namespace LethalVocabulary;

public class Config {
    public static ConfigEntry<bool> ExtendedWordsEnabled;
    public static ConfigEntry<int> CategoriesPerMoon;
    public static ConfigEntry<string> SnareFleaWords;
    public static ConfigEntry<string> BunkerSpiderWords;
    public static ConfigEntry<string> HoardingBugWords;
    public static ConfigEntry<string> BrackenWords;
    public static ConfigEntry<string> ThumperWords;
    public static ConfigEntry<string> HygrodereWords;
    public static ConfigEntry<string> GhostGirlWords;
    public static ConfigEntry<string> SporeLizardWords;
    public static ConfigEntry<string> NutCrackerWords;
    public static ConfigEntry<string> JesterWords;
    public static ConfigEntry<string> MaskedWords;
    public static ConfigEntry<string> EyelessDogWords;
    public static ConfigEntry<string> ForestKeeperWords;
    public static ConfigEntry<string> EarthLeviathanWords;
    public static ConfigEntry<string> BaboonHawkWords;
    public static ConfigEntry<bool> DebugSTTWords;

    public Config(ConfigFile cfg) {
        ExtendedWordsEnabled = cfg.Bind("Gameplay",
            "EnableExtendedWords",
            false,
            "Words in ALL CAPS are considered \"extended words\" and will not be included unless this setting is true.");
        CategoriesPerMoon = cfg.Bind("Gameplay",
            "CategoriesPerMoon",
            1,
            "The number of categories to choose per moon.");
        SnareFleaWords = cfg.Bind("Dictionary.Entities",
            "SnareFlea",
            "snare,flea,fleas,centipede,centipedes,HEAD,CRAB,CRABS,FACE,HUGGER,HUGGERS",
            "Words that count as mentioning the Snare FleaDefault:snare,flea,fleas,centipede,centipedes,HEAD,CRAB,CRABS,FACE,HUGGER,HUGGERS");
        BunkerSpiderWords = cfg.Bind("Dictionary.Entities",
            "BunkerSpider",
            "bunker,spider,spiders,arachnid,arachnids,web,webs",
            "Words that count as mentioning the Bunker SpiderDefault:bunker,spider,arachnid,web");
        HoardingBugWords = cfg.Bind("Dictionary.Entities",
            "HoardingBug",
            "hoard,hoarding,bug,loot bug,LOOT",
            "Words that count as mentioning the Hoarding BugDefault:hoard,hoarding,bug,loot bug");
        BrackenWords = cfg.Bind("Dictionary.Entities",
            "Bracken",
            "bracken,flower,man,SHADOW,BLACK",
            "Words that count as mentioning the BrackenDefault:bracken,flower,man,SHADOW,BLACK");
        ThumperWords = cfg.Bind("Dictionary.Entities",
            "Thumper",
            "thumper,thumpers,halve,halves,crawler,crawlers,BEAST,TWO LEG,TWO LEGGED",
            "Words that count as mentioning the ThumperDefault:Words that count as mentioning the BrackenDefault:bracken,flower,man,SHADOW,BLACK");
        HygrodereWords = cfg.Bind("Dictionary.Entities",
            "Hygrodere",
            "hygrodere,hygroderes,slime,slimes,JELLY",
            "Words that count as mentioning the HygrodereDefault:Words that count as mentioning the BrackenDefault:bracken,flower,man,SHADOW,BLACK");
        GhostGirlWords = cfg.Bind("Dictionary.Entities",
            "Ghost Girl",
            "ghost,ghosts,girl,girls,huant,huanted,BITCH",
            "Words that count as mentioning the Ghost GirlDefault:ghost,ghosts,girl,girls,huant,huanted,BITCH");
        SporeLizardWords = cfg.Bind("Dictionary.Entities",
            "Spore Lizard",
            "spore,spores,lizard,lizards",
            "Words that count as mentioning the Spore LizardDefault:spore,spores,lizard,lizards");
        NutCrackerWords = cfg.Bind("Dictionary.Entities",
            "Nutcracker",
            "nut,nuts,cracker,crackers,shotgun,gun,soldier",
            "Words that count as mentioning the NutcrackerDefault:nut,nuts,cracker,crackers,shotgun,gun,soldier");
        JesterWords = cfg.Bind("Dictionary.Entities",
            "Jester",
            "jester,music,wind,winding",
            "Words that count as mentioning the Jester");
        MaskedWords = cfg.Bind("Dictionary.Entities",
            "Masked",
            "masked,mimic",
            "Words that count as mentioning the Masked");
        EyelessDogWords = cfg.Bind("Dictionary.Entities",
            "EyelessDog",
            "eye,eyeless,dog,dogs",
            "Words that count as mentioning the Eyeless Dog");
        ForestKeeperWords = cfg.Bind("Dictionary.Entities",
            "ForestKeeper",
            "forest,keeper,keepers,giant,giants",
            "Words that count as mentioning the Giant");
        EarthLeviathanWords = cfg.Bind("Dictionary.Entities",
            "EarthLeviathan",
            "earth,leviathan,leviathans,worm,worms",
            "Words that count as mentioning the Earth Leviathan");
        BaboonHawkWords = cfg.Bind("Dictionary.Entities",
            "BaboonHawk",
            "baboon,baboons,hawk,hawks",
            "Words that count as mentioning the Baboon Hawk");
        DebugSTTWords = cfg.Bind("Debug",
            "LogSpeechToTextOutput",
            true,
            "Log all words and confidence");
    }
}