using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch {
    [HarmonyPrefix]
    [HarmonyPatch("ParsePlayerSentence")]
    private static void ParseTerminalCommand (ref Terminal __instance) {
        string command = __instance.screenText.text[^__instance.textAdded..];
        if (!PunishmentManager.Instance.StringIsLegal(command)) __instance.screenText.text = "";
    }
}