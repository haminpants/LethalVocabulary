using System;
using System.Speech.Recognition;

namespace LethalVocabulary;

public class SpeechRecognizer {
    private static readonly Grammar CommonWords = new(new GrammarBuilder(new Choices(
        "cog", "heck", "some", "something", "ok", "way", "main", "fire", "exit", "hello", "lyrics", "uh", "oh",
        "hi", "sing", "sky", "forever", "for", "see", "fly", "word", "what", "hey", "hm", "mine", "turret",
        "door", "lock", "locked", "explode", "exploded", "dead", "end", "pipe", "steam", "smoke", "scrap", "flash",
        "stun", "light", "shovel", "stop", "sign", "yield", "boom", "boombox", "box", "ladder", "emergency", "pro",
        "tzp", "inhale", "use", "drop", "miss"
    )));

    private readonly SpeechRecognitionEngine _recognizer = new();

    public SpeechRecognizer () {
        _recognizer.SetInputToDefaultAudioDevice();
        _recognizer.LoadGrammar(CommonWords);
        _recognizer.LoadGrammar(new Grammar(new GrammarBuilder(
            new Choices(Plugin.GetAllWordsFromConfig(true)))));
        _recognizer.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void AddSpeechRecognizedHandler (EventHandler<SpeechRecognizedEventArgs> eventHandler) {
        _recognizer.SpeechRecognized += eventHandler;
    }
}