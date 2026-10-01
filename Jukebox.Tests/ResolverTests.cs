using System;
using System.Collections.Generic;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // Where a sound for an action comes from: the look you are wearing, then cues.cfg, then the
    // game's own sound. "off" in cues.cfg silences it.
    public class ResolverTests
    {
        private static readonly HashSet<string> Loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "me/boing", "me/thud", "me/ow1", "me/ow2", "me/wilhelm", "coin",
            "sets/Mario/jump", "sets/Mario/jump2", "sg/mario/hurt1", "sg/mario/jump1", "sg/luigi/jump1"
        };

        private static readonly SetIndex Mine = SetIndex.Build(new[] { "Mario/jump", "Mario/jump2" }, "sets/");
        private static readonly PackSet[] Packs = { new PackSet("sg", SetIndex.Build(new[] { "mario/hurt1", "mario/jump1", "luigi/jump1" }, "sg/")) };

        private static SoundPick Resolve(string action, string? look, params string[] cues) =>
            Resolver.Resolve(action, look, Mine, Packs, CueTable.Parse(cues), Loaded.Contains);

        [Fact]
        public void TheLooksOwnSetComesFirst()
        {
            SoundPick r = Resolve("jump", "Mario", "jump = boing");

            Assert.Equal(CueSource.Set, r.Source);
            Assert.Equal(new[] { "sets/Mario/jump", "sets/Mario/jump2" }, r.Clips);
        }

        [Fact]
        public void YourOwnSetBeatsAPacksSetForTheSameLook()
        {
            SoundPick r = Resolve("jump", "mario");

            Assert.Equal(new[] { "sets/Mario/jump", "sets/Mario/jump2" }, r.Clips);
        }

        [Fact]
        public void APackFillsWhatYourSetLacks()
        {
            SoundPick r = Resolve("hurt", "Mario", "hurt = ow1");

            Assert.Equal(CueSource.Set, r.Source);
            Assert.Equal(new[] { "sg/mario/hurt1" }, r.Clips);
        }

        [Fact]
        public void AMissingActionFallsBackToCuesCfg()
        {
            SoundPick r = Resolve("land", "Mario", "land = thud");

            Assert.Equal(CueSource.Cues, r.Source);
            Assert.Equal(new[] { "me/thud" }, r.Clips);
        }

        [Fact]
        public void NoLookGoesStraightToCuesCfg()
        {
            SoundPick r = Resolve("jump", null, "jump = boing");

            Assert.Equal(CueSource.Cues, r.Source);
        }

        [Fact]
        public void NothingMappedLeavesTheGamesOwnSound()
        {
            SoundPick r = Resolve("crouch", "Mario");

            Assert.Equal(CueSource.Game, r.Source);
            Assert.Empty(r.Clips);
        }

        [Fact]
        public void OffSilencesTheAction()
        {
            SoundPick r = Resolve("land", "Luigi", "land = off");

            Assert.Equal(CueSource.Off, r.Source);
            Assert.Empty(r.Clips);
        }

        [Fact]
        public void OffIsCaseBlind()
        {
            Assert.Equal(CueSource.Off, Resolve("land", null, "land = OFF").Source);
        }

        [Fact]
        public void ALooksSetBeatsOff()
        {
            Assert.Equal(CueSource.Set, Resolve("jump", "Mario", "jump = off").Source);
        }

        [Fact]
        public void ALevelTrimStillFindsTheFile()
        {
            SoundPick r = Resolve("death", null, "death = wilhelm@0.6");

            Assert.Equal(new[] { "me/wilhelm@0.6" }, r.Clips);
        }

        [Fact]
        public void AFileThatIsNotThereIsDropped()
        {
            SoundPick r = Resolve("hurt", null, "hurt = ow1, nope, ow2");

            Assert.Equal(new[] { "me/ow1", "me/ow2" }, r.Clips);
        }

        [Fact]
        public void OnlyTheExactActionLineCounts()
        {
            Assert.Equal(CueSource.Game, Resolve("land_hard", null, "land = thud").Source);
        }

        [Fact]
        public void HasASetFromYourFolderOrAPack()
        {
            Assert.True(Resolver.HasSet("Mario", Mine, Packs));
            Assert.True(Resolver.HasSet("luigi", Mine, Packs));
            Assert.False(Resolver.HasSet("Bowser", Mine, Packs));
        }
    
        [Fact]
        public void TheWheelShowsOnlyLooksWithASet()
        {
            Assert.True(Resolver.WheelShows("Mario", Mine, Packs));
            Assert.False(Resolver.WheelShows("Bowser", Mine, Packs));
        }

        [Fact]
        public void WithNoSetAnywhereTheWheelShowsEverything()
        {
            Assert.True(Resolver.WheelShows("Bowser", SetIndex.Empty, new PackSet[0]));
        }
    }
}
