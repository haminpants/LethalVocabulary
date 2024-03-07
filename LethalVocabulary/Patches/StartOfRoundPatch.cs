using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(StartOfRound))]
public class StartOfRoundPatch {
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void SpawnNetcodeHelper (ref StartOfRound __instance) {
        if (!__instance.IsHost) return;
        GameObject netcodeHelper = Object.Instantiate(Plugin.NetcodeHelperPrefab);
        netcodeHelper.GetComponent<NetworkObject>().Spawn();
        Plugin.Console.LogInfo("Spawned netcode helper!");
    }

    [HarmonyPostfix]
    [HarmonyPatch("EndOfGameClientRpc")]
    private static void SetMoonInProgress (ref StartOfRound __instance) {
        if (!__instance.IsHost || !PunishmentManager.Instance.MoonInProgress.Value) return;
        PunishmentManager.Instance.SetMoonInProgressServerRpc(false);
    }
}