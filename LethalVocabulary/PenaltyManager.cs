using Unity.Netcode;
using UnityEngine;

namespace LethalVocabulary;

public class PenaltyManager : NetworkBehaviour {
    [ServerRpc(RequireOwnership = false)]
    public void CreateExplosionServerRpc (Vector3 position) {
        var posOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        CreateExplosionClientRpc(position + posOffset);
    }

    [ClientRpc]
    public void CreateExplosionClientRpc (Vector3 position) {
        Landmine.SpawnExplosion(position, true, 1, 0);
    }
}