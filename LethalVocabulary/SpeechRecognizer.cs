using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;

namespace LethalVocabulary;

public class SpeechRecognizer {
    public static readonly HashSet<string> DefaultCommonWordsSet = new() {
        // Words that might get used in categories later
        "fire", "exit", "main", "land", "mine", "landmine", "turret", "door", "lock", "locked", "key", "steam", "smoke",
        "gas", "scrap", "flash", "light", "pro", "flashlight", "stun", "grenade", "shovel", "stop", "yield", "sign",
        "boombox", "emergency", "ladder", "tzp", "inhale", "inhalant", "moon", "land", "enemy", "pit", "gap", "quota",
        // Other common words
        "heck", "some", "something", "ok", "way", "hello", "hi", "hey", "lyrics", "sing", "sky", "not", "uh", "oh",
        "hm", "for", "forever", "see", "fly", "word", "what", "look", "dead", "died", "end", "explode", "exploded",
        "pipe", "boom", "box", "use", "drop", "miss", "really", "bro", "yea", "hard", "harder", "bright", "dark",
        "horse", "pony", "cog", "advance", "advanced", "game", "gamer", "you", "your", "watch", "out", "smile",
        "stupid", "doing", "job", "jump", "heavy", "weight", "weigh", "cocky", "devastated"
    };

    public static readonly HashSet<string> DefaultCurseWordsSet = new() {
        "fuck", "fucker", "fucking", "motherfucker",
        "shit", "shitter", "shitting", "bullshit",
        "bitch", "bitching",
        "ass", "asshole",
        "cock", "dick",
        "bastard", "pussy", "cunt", "crap"
    };

    private readonly SpeechRecognitionEngine _recognizer = new();

    public SpeechRecognizer () {
        _recognizer.SetInputToDefaultAudioDevice();
        _recognizer.LoadGrammar(CreateGrammar(DefaultCommonWordsSet, "default", 2));
        _recognizer.LoadGrammar(CreateGrammar(DefaultCurseWordsSet, "curse", 0));
    }

    public void AddSpeechRecognizedHandler (EventHandler<SpeechRecognizedEventArgs> eventHandler) {
        _recognizer.SpeechRecognized += eventHandler;
    }

    public void Start () {
        _recognizer.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void Stop () {
        _recognizer.RecognizeAsyncStop();
    }

    public void AddGrammar (Grammar grammar) {
        _recognizer.LoadGrammar(grammar);
    }

    public void UnloadGrammar (Grammar grammar) {
        _recognizer.UnloadGrammar(grammar);
    }

    public static Grammar CreateGrammar (IEnumerable<string> set, string name, int priority) {
        var grammar = new Grammar(new GrammarBuilder(new Choices(set.ToArray()))) {
            Name = name,
            Priority = priority
        };

        return grammar;
    }
}