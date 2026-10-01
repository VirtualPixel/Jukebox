using HarmonyLib;
using Jukebox.Models;
using Jukebox.Services;

namespace Jukebox.Patches.Moments
{
    // The run: levels, extraction, the shop, the taxman. These happen to the whole room at
    // once and fire on every client, so they are played shared: one friend with the mod plays
    // it and nobody hears it twice.

    // The one caller is the game director starting the level, a moment before it goes to Main,
    // and nothing plays before Main. So the sound waits for the director's next step.
    [HarmonyPatch(typeof(LoadingUI), nameof(LoadingUI.StopLoading))]
    internal static class LevelStartPatch
    {
        internal static readonly StartCue Cue = new StartCue();

        [HarmonyPostfix]
        public static void Postfix()
        {
            Hands.NewLevel();
            Moves.Tumbling = false;
            if (RunManager.instance && SemiFunc.RunIsLevel())
            {
                Cue.Arm();
            }
            else
            {
                Cue.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(GameDirector), nameof(GameDirector.gameStateStart))]
    internal static class LevelStartedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameDirector __instance)
        {
            if (LevelStartPatch.Cue.Due(__instance.currentState == GameDirector.gameState.Main) && SemiFunc.RunIsLevel())
            {
                Actions.Fire("level_start", shared: true);
            }
        }
    }

    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.StateSetRPC))]
    internal static class ExtractionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ExtractionPoint __instance, ExtractionPoint.State state, out bool __state) => __state = __instance.currentState != state;

        [HarmonyPostfix]
        public static void Postfix(ExtractionPoint.State state, bool __state)
        {
            if (!__state || !RunManager.instance)
            {
                return;
            }
            bool shop = SemiFunc.RunIsShop();
            if (state == ExtractionPoint.State.Active && !shop)
            {
                Actions.Fire("extraction_open", shared: true);
            }
            else if (state == ExtractionPoint.State.Complete)
            {
                Actions.Fire(shop ? "shop_buy" : "extracted", shared: true);
            }
        }
    }

    [HarmonyPatch(typeof(RoundDirector), "ExtractionCompletedAllRPC")]
    internal static class LevelWonPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => Actions.Fire("level_won", shared: true);
    }

    [HarmonyPatch(typeof(TruckScreenText), nameof(TruckScreenText.EngineStartRPC))]
    internal static class TruckLeavePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (RunManager.instance && SemiFunc.RunIsLevel())
            {
                Actions.Fire("truck_leave", shared: true);
            }
        }
    }

    // The host sets it; everyone else hears it by a buffered RPC, which a late joiner gets
    // replayed while the level is still loading, hence the Generated check.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.AllPlayersDeadSet))]
    internal static class LevelLostHostPatch
    {
        [HarmonyPrefix]
        public static void Prefix(RunManager __instance, bool _set)
        {
            if (_set && !__instance.allPlayersDead)
            {
                Actions.Fire("level_lost", shared: true, sticky: true);
            }
        }
    }

    [HarmonyPatch(typeof(RunManagerPUN), "AllPlayersDeadRPC")]
    internal static class LevelLostPatch
    {
        [HarmonyPostfix]
        public static void Postfix(bool _set)
        {
            if (_set && LevelGenerator.Instance && LevelGenerator.Instance.Generated)
            {
                Actions.Fire("level_lost", shared: true, sticky: true);
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.DeathRPC))]
    internal static class EnemyDiesPatch
    {
        [HarmonyPostfix]
        public static void Postfix() => Actions.Fire("enemy_dies", shared: true);
    }

    // Each new line on the truck screen plays a blip; a taxman line is one spoken under his name.
    [HarmonyPatch(typeof(TruckScreenText), "NewLineSoundEffect")]
    internal static class TaxmanPatch
    {
        [HarmonyPrefix]
        public static void Prefix(TruckScreenText __instance, out bool __state)
        {
            __state = __instance.currentNickname == __instance.nicknameTaxman;
            if (__state)
            {
                Actions.Fire("taxman", shared: true);
                Silencer.Hold("taxman", __instance.newLineSound);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                Silencer.Release("taxman");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerExpressionSet))]
    internal static class EmotePatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance, float _percent)
        {
            if (__instance.isLocal && _percent > 0f)
            {
                Actions.Fire("emote");
            }
        }
    }

    // The tester build adds a locale argument, so this binds by name. A leading '/' is a
    // command, not a message.
    [HarmonyPatch(typeof(PlayerAvatar), "ChatMessageSend")]
    internal static class ChatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance, string _message)
        {
            if (__instance.isLocal && !string.IsNullOrWhiteSpace(_message) && !_message.TrimStart().StartsWith("/"))
            {
                Actions.Fire("chat");
            }
        }
    }
}
