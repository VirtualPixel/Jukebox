using HarmonyLib;
using Jukebox.Models;
using Jukebox.Services;
using Photon.Voice.Unity;

namespace Jukebox.Patches
{
    // The game writes your transmit gate every frame: shut with push to talk up, muted, or no
    // mic. Photon drops a buffer before any processor sees it while the gate is shut, so a clip
    // could never get into the stream then. The game's word is kept for the mic (the mixer
    // blanks it) and the gate itself stays open while a clip sounds. Taking the value on the
    // way in, instead of writing it back after, keeps Photon from ending the stream and
    // starting it again every frame.
    [HarmonyPatch(typeof(Recorder), nameof(Recorder.TransmitEnabled), MethodType.Setter)]
    internal static class RecorderTransmitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Recorder __instance, ref bool value)
        {
            PlayerVoiceChat own = PlayerVoiceChat.instance;
            if (!own || own.recorder != __instance)
            {
                return;
            }
            Mixer.MicOpen = value;
            value = VoiceGate.Transmit(value, Mixer.Holding);
        }
    }
}
