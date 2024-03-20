using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(HUDManager))]
public class HUDManagerPatch {
    [HarmonyPrefix]
    [HarmonyPatch("SubmitChat_performed")]
    private static void ParseChatMessage (ref HUDManager __instance) {
        string message = __instance.chatTextField.text;

        if (message.ToLower().Contains("remind me")) {
            PunishmentManager.Instance.DisplayCategoryHintsClientRpc();
            __instance.chatTextField.text = "";
            return;
        }

        if (PunishmentManager.Instance.apologyIndex >= 0)
            if (message.Equals(PunishmentManager.Apologies[PunishmentManager.Instance.apologyIndex]))
                PunishmentManager.Instance.StopApologyTimer();

        if (!PunishmentManager.Instance.StringIsLegal(message)) __instance.chatTextField.text = "";
    }
}