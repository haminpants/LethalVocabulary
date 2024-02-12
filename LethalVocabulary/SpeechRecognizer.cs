using System.Speech.Recognition;

namespace LethalVocabulary;

public class SpeechRecognizer {
    private static readonly SpeechRecognitionEngine Recognizer = new();

    private static readonly Grammar CommonWords = new(new GrammarBuilder(new Choices(
        "cog", "heck", "some", "something", "ok", "way", "main", "fire", "exit", "hello", "lyrics", "uh", "oh",
        "hi", "sing", "sky", "forever", "for", "see", "fly", "word"
    )));

    public SpeechRecognizer () {
        Recognizer.SetInputToDefaultAudioDevice();
        Recognizer.LoadGrammar(CommonWords);
        Recognizer.LoadGrammar(new Grammar(new GrammarBuilder(new Choices(Plugin.GetAllWords()))));
        Recognizer.SpeechRecognized += (_, e) => {
            var speech = e.Result.Text;
            var confidence = e.Result.Confidence;
            Plugin.logger.LogInfo("Heard \"" + speech + "\" with " + confidence + " confidence.");

            if (!Plugin.CheckVocabulary || !Plugin.StringIsIllegal(speech) || confidence < 0.85f || 
                RoundManager.Instance == null) return;
            var player = RoundManager.Instance.playersManager.localPlayerController;
            if (player == null || player.isPlayerDead) return;
            player.GetComponent<PenaltyManager>().CreateExplosionServerRpc(player.transform.position);
        };
        Recognizer.RecognizeAsync(RecognizeMode.Multiple);
    }
}