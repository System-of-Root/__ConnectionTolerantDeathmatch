using BepInEx;
using HarmonyLib;
using Photon.Pun;
using UnboundLib.GameModes;

namespace CTD {

    [BepInDependency("io.olavim.rounds.rwf")]
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class Main:BaseUnityPlugin {
        private const string ModId = "Systems.R00t.CTM";
        private const string ModName = "Connection Tolerant Deathmatch";
        public const string Version = "0.5.0";

        void Awake() {
            new Harmony(ModId).PatchAll();
        }

        void Start() {
            gameObject.AddComponent<NetworkEventCallbacks>();
        }
    }

    public class NetworkEventCallbacks:MonoBehaviourPunCallbacks {
        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) {
            if(GameModeManager.CurrentHandler is ConnectionTolerantDeathmatchHandler handler) {
                handler.GameMode.HeartBeatList.Remove(otherPlayer.ActorNumber);
                handler.GameMode.WaitingClients.Remove(otherPlayer.ActorNumber);
            }
        }
    }
}
