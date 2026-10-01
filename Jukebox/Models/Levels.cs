using System;

namespace Jukebox.Models
{
    // Where a clip goes on your end, past the gain it was asked at.
    internal enum Bus
    {
        // No mixer group: straight to the listener, past every volume slider.
        None,
        // "Microphone", the group every voice in the room plays through, 20 dB up and under
        // the proximity voice and master sliders.
        Voice
    }

    // The gain of a clip from the moment a mod asks for it to what comes out of your speakers
    // and what goes into your voice. Kept apart from the mixer so it can be checked by number.
    internal static class Levels
    {
        // Every clip is brought to this average level (RMS) with this peak ceiling on load, so
        // nothing in the stream can clip and nothing arrives twice as loud as the last thing.
        public const float TargetRms = 0.055f;
        public const float PeakCeiling = 0.27f;

        // The game's default per-player voice gain times a nearby listener's distance falloff.
        public const float RoomGain = 0.5f * 0.7f;

        public const float MaxGain = 2f;

        // What the voices' group adds in the game's "Sounds On" snapshot.
        public const float VoiceDb = 20f;

        public static float Clamp(float gain) => gain < 0f ? 0f : gain > MaxGain ? MaxGain : gain;

        // A "name@0.6" trim on what was asked for.
        public static float Trimmed(float gain, float trim) => gain * Math.Max(0f, Math.Min(trim, MaxGain));

        // The most the copy's source may be set to: a clip at the peak ceiling comes out of the
        // voices' group at full scale, and the sliders after it only ever take away.
        public static readonly float CopyCeiling = 1f / (PeakCeiling * (float)Math.Pow(10, VoiceDb / 20f));

        // Your copy's volume on its source, before the mixer group.
        public static float Copy(float gain, float ownCopy) => Math.Min(Clamp(gain) * RoomGain * ownCopy, CopyCeiling);

        // Your copy goes where the room's voices go, the voice chat's group in a lobby and the
        // same group off the voice prefab in singleplayer, so it sounds the same in both and the
        // game's sliders reach it. With neither, it plays straight out as it did on Sept 21.
        public static Bus CopyBus(bool voiceChat, bool voicePrefab) => voiceChat || voicePrefab ? Bus.Voice : Bus.None;

        public static float BusDb(Bus bus) => bus == Bus.Voice ? VoiceDb : 0f;

        // What comes out of your speakers for a copy at that volume: the group, then the game's
        // sliders (master and proximity voice, in dB), unless the copy skips the mixer.
        public static float Heard(float copy, Bus bus, float slidersDb) =>
            copy * (float)Math.Pow(10, (BusDb(bus) + (bus == Bus.None ? 0f : slidersDb)) / 20f);

        // The most any clip in the stream may be played at while you talk: clips are levelled
        // to TargetRms at gain 1, so this lands one at DuckRatio of your voice. Not talking,
        // nothing is held down.
        public static float DuckTarget(bool voiceGoesOut, float ratio, float envelope) =>
            voiceGoesOut ? Clamp(ratio * envelope / TargetRms) : MaxGain;

        // Down at once, back up over a few buffers.
        public static float DuckStep(float duck, float target) => target < duck ? target : Math.Min(target, duck + 0.08f);

        // A clip already under the duck stays where it was asked for.
        public static float InStream(float cueGain, float duck) => Math.Min(cueGain, duck);
    }
}
