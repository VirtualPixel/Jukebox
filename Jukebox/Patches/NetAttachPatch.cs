using HarmonyLib;
using Jukebox.Services;
using Photon.Pun;

namespace Jukebox.Patches
{
    // The direct path rides the game's own PunManager view: the one persistent, shared
    // PhotonView every client has under the same id, so PUN dispatches our RPC by name with no
    // view id to juggle. A MonoBehaviourPunCallbacks registers with Photon the moment it
    // exists, and the first touch of PhotonNetwork builds its client and handler object, which
    // is the game's job to do at its own time and not a plugin's at load. So the relay goes on
    // from the game's own Awake, not ours.
    //
    // Two doors into the same room: the game does not settle which of PunManager and
    // StatsManager wakes first, and PunManager has to exist for there to be anything to attach
    // to. Whichever wakes second finds the relay already on and does nothing.
    internal static class NetAttach
    {
        public static void Ensure()
        {
            PunManager pun = PunManager.instance;
            if (pun && pun.gameObject.GetComponent<Net>() == null)
            {
                pun.gameObject.AddComponent<Net>();
            }
        }
    }

    [HarmonyPatch(typeof(PunManager), "Awake")]
    internal static class PunManagerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix() => NetAttach.Ensure();
    }

    [HarmonyPatch(typeof(StatsManager), "Awake")]
    internal static class StatsManagerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix() => NetAttach.Ensure();
    }
}
