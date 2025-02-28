using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using RWF;
using UnboundLib.GameModes;

namespace CTD
{
    [HarmonyPatch]
    class LateJoinPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PrivateRoomHandler),nameof(PrivateRoomHandler.OnJoinedRoom))]
        static bool LateJoin() {
            bool active = (bool)PhotonNetwork.CurrentRoom.CustomProperties["active"];
            if(GameModeManager.CurrentHandler is ConnectionTolerantDeathmatchHandler handler) {
                handler.GameMode.RquestLateJoin();
            } else active = false;
            return !active;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkConnectionHandler), nameof(NetworkConnectionHandler.CreateRoom))]
        static void AddProperty(ref RoomOptions roomOptions) {
            roomOptions.CustomRoomPropertiesForLobby = new string[] { "active" };
            roomOptions.CustomRoomProperties = new Hashtable { { "active", false } };
        }
    }
}
