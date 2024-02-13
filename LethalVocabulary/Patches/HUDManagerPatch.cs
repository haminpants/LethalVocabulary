using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(HUDManager))]
public class HUDManagerPatch {
    [HarmonyPrefix]
    [HarmonyPatch("SubmitChat_performed")]
    private static void CheckSubmittedChat (ref HUDManager __instance) {
        var chatMessage = __instance.chatTextField.text.ToLower();
        if (!Plugin.Instance.roundInProgress ||
            !Plugin.StringHasWords(chatMessage, Plugin.Instance.ClientBannedWords)) return;
        PenaltyManager.Instance.PunishPlayerServerRpc(__instance.NetworkManager.LocalClientId);
        __instance.chatTextField.text = "";
    }
}