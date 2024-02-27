using GameNetcodeStuff;
using HarmonyLib;
using TMPro;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(HUDManager))]
public class HUDManagerPatch {
    [HarmonyPrefix]
    [HarmonyPatch("SubmitChat_performed")]
    private static void CheckSubmittedChat (ref HUDManager __instance) {
        if (ProcessCommand(__instance.chatTextField, __instance.localPlayer)) { }
        else if (!PenaltyManager.Instance.StringIsLegal(__instance.chatTextField.text)) {
            PenaltyManager.Instance.PunishPlayerServerRpc(__instance.localPlayer.playerClientId);
            __instance.chatTextField.text = "";
        }
    }

    private static bool ProcessCommand (TMP_InputField textField, PlayerControllerB playerController) {
        string[] command = textField.text.Split("_", 2);
        if (!command[0].Equals("/lv")) return false;

        switch (command[1]) {
            case "debug":
                var playerObjects = StartOfRound.Instance.allPlayerObjects;
                string output = "All Player Objects:\n";
                for (int i = 0; i < playerObjects.Length; i++) {
                    var player = playerObjects[i].GetComponent<PlayerControllerB>();
                    output += $"Index: {i}: {player.playerClientId} - {player.playerUsername}";
                    if (i < playerObjects.Length - 1) output += "\n";
                }

                Plugin.logger.LogInfo(output);
                break;
            case "cw":
            case "cursewords":
                if (!playerController.IsHost) {
                    Plugin.DisplayHUDTip("Only the host can toggle this setting!", "");
                    break;
                }

                PenaltyManager.Instance.TogglePunishCurseWordsServerRpc();
                break;
            case "sc":
            case "sharedcategories":
                if (!playerController.IsHost) {
                    Plugin.DisplayHUDTip("Only the host can toggle this setting!", "");
                    break;
                }

                PenaltyManager.Instance.ToggleHideSharedCategoriesServerRpc();
                break;
            case "pc":
            case "privatecategories":
                if (!playerController.IsHost) {
                    Plugin.DisplayHUDTip("Only the host can toggle this setting!", "");
                    break;
                }

                PenaltyManager.Instance.ToggleHidePrivateCategoriesServerRpc();
                break;
        }

        textField.text = "";
        return true;
    }
}