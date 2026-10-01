using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jukebox.Configuration;
using Jukebox.Services;
using UnityEngine;

namespace Jukebox
{
    // The sound engine the rest of the house borrows. Clips are mixed into your own voice
    // stream on the way to the encoder, so everyone in the room hears them with nothing
    // installed, ducked under your voice while you talk. In a room where everyone has this
    // mod the clips skip the voice and play at your Semibot on the other side instead.
    //
    // Any mod registers a folder or a .pak of its own and calls Api.Play. Your own clips live
    // in the save folder and answer to a config, a hotkey or an HTTP request, so a soundboard
    // or a stream deck can put anything through your Semibot's mouth.
    [BepInPlugin("Vippy.Jukebox", "Jukebox", BuildInfo.Version)]
    [BepInDependency(Wardrobe.Api.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Jukebox : BaseUnityPlugin
    {
        internal static Jukebox Instance { get; private set; } = null!;
        private ManualLogSource BaseLogger => base.Logger;
        internal Harmony? Harmony;

        private void Awake()
        {
            Instance = this;
            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            PluginConfig.Init(Config);
            Harmony ??= new Harmony("Vippy.Jukebox");
            Harmony.PatchAll();
            Library.LoadSoundboard();
            Soundboard.Load();
            SoundSets.Load();
            Actions.WriteReadme();
            HostSwitch.Init();
            WheelTakeover.Start();
            Endpoint.Start();
            gameObject.AddComponent<JukeboxController>();
            AudioSettings.OnAudioConfigurationChanged += deviceChanged => Services.Mixer.OnAudioReset();

            LogAlways("Jukebox v" + BuildInfo.Version + " ready. Your own clips go in " + Library.SoundboardFolder + ".");
        }

        internal static void LogAlways(string message) => Instance.BaseLogger.LogInfo(message);

        internal static void LogInfo(string message)
        {
            if (PluginConfig.Logging.Value)
            {
                Instance.BaseLogger.LogInfo(message);
            }
        }
    }
}
