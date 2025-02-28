using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using RWF;
using UnboundLib;
using UnboundLib.GameModes;

namespace CTD
{
    [HarmonyPatch]
    class LateJoinPatches
    {
        public static bool LateJoining = false;
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PrivateRoomHandler),nameof(PrivateRoomHandler.OnJoinedRoom))]
        static bool LateJoinHandler() {
            LateJoining = (bool)PhotonNetwork.CurrentRoom.CustomProperties["active"];
            return !LateJoining;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PrivateRoomHandler), nameof(PrivateRoomHandler.SetGameSettings))]
        static bool TriggerLateJoin() {
            try {
                if((bool)PhotonNetwork.CurrentRoom.CustomProperties["active"] != LateJoining) {
                    NetworkingManager.RPC(typeof(PrivateRoomHandler), nameof(PrivateRoomHandler.SetGameSettingsResponse), PhotonNetwork.LocalPlayer.ActorNumber);
                    return false;
                }
            } catch { return true; }
            Unbound.Instance.ExecuteAfterFrames(2, () => ((ConnectionTolerantDeathmatch)GameModeManager.CurrentHandler.GameMode).RquestLateJoin());
            return true;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkConnectionHandler), nameof(NetworkConnectionHandler.CreateRoom))]
        static void AddProperty(ref RoomOptions roomOptions) {
            roomOptions.CustomRoomPropertiesForLobby = new string[] { "active" };
            roomOptions.CustomRoomProperties = new Hashtable { { "active", false } };

            ///DELETE BEFORE PUBLISHING
            for(int _=0; _<20; _++) UnityEngine.Debug.Log("FUCKING DO NOT PUBLISH THE MOD WITH THIS CODE STILL IN IT!!!");
            if(!SteamManager.Initialized)
                PhotonNetwork.CreateRoom("0", roomOptions);
        }
    }
}
