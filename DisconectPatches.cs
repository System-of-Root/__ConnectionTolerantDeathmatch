using HarmonyLib;
using RWF;
using UnboundLib.GameModes;

namespace CTD {
    [HarmonyPatch]
    class DisconectPatches
    {
        [HarmonyPatch(typeof(PrivateRoomHandler),nameof(PrivateRoomHandler.OnMasterClientSwitched))]
        [HarmonyPrefix]
        static bool AllowMasterSwitching(Photon.Realtime.Player newMaster) {
            if(GameModeManager.CurrentHandler is ConnectionTolerantDeathmatchHandler handler){
                handler.GameMode.MasterSwitched(newMaster);
                return false;
            }
            return true;
        }
    }
}
