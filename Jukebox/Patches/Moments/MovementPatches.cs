using HarmonyLib;
using Jukebox.Services;

namespace Jukebox.Patches.Moments
{
    // Your own Semibot's movement. The RPCs run on every client for every Semibot, so each
    // patch checks isLocal; the sound is held for the length of the method so a mapped action
    // quietens the game's own.

    [HarmonyPatch(typeof(PlayerAvatar), "JumpRPC")]
    internal static class JumpPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance)
        {
            if (__instance.isLocal)
            {
                Actions.Fire("jump");
                Silencer.Hold("jump", __instance.jumpSound, __instance.extraJumpSound);
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("jump");
    }

    // LandRPC returns before its sound when you land on a crate or a valuable (it hands the
    // bump to the object instead), so a landing counts only when the sound actually played.
    [HarmonyPatch(typeof(PlayerAvatar), "LandRPC")]
    internal static class LandPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance)
        {
            if (__instance.isLocal)
            {
                Silencer.Hold("land", __instance.landSound);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance)
        {
            if (Silencer.Release("land") && __instance.isLocal)
            {
                Actions.Fire("land");
            }
        }
    }

    // A fall of 8 m or more forces a tumble with a hurt timer on it; the hurt lands in
    // BreakImpact, which clears the timer, hence a prefix. Enemies throwing you set the same
    // timer, so the fall distance tells the two apart.
    [HarmonyPatch(typeof(PlayerTumble), nameof(PlayerTumble.BreakImpact))]
    internal static class HardLandPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerTumble __instance)
        {
            PlayerController pc = PlayerController.instance;
            if (__instance.playerAvatar && __instance.playerAvatar.isLocal && __instance.impactHurtTimer > 0f
                && pc && pc.CollisionController && pc.CollisionController.fallDistance > 5f)
            {
                Actions.Fire("land_hard");
            }
        }
    }

    // Crouch, stand and crawl: the avatar skips the sound while sprinting or sliding, and so do we.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.StandToCrouch))]
    internal static class CrouchPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance) => Moves.Hold(__instance, "crouch", __instance.standToCrouchSound);

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance) => Moves.FireIfPlayed(__instance, "crouch");
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.CrouchToStand))]
    internal static class StandPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance) => Moves.Hold(__instance, "stand", __instance.crouchToStandSound);

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance) => Moves.FireIfPlayed(__instance, "stand");
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.CrouchToCrawl))]
    internal static class CrawlPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance) => Moves.Hold(__instance, "crawl", __instance.crouchToCrawlSound);

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance) => Moves.FireIfPlayed(__instance, "crawl");
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.Slide))]
    internal static class SlidePatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance) => Moves.Hold(__instance, "slide", __instance.slideSound);

        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance) => Moves.FireIfPlayed(__instance, "slide");
    }

    // TumbleSetRPC calls these on every client, every time, so the edge is kept here.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.TumbleStart))]
    internal static class TumbleStartPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance)
        {
            if (__instance.isLocal)
            {
                Silencer.Hold("tumble", __instance.tumbleStartSound);
                if (!Moves.Tumbling)
                {
                    Moves.Tumbling = true;
                    Actions.Fire("tumble");
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("tumble");
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.TumbleStop))]
    internal static class TumbleStopPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance)
        {
            if (__instance.isLocal)
            {
                Silencer.Hold("tumble_end", __instance.tumbleStopSound);
                if (Moves.Tumbling)
                {
                    Moves.Tumbling = false;
                    Actions.Fire("tumble_end");
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("tumble_end");
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.TumbleBreakFree))]
    internal static class BreakFreePatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance)
        {
            if (__instance.isLocal)
            {
                Actions.Fire("break_free");
                Silencer.Hold("break_free", __instance.tumbleBreakFreeSound);
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("break_free");
    }

    // The controller hands the avatar its new state only when something flips, so the
    // not-sprinting to sprinting edge is a real one.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.UpdateState))]
    internal static class SprintPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerAvatar __instance, bool isSprinting)
        {
            if (__instance.isLocal && isSprinting && !__instance.isSprinting)
            {
                Actions.Fire("sprint");
            }
        }
    }

    internal static class Moves
    {
        public static bool Tumbling;

        public static void Hold(PlayerAvatar avatar, string action, Sound sound)
        {
            if (avatar.isLocal)
            {
                Silencer.Hold(action, sound);
            }
        }

        public static void FireIfPlayed(PlayerAvatar avatar, string action)
        {
            if (Silencer.Release(action) && avatar.isLocal)
            {
                Actions.Fire(action);
            }
        }
    }
}
