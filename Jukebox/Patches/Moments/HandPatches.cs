using System.Collections.Generic;
using HarmonyLib;
using Jukebox.Services;
using UnityEngine;

namespace Jukebox.Patches.Moments
{
    // Your hands: grabbing, letting go, throwing, valuables and doors.

    [HarmonyPatch(typeof(PhysGrabber), "StartGrabbingPhysObject")]
    internal static class GrabPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PhysGrabber __instance, PhysGrabObject _physGrabObject)
        {
            if (!__instance.isLocal || !__instance.grabbed || !_physGrabObject)
            {
                return;
            }
            if (_physGrabObject.GetComponent<ValuableObject>() && Hands.FirstTouch(_physGrabObject))
            {
                Actions.FireEither("valuable_grab", "grab");
                return;
            }
            Actions.Fire("grab");
        }
    }

    // There is no throw in the game, only a let go; one that leaves the object moving fast is
    // a throw here.
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ReleaseObject))]
    internal static class LetGoPatch
    {
        private const float ThrowSpeed = 7f;

        [HarmonyPrefix]
        public static void Prefix(PhysGrabber __instance, out float __state)
        {
            __state = -1f;
            if (__instance.isLocal && __instance.grabbed)
            {
                PhysGrabObject held = __instance.grabbedPhysGrabObject;
                __state = held ? held.rbVelocity.magnitude : 0f;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(PhysGrabber __instance, float __state)
        {
            if (__state < 0f || __instance.grabbed)
            {
                return;
            }
            if (__state > ThrowSpeed)
            {
                Actions.FireEither("throw", "let_go");
            }
            else
            {
                Actions.Fire("let_go");
            }
        }
    }

    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.Discover))]
    internal static class ValuableFoundPatch
    {
        [HarmonyPostfix]
        public static void Postfix(bool __result)
        {
            if (__result)
            {
                Actions.Fire("valuable_found");
            }
        }
    }

    // Runs on every client; only a valuable in your own hands is yours to hear break.
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
    internal static class ValuableBreakPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PhysGrabObjectImpactDetector __instance, bool _loseValue, out bool __state)
        {
            PhysGrabObject body = __instance.GetComponent<PhysGrabObject>();
            __state = _loseValue && __instance.isValuable && body && body.grabbedLocal;
            if (__state && __instance.impactAudio)
            {
                Actions.Fire("valuable_break");
                Silencer.Hold("valuable_break", __instance.impactAudio.breakLight, __instance.impactAudio.breakMedium);
                Silencer.Hold("valuable_break", __instance.impactAudio.breakHeavy);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                Silencer.Release("valuable_break");
            }
        }
    }

    // A valuable smashed to nothing, anywhere: the whole room sees it, one friend plays it.
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "DestroyObjectRPC")]
    internal static class ValuableDestroyedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PhysGrabObjectImpactDetector __instance, bool effects)
        {
            if (effects && __instance.isValuable)
            {
                Actions.Fire("valuable_destroyed", shared: true);
                if (__instance.impactAudio)
                {
                    Silencer.Hold("valuable_destroyed", __instance.impactAudio.destroy);
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("valuable_destroyed");
    }

    // Doors are pushed by physics and the host decides when one opened or shut; yours is the
    // one you are holding, or the last one you held.
    [HarmonyPatch(typeof(PhysGrabHinge), "OpenImpulseRPC")]
    internal static class DoorOpenPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PhysGrabHinge __instance)
        {
            if (Hands.Mine(__instance.physGrabObject) && __instance.hingeAudio)
            {
                Actions.Fire("door_open");
                Silencer.Hold("door_open", __instance.hingeAudio.Open, __instance.hingeAudio.OpenHeavy);
            }
        }

        [HarmonyPostfix]
        public static void Postfix() => Silencer.Release("door_open");
    }

    // The tester build adds a bool _sound before the message info, so this binds by name.
    [HarmonyPatch(typeof(PhysGrabHinge), "CloseImpulseRPC")]
    internal static class DoorClosePatch
    {
        [HarmonyPrefix]
        public static void Prefix(PhysGrabHinge __instance, bool heavy)
        {
            if (Hands.Mine(__instance.physGrabObject) && __instance.hingeAudio)
            {
                string action = heavy ? "door_slam" : "door_close";
                Actions.Fire(action);
                Silencer.Hold(action, heavy ? __instance.hingeAudio.CloseHeavy : __instance.hingeAudio.Close);
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            Silencer.Release("door_slam");
            Silencer.Release("door_close");
        }
    }

    internal static class Hands
    {
        private static readonly HashSet<int> touched = new HashSet<int>();

        // True the first time this level you get your hands on it.
        public static bool FirstTouch(PhysGrabObject thing) => touched.Add(thing.GetInstanceID());

        public static void NewLevel() => touched.Clear();

        public static bool Mine(PhysGrabObject thing)
        {
            if (!thing)
            {
                return false;
            }
            PhysGrabber hands = PhysGrabber.instance;
            return (hands && thing.playerGrabbing.Contains(hands))
                || (PlayerAvatar.instance && ReferenceEquals(thing.lastPlayerGrabbing, PlayerAvatar.instance));
        }
    }
}
