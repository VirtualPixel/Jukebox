using HarmonyLib;
using Jukebox.Services;

namespace Jukebox.Patches.Moments
{
    // Health moves on your own Semibot. Hurt and Heal return early for all sorts of reasons
    // (invincible, god mode, already dead, not in the Main state), so the health before and
    // after is what says whether anything happened.

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    internal static class HurtPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerHealth __instance, out int __state) => __state = __instance.health;

        [HarmonyPostfix]
        public static void Postfix(PlayerHealth __instance, int __state)
        {
            // Down to 0 is a death, which has its own sound.
            if (Health.Mine(__instance) && __instance.health < __state && __instance.health > 0)
            {
                Actions.Fire("hurt");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Heal))]
    internal static class HealPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerHealth __instance, out int __state) => __state = __instance.health;

        [HarmonyPostfix]
        public static void Postfix(PlayerHealth __instance, int __state)
        {
            // From 0 is a revive, and the truck's heal is truck_heal: both have their own sound.
            if (Health.Mine(__instance) && !Health.InTruckHeal && __instance.health > __state && __state > 0)
            {
                Actions.Fire("heal");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.FinalHealRPC))]
    internal static class TruckHealPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance, out bool __state)
        {
            __state = __instance.isLocal && !__instance.finalHeal;
            if (__state)
            {
                Health.InTruckHeal = true;
                Silencer.Hold("truck_heal", __instance.truckReturn, __instance.truckReturnGlobal);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance, bool __state)
        {
            Silencer.Release("truck_heal");
            Health.InTruckHeal = false;
            if (__state && __instance.finalHeal)
            {
                Actions.Fire("truck_heal");
            }
        }
    }

    // Runs on every client; the death sting and the death sound come later and are known by
    // sight in the Silencer.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
    internal static class DeathPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance, out bool __state) => __state = __instance.isLocal && !__instance.deadSet;

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance, bool __state)
        {
            if (__state && __instance.deadSet)
            {
                Actions.Fire("death", sticky: true);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ReviveRPC))]
    internal static class RevivePatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance, out bool __state)
        {
            __state = __instance.isLocal && __instance.isDisabled;
            if (__state && __instance.playerReviveEffects)
            {
                Silencer.Hold("revive", __instance.playerReviveEffects.reviveSound);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance, bool __state)
        {
            Silencer.Release("revive");
            if (__state && !__instance.isDisabled)
            {
                Actions.Fire("revive");
            }
        }
    }

    internal static class Health
    {
        public static bool InTruckHeal;

        // PlayerHealth sits on the avatar's own object; the local one is the one PlayerAvatar.instance holds.
        public static bool Mine(PlayerHealth health) => PlayerAvatar.instance && ReferenceEquals(PlayerAvatar.instance.playerHealth, health);
    }
}
