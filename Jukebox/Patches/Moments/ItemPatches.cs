using HarmonyLib;
using Jukebox.Services;

namespace Jukebox.Patches.Moments
{
    // Items. Their RPCs run on every client, so each one checks the item is in your hands or
    // was set off by you.

    // Turning an item on. Items with a moment of their own (guns, melee, grenades, mines,
    // drones, upgrades) are left to it, so one press is one sound.
    [HarmonyPatch(typeof(ItemToggle), "ToggleItemLogic")]
    internal static class ItemUsePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemToggle __instance, bool toggle, int player, out bool __state)
        {
            __state = toggle && player != -1 && player == SemiFunc.PhotonViewIDPlayerAvatarLocal() && !Items.HasOwnMoment(__instance);
            if (__state)
            {
                Actions.Fire("item_use");
                Silencer.Hold("item_use", __instance.soundOn);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                Silencer.Release("item_use");
            }
        }
    }

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootRPC))]
    internal static class GunPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemGun __instance, out bool __state)
        {
            __state = __instance.physGrabObject && __instance.physGrabObject.grabbedLocal;
            if (__state)
            {
                Silencer.Hold("gun", __instance.soundShoot, __instance.soundShootGlobal);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state && Silencer.Release("gun"))
            {
                Actions.Fire("gun");
            }
        }
    }

    [HarmonyPatch(typeof(ItemMelee), "StateSetRPC")]
    internal static class MeleeSwingPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemMelee __instance, int _newState)
        {
            if (_newState == (int)ItemMelee.State.Swinging && __instance.currentState != ItemMelee.State.Swinging
                && __instance.physGrabObject && __instance.physGrabObject.grabbedLocal)
            {
                Actions.Fire("melee_swing");
            }
        }
    }

    // SwingHitRPC returns before its sound while the hit is cooling down, so a hit counts only
    // when the sound played.
    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.SwingHitRPC))]
    internal static class MeleeHitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemMelee __instance, out bool __state)
        {
            __state = __instance.physGrabObject && __instance.physGrabObject.grabbedLocal;
            if (__state)
            {
                Silencer.Hold("melee_hit", __instance.soundHit);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state && Silencer.Release("melee_hit"))
            {
                Actions.Fire("melee_hit");
            }
        }
    }

    [HarmonyPatch(typeof(ItemGrenade), "TickStartRPC")]
    internal static class GrenadePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemGrenade __instance)
        {
            if (Items.Mine(__instance.activatorPlayerAvatar))
            {
                Actions.Fire("grenade");
            }
        }
    }

    // The explosion is skipped while the grenade sits in an inventory slot.
    [HarmonyPatch(typeof(ItemGrenade), "TickEndRPC")]
    internal static class GrenadeBoomPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemGrenade __instance)
        {
            if (Items.Mine(__instance.activatorPlayerAvatar) && __instance.itemEquippable && !__instance.itemEquippable.isEquipped)
            {
                Actions.Fire("grenade_boom");
            }
        }
    }

    [HarmonyPatch(typeof(ItemMine), nameof(ItemMine.StateSetRPC))]
    internal static class MinePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemMine __instance, int newState)
        {
            if (newState == (int)ItemMine.States.Armed && __instance.state != ItemMine.States.Armed && Items.Mine(__instance.activatorPlayerAvatar))
            {
                Actions.Fire("mine");
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrone), "ButtonToggleLogic")]
    internal static class DronePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemDrone __instance, bool activated)
        {
            if (!activated || __instance.currentState != ItemDrone.State.Off)
            {
                return;
            }
            ItemToggle toggle = __instance.GetComponent<ItemToggle>();
            if (toggle && toggle.playerTogglePhotonID == SemiFunc.PhotonViewIDPlayerAvatarLocal())
            {
                Actions.Fire("drone");
            }
        }
    }

    [HarmonyPatch(typeof(ItemUpgrade), nameof(ItemUpgrade.PlayerUpgrade))]
    internal static class UpgradePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ItemUpgrade __instance, out bool __state) => __state = !__instance.upgradeDone;

        [HarmonyPostfix]
        public static void Postfix(ItemUpgrade __instance, bool __state)
        {
            if (__state && __instance.upgradeDone && __instance.itemToggle
                && __instance.itemToggle.playerTogglePhotonID == SemiFunc.PhotonViewIDPlayerAvatarLocal())
            {
                Actions.Fire("upgrade");
            }
        }
    }

    internal static class Items
    {
        public static bool Mine(PlayerAvatar? who) => who && PlayerAvatar.instance && ReferenceEquals(who, PlayerAvatar.instance);

        public static bool HasOwnMoment(ItemToggle item) =>
            item.GetComponent<ItemGun>() || item.GetComponent<ItemMelee>() || item.GetComponent<ItemGrenade>()
            || item.GetComponent<ItemMine>() || item.GetComponent<ItemDrone>() || item.GetComponent<ItemUpgrade>();
    }
}
