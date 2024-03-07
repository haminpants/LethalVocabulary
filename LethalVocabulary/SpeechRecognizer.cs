using System;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;

namespace LethalVocabulary;

public class SpeechRecognizer {
    private readonly SpeechRecognitionEngine _recognizer = new();
    private Grammar _activeGrammar;

    public SpeechRecognizer () {
        _recognizer.SetInputToDefaultAudioDevice();
        _recognizer.SpeechRecognized += (_, e) => {
            string speech = e.Result.Text;
            double confidence = e.Result.Confidence;

            if (Config.LogRecognitionOutput.Value) {
                string logMessage = $"Heard \"{speech}\" with {confidence * 100}% confidence";
                if (confidence > Config.ConfidenceThreshold.Value) Plugin.Console.LogError(logMessage);
                else Plugin.Console.LogInfo(logMessage);
            }
            
            if (speech.Contains("word") && (speech.Contains("what's") || speech.Contains("forgot")))
                PunishmentManager.Instance.DisplayCategoryHintsClientRpc();
            
            PunishmentManager.Instance.StringIsLegal(speech, confidence);
        };

        Plugin.Console.LogInfo("Created speech recognizer successfully. On standby until you land on a moon!");
    }

    public void StartRecognizer (bool updateGrammar = true) {
        try {
            if (updateGrammar) {
                if (_recognizer.Grammars.Contains(_activeGrammar)) _recognizer.UnloadGrammar(_activeGrammar);
                _activeGrammar = CreateCategoryGrammar();
                _recognizer.LoadGrammar(_activeGrammar);
            }

            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
            Plugin.Console.LogInfo("Speech recognizer is now active!");

            if (_recognizer.Grammars.Count > 1)
                Plugin.Console.LogWarning("Multiple grammars are loaded, this is unexpected and may cause issues. " +
                                          "Try disconnecting and reconnecting if issues arise.");
        }
        catch (Exception e) {
            Plugin.Console.LogError($"Failed to start speech recognizer (updateGrammar={updateGrammar})\n{e}");
        }
    }

    public void StopRecognizer (bool unloadAllGrammar = true) {
        try {
            _recognizer.RecognizeAsyncStop();
            Plugin.Console.LogInfo("Speech recognizer is now inactive!");

            if (unloadAllGrammar) {
                _recognizer.UnloadAllGrammars();
                Plugin.Console.LogInfo("Unloaded all loaded grammars!");
            }
        }
        catch (Exception e) {
            Plugin.Console.LogError($"Failed to stop speech recognizer\n{e}");
        }
    }

    private Grammar CreateCategoryGrammar () {
        SrgsRule rule = new("PunishmentRule");
        SrgsOneOf triggerWords = new("word");

        foreach (string word in PunishmentManager.Instance.ActiveWords) triggerWords.Add(new SrgsItem(word));

        rule.Add(new SrgsItem(1, 1, SrgsRuleRef.Dictation));
        rule.Add(new SrgsItem(1, 1, triggerWords));
        rule.Add(new SrgsItem(0, 1, SrgsRuleRef.Dictation));

        return new Grammar(new SrgsDocument {
            Rules = { rule },
            Root = rule
        });
    }
}