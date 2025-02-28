using HarmonyLib;
using Photon.Pun;
using UnboundLib;
using UnboundLib.GameModes;
using UnityEngine;

namespace CTD {
    
    [HarmonyPatch(typeof(PlayerAssigner), nameof(PlayerAssigner.Awake))]
    public class LatePlayerHandlerAssigner {
        public static void Postfix() {
            PlayerAssigner.instance.playerPrefab.GetOrAddComponent<LatePlayerHandler>();
        }
    }
    class LatePlayerHandler:MonoBehaviour, IPunInstantiateMagicCallback {
        public const string LateJoinPlayerString = "NEW PLAYER ASKING FOR LATE JOIN";
        public void OnPhotonInstantiate(PhotonMessageInfo info) {

            var data = info.photonView.InstantiationData;
            if(data != null && data[0] is string str && str == LateJoinPlayerString) {
                gameObject.SetActive(false);
                ((ConnectionTolerantDeathmatch)GameModeManager.CurrentHandler.GameMode).UnloadPlayers.Add(GetComponent<Player>());
            }
        }
    }
}
