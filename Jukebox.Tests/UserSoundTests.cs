using System.Reflection;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // PlayUserSound is for a mod that voices a moment itself (Smash & Grab spending a life): it
    // plays the player's own file for that look and action, and nothing else. A pack's file, a
    // cues.cfg line or the game's sound never count, so the mod knows to play its own line.
    public class UserSoundTests
    {
        private static readonly SetIndex Mine = SetIndex.Build(new[] { "Mario/death", "Mario/death2", "Mario/jump" }, "sets/");

        [Fact]
        public void YourOwnFilesForTheLookAndActionAreThePick()
        {
            Assert.Equal(new[] { "sets/Mario/death", "sets/Mario/death2" }, Resolver.UserClips("mario", "death", Mine));
        }

        [Fact]
        public void NothingOfYoursIsNoPick()
        {
            Assert.Empty(Resolver.UserClips("Mario", "hurt", Mine));
            Assert.Empty(Resolver.UserClips("Luigi", "death", Mine));
            Assert.Empty(Resolver.UserClips("", "death", Mine));
            Assert.Empty(Resolver.UserClips("Mario", "", Mine));
        }

        [Fact]
        public void ThePublicCallIsThere()
        {
            MethodInfo? play = typeof(Api).GetMethod("PlayUserSound", new[] { typeof(string), typeof(string) });

            Assert.NotNull(play);
            Assert.Equal(typeof(bool), play!.ReturnType);
        }

        [Fact]
        public void ItSaysFalseWhenYouHaveNoFile()
        {
            Assert.False(Api.PlayUserSound("Nobody", "death"));
            Assert.False(Api.PlayUserSound(null!, "death"));
        }
    }
}
