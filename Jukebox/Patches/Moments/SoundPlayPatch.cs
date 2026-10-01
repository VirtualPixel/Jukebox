using HarmonyLib;
using Jukebox.Services;
using UnityEngine;

namespace Jukebox.Patches.Moments
{
    // Every game sound goes through this overload (the Transform ones call it), so it is the
    // one place the game's own sound for a mapped action is quietened.
    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float))]
    internal static class SoundPlayPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Sound __instance, AudioSource __result) => Silencer.Played(__instance, __result);
    }
}
