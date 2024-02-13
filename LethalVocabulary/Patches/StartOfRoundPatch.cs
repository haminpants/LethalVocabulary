using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(StartOfRound))]
public class StartOfRoundPatch {
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void SpawnPenaltyManagerPrefab (ref StartOfRound __instance) {
        if (!__instance.IsHost) return;
        var penaltyManager = Object.Instantiate(Plugin.Instance.penaltyManagerPrefab);
        penaltyManager.GetComponent<NetworkObject>().Spawn();
    }

    [HarmonyPostfix]
    [HarmonyPatch("EndOfGameClientRpc")]
    private static void PerformEndOfGameOperations () {
        Plugin.Instance.EndRound();
    }
}