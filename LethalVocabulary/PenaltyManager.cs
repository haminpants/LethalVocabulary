using Unity.Netcode;

namespace LethalVocabulary;

public class PenaltyManager : NetworkBehaviour {
    public static PenaltyManager Instance;

    private void Awake () {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId) {
        PunishPlayerClientRpc(clientId);
    }

    [ClientRpc]
    public void PunishPlayerClientRpc (ulong clientId) {
        if (StartOfRound.Instance == null) return;
        var player = StartOfRound.Instance.allPlayerObjects[clientId];
        Landmine.SpawnExplosion(player.transform.position, true, 1, 0);
    }
}