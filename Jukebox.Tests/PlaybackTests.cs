using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // 2026-09-30 polish pass, the ways a clip went missing for the player who fired it.
    public class PlaybackTests
    {
        // Five deaths or game overs in the stream (sticky, never dropped) and the next plain
        // clip was thrown away before its local copy played, so you heard nothing either.
        [Fact]
        public void AFullStreamStillPlaysYourOwnCopy()
        {
            Assert.Equal(Admit.LocalOnly, VoiceGate.Room(queued: 5, max: 5, anyPlain: false, sticky: false));
        }

        [Fact]
        public void TheCapMakesRoomOrLetsAStickyIn()
        {
            Assert.Equal(Admit.Queue, VoiceGate.Room(queued: 2, max: 5, anyPlain: false, sticky: false));
            Assert.Equal(Admit.MakeRoom, VoiceGate.Room(queued: 5, max: 5, anyPlain: true, sticky: false));
            Assert.Equal(Admit.Queue, VoiceGate.Room(queued: 5, max: 5, anyPlain: false, sticky: true));
        }

        // Leaving a lobby for singleplayer destroys the voice chat, and the mixer kept the dead
        // voice: every clip queued into a stream that never drained, and once five deaths sat in
        // it nothing played at all. Justin's solo run earlier the same day lost sounds.
        [Fact]
        public void NoVoiceChatLetsGoOfTheVoice()
        {
            Assert.False(VoiceGate.StillAttached(voiceChat: false, liveVoice: true));
            Assert.False(VoiceGate.StillAttached(voiceChat: true, liveVoice: false));
            Assert.True(VoiceGate.StillAttached(voiceChat: true, liveVoice: true));
        }

        // A loop shorter than one step of the resampler ran past its end on the audio thread.
        [Fact]
        public void AShortLoopWrapsInsideItself()
        {
            for (double pos = 3; pos < 40; pos += 1.7)
            {
                double wrapped = VoiceGate.Wrap(pos, 3);
                Assert.InRange(wrapped, 0, 2.999999);
            }
        }

        // level_start fired from LoadingUI.StopLoading, which the game director calls while it is
        // still starting, and nothing plays until it is in Main: the level never had a start sound.
        [Fact]
        public void TheLevelStartWaitsForTheLevel()
        {
            var cue = new StartCue();
            cue.Arm();
            Assert.False(cue.Due(main: false));
            Assert.True(cue.Due(main: true));
            Assert.False(cue.Due(main: true));
        }

        [Fact]
        public void ALevelLeftBeforeItStartedSaysNothingLater()
        {
            var cue = new StartCue();
            cue.Arm();
            cue.Clear();
            Assert.False(cue.Due(main: true));
        }
    }
}
