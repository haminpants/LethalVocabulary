using System.Speech.Recognition;

namespace LethalVocabulary;

public class SpeechRecognizer {
    private static readonly SpeechRecognitionEngine Recognizer = new();

    private static readonly Grammar CommonWords = new(new GrammarBuilder(new Choices(
        "cog", "heck", "some", "something", "ok"
    )));

    public SpeechRecognizer () {
        Recognizer.SetInputToDefaultAudioDevice();
        Recognizer.SpeechRecognized += Plugin.ProcessSpeech;
        Recognizer.LoadGrammar(CommonWords);
        Recognizer.LoadGrammar(new Grammar(new GrammarBuilder(new Choices(Plugin.GetAllWords()))));
        Recognizer.RecognizeAsync(RecognizeMode.Multiple);
    }
}