using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // 2026-09-30, Justin after the polish deploy: "the in-game sounds are quiet". Smash & Grab
    // lines, kit sounds and effects, all through Jukebox, were louder on the Sept 21 build.
    // His settings: SG Volume 0.5 with @0.7 trims, Jukebox Volume 1, OwnCopyVolume 0.85,
    // DuckRatio 0.5, game master 40% (the game's own curve puts that at about -16.3 dB),
    // proximity voice 100% (0 dB). He was playing solo.
    public class LevelTests
    {
        private const float MasterAt40 = -16.3f;

        private static float JustinsCopy() => Levels.Copy(Levels.Trimmed(0.5f, 0.7f) * 1f, 0.85f);

        // The polish pass sent the solo copy into "Microphone Master". On Sept 21 it skipped the
        // mixer, and in a lobby it has always gone through "Microphone", the voices' group,
        // which the game runs 20 dB up. Microphone Master has none of that and sits under the
        // master slider, so at his 40% the copy came out about 16 dB under Sept 21.
        [Fact]
        public void SoloCopyGoesThroughTheVoicesGroup()
        {
            Assert.Equal(Bus.Voice, Levels.CopyBus(voiceChat: false, voicePrefab: true));
            Assert.Equal(Bus.Voice, Levels.CopyBus(voiceChat: true, voicePrefab: true));
        }

        [Fact]
        public void SoloCopyIsAtLeastAsLoudAsSept21()
        {
            float copy = JustinsCopy();
            float sept21 = Levels.Heard(copy, Bus.None, 0f);
            float now = Levels.Heard(copy, Levels.CopyBus(voiceChat: false, voicePrefab: true), MasterAt40);
            Assert.True(now >= sept21, "solo copy " + now + " against Sept 21's " + sept21);
        }

        // Nowhere to send it (no voice chat, no prefab to read the group off): the Sept 21 way,
        // straight out, rather than a group that drops it 20 dB.
        [Fact]
        public void NoVoiceGroupFallsBackToSept21()
        {
            Assert.Equal(Bus.None, Levels.CopyBus(voiceChat: false, voicePrefab: false));
        }

        // The lobby copy did not move: Microphone then and now.
        [Fact]
        public void LobbyCopyIsWhereItWas()
        {
            float copy = JustinsCopy();
            Assert.Equal(Levels.Heard(copy, Bus.Voice, MasterAt40), Levels.Heard(copy, Levels.CopyBus(true, true), MasterAt40), 6);
        }

        // Volume, OwnCopyVolume and the clip's own gain all at their top with the game's sliders
        // up: a clip at the loader's peak ceiling went out of the voices' group at 3.8.
        [Fact]
        public void TopSettingsDoNotClip()
        {
            float copy = Levels.Copy(Levels.MaxGain, 2f);
            Assert.True(Levels.Heard(copy, Bus.Voice, 0f) * Levels.PeakCeiling <= 1f);
        }

        // DuckRatio says the most a clip reaches while you talk is that fraction of your voice.
        // The duck was worked out as if every clip played at gain 1, so a clip already asked for
        // at 0.35 was pushed down to a sixth of his voice instead of left where it was.
        [Fact]
        public void AQuietClipIsNotDuckedFurther()
        {
            float voice = 0.05f;
            float duck = Levels.DuckTarget(true, 0.5f, voice);
            float clip = Levels.TargetRms * Levels.InStream(0.35f, duck);
            Assert.Equal(Levels.TargetRms * 0.35f, clip, 5);
        }

        [Fact]
        public void ALoudClipIsHeldAtTheRatio()
        {
            float voice = 0.05f;
            float duck = Levels.DuckTarget(true, 0.5f, voice);
            Assert.Equal(0.5f * voice, Levels.TargetRms * Levels.InStream(1f, duck), 5);
            Assert.Equal(0.5f * voice, Levels.TargetRms * Levels.InStream(2f, duck), 5);
        }

        [Fact]
        public void NotTalkingLeavesEveryClipAlone()
        {
            float duck = Levels.DuckTarget(false, 0.5f, 0f);
            Assert.Equal(2f, Levels.InStream(2f, duck), 5);
            Assert.Equal(0.35f, Levels.InStream(0.35f, duck), 5);
        }

        // The request to the stream: SG Volume and a trim, then Jukebox's Volume, once each.
        [Fact]
        public void RequestToStreamAppliesEachSettingOnce()
        {
            float gain = Levels.Trimmed(0.5f, 0.7f) * 1f;
            Assert.Equal(0.35f, gain, 5);
            Assert.Equal(0.35f, Levels.InStream(Levels.Clamp(gain), Levels.DuckTarget(false, 0.5f, 0f)), 5);
            Assert.Equal(0.35f * Levels.RoomGain * 0.85f, JustinsCopy(), 5);
        }
    }
}
