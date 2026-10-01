using System.Reflection;
using HarmonyLib;
using Jukebox.Configuration;
using Jukebox.Models;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace Jukebox.Services
{
    // Finds the recorder behind your own voice and hands its live voice object to the mixer,
    // which writes clips into it on the way to the encoder. Every recorder restart rebuilds the
    // voice object, so this looks again once a second and re-attaches when it changes. Photon
    // Voice is not publicized, hence the fields by name.
    //
    // Every frame it also keeps the way out clear for a clip: the transmit gate held open (see
    // RecorderTransmitPatch) and the recorder's WebRTC speech detector off while one sounds,
    // since that detector drops every frame it hears no voice in.
    internal static class VoiceMix
    {
        private static readonly FieldInfo? VoiceField = AccessTools.Field(typeof(Recorder), "voice");
        private static readonly FieldInfo? InputField = AccessTools.Field(typeof(Recorder), "inputSource");
        private static object? attached;
        private static float nextCheck;
        private static float lastSounding = -10f;

        private static Recorder? dspOwner;
        private static WebRtcAudioDsp? dsp;
        // The detector as the game had it, taken when a clip first turned it off.
        private static bool? detectorGame;

        public static void Tick()
        {
            PlayerVoiceChat voice = PlayerVoiceChat.instance;
            if (!voice || !voice.recorder)
            {
                if (Mixer.Attached)
                {
                    Let();
                }
                return;
            }
            Recorder recorder = voice.recorder;
            float now = Time.unscaledTime;
            if (now >= nextCheck)
            {
                nextCheck = now + 1f;
                Watch(recorder);
            }
            if (Mixer.Playing)
            {
                lastSounding = now;
            }
            bool hold = Mixer.Attached && VoiceGate.Hold(Mixer.Playing, now - lastSounding);
            Mixer.Holding = hold;
            Detector(recorder, hold);
            if (PluginConfig.HearWhatGoesOut.Value && Gate.Active && Mixer.InPlace)
            {
                voice.OverrideHearSelf(0f, 0.2f);
            }
        }

        private static void Watch(Recorder recorder)
        {
            object? live = VoiceField?.GetValue(recorder);
            if (live is not LocalVoice local || !VoiceGate.StillAttached(true, local != LocalVoiceAudioDummy.Dummy))
            {
                Let();
                return;
            }
            if (live == attached)
            {
                return;
            }
            IAudioDesc? input = InputField?.GetValue(recorder) as IAudioDesc;
            if (Mixer.Attach(local, input?.SamplingRate ?? 0, input?.Channels ?? 1))
            {
                attached = live;
            }
        }

        private static void Let()
        {
            attached = null;
            Mixer.Detach();
            Mixer.Holding = false;
        }

        private static void Detector(Recorder recorder, bool hold)
        {
            if (dspOwner != recorder)
            {
                dspOwner = recorder;
                dsp = recorder.GetComponent<WebRtcAudioDsp>();
                detectorGame = null;
            }
            if (!dsp)
            {
                return;
            }
            if (hold && detectorGame == null)
            {
                detectorGame = dsp!.VAD;
            }
            if (detectorGame == null)
            {
                return;
            }
            bool want = VoiceGate.DetectorOn(detectorGame.Value, hold);
            if (dsp!.VAD != want)
            {
                dsp.VAD = want;
            }
            if (!hold)
            {
                detectorGame = null;
            }
        }
    }
}
