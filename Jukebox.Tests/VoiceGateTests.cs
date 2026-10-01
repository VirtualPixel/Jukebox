using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // 2026-09-30, Justin solo in a hosted lobby: "I'm not convinced the sound effects are playing
    // through the voice". The game shuts the recorder's transmit gate whenever push to talk is
    // up or you are muted, and Photon drops the whole buffer before any processor sees it, so a
    // clip queued then never went out (and piled up to burst on the next keypress). While the
    // mic is open, the WebRTC speech detector on the recorder threw away every frame it did not
    // hear a voice in, which is most of a coin or a jingle. And the mixer stepped through clips
    // as if the mic always ran at 48 kHz.
    public class VoiceGateTests
    {
        [Fact]
        public void AClipOpensTheGateWithPushToTalkUp()
        {
            Assert.True(VoiceGate.Transmit(gameWants: false, VoiceGate.Hold(sounding: true, sinceSounding: 0f)));
        }

        [Fact]
        public void NothingPlayingLeavesTheGateToTheGame()
        {
            Assert.False(VoiceGate.Transmit(gameWants: false, VoiceGate.Hold(sounding: false, sinceSounding: 5f)));
            Assert.True(VoiceGate.Transmit(gameWants: true, VoiceGate.Hold(sounding: false, sinceSounding: 5f)));
        }

        [Fact]
        public void TheGateStaysOpenLongEnoughForTheLastFrame()
        {
            Assert.True(VoiceGate.Hold(sounding: false, sinceSounding: 0.1f));
            Assert.False(VoiceGate.Hold(sounding: false, sinceSounding: 1f));
        }

        [Fact]
        public void AMutedMicStaysMutedUnderAClip()
        {
            Assert.False(VoiceGate.KeepMic(gameWants: false, talking: true, sounding: true));
            Assert.False(VoiceGate.KeepMic(gameWants: false, talking: false, sounding: false));
        }

        [Fact]
        public void AnOpenMicGoesOutUnderAClipOnlyWhileYouTalk()
        {
            Assert.True(VoiceGate.KeepMic(gameWants: true, talking: true, sounding: true));
            Assert.False(VoiceGate.KeepMic(gameWants: true, talking: false, sounding: true));
        }

        [Fact]
        public void WithNoClipTheMicIsLeftAlone()
        {
            Assert.True(VoiceGate.KeepMic(gameWants: true, talking: false, sounding: false));
        }

        [Fact]
        public void TheSpeechDetectorStandsAsideWhileAClipPlays()
        {
            Assert.False(VoiceGate.DetectorOn(gameSetting: true, hold: true));
            Assert.True(VoiceGate.DetectorOn(gameSetting: true, hold: false));
            Assert.False(VoiceGate.DetectorOn(gameSetting: false, hold: false));
        }

        [Fact]
        public void AClipKeepsItsLengthOnASlowerMic()
        {
            Assert.Equal(2.0, VoiceGate.Step(1f, 48000, 24000), 6);
            Assert.Equal(1.0, VoiceGate.Step(1f, 48000, 48000), 6);
            Assert.Equal(48000.0 / 44100.0 * 1.5, VoiceGate.Step(1.5f, 48000, 44100), 6);
        }

        [Fact]
        public void AnUnknownMicRateIsTakenAsTheClipRate()
        {
            Assert.Equal(1.0, VoiceGate.Step(1f, 48000, 0), 6);
        }
    }
}
