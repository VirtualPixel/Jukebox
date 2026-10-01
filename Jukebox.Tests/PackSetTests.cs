using System;
using System.Collections.Generic;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // A pack from a mod that voices its own moments (Smash & Grab) says which looks have a voice,
    // for the wheel, and never plays on Jukebox's actions, so a line is never heard twice. A file
    // the player drops into their own sets folder still plays. A pack switched off is no set at
    // all.
    public class PackSetTests
    {
        private static readonly HashSet<string> Loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "me/ow1", "sets/Mario/jump", "sg/mario/hurt1", "sg/mario/jump21", "sg/mario/win1", "sg/luigi/jump21"
        };

        private static readonly SetIndex Mine = SetIndex.Build(new[] { "Mario/jump" }, "sets/");

        private static SetIndex SgIndex() => SetIndex.Build(new[] { "mario/hurt1", "mario/jump21", "mario/win1", "luigi/jump21" }, "sg/",
            new Dictionary<string, string> { { "jump21", "jump" }, { "win1", "level_won" } });

        private static PackSet VoicedByTheMod() => new PackSet("sg", SgIndex(), playsActions: false);

        private static SoundPick Resolve(string action, string look, PackSet pack, params string[] cues) =>
            Resolver.Resolve(action, look, Mine, new[] { pack }, CueTable.Parse(cues), Loaded.Contains);

        [Fact]
        public void APackVoicedByItsModPlaysNothingOnAnAction()
        {
            PackSet sg = VoicedByTheMod();

            Assert.Equal(CueSource.Game, Resolve("hurt", "Mario", sg).Source);
            Assert.Equal(CueSource.Game, Resolve("level_won", "Mario", sg).Source);
            Assert.Equal(CueSource.Game, Resolve("jump", "Luigi", sg).Source);
        }

        [Fact]
        public void ItStillFallsBackToCuesCfgLikeNoSetWasThere()
        {
            Assert.Equal(new[] { "me/ow1" }, Resolve("hurt", "Mario", VoicedByTheMod(), "hurt = ow1").Clips);
        }

        [Fact]
        public void ItStillPutsItsLooksOnTheWheel()
        {
            PackSet sg = VoicedByTheMod();

            Assert.True(Resolver.HasSet("Luigi", SetIndex.Empty, new[] { sg }));
            Assert.True(Resolver.WheelShows("Luigi", SetIndex.Empty, new[] { sg }));
            Assert.False(Resolver.WheelShows("Bowser", SetIndex.Empty, new[] { sg }));
        }

        [Fact]
        public void YourOwnFileForTheLookStillPlays()
        {
            SoundPick r = Resolve("jump", "Mario", VoicedByTheMod());

            Assert.Equal(CueSource.Set, r.Source);
            Assert.Equal(new[] { "sets/Mario/jump" }, r.Clips);
        }

        [Fact]
        public void OnlyYourOwnFilesCountAsYourSound()
        {

            Assert.True(Resolver.HasUserSound("Mario", "jump", Mine));
            Assert.True(Resolver.HasUserSound("mario", "jump", Mine));
            Assert.False(Resolver.HasUserSound("Mario", "hurt", Mine));
            Assert.False(Resolver.HasUserSound("Luigi", "jump", Mine));
        }

        [Fact]
        public void APackThatPlaysItsActionsStillDoes()
        {
            PackSet pack = new PackSet("sg", SgIndex());

            Assert.Equal(new[] { "sg/mario/hurt1" }, Resolve("hurt", "Mario", pack).Clips);
        }

        [Fact]
        public void ASwitchedOffPackIsNoSetAndPlaysNothing()
        {
            PackSet pack = new PackSet("sg", SgIndex()) { Enabled = false };

            Assert.Equal(CueSource.Game, Resolve("hurt", "Mario", pack).Source);
            Assert.False(Resolver.HasSet("Luigi", Mine, new[] { pack }));
            Assert.False(Resolver.WheelShows("Luigi", Mine, new[] { pack }));
        }

        [Fact]
        public void ASwitchedOffPackLeavesYourOwnSetsAlone()
        {
            PackSet pack = new PackSet("sg", SgIndex()) { Enabled = false };

            Assert.True(Resolver.HasSet("Mario", Mine, new[] { pack }));
            Assert.Equal(new[] { "sets/Mario/jump" }, Resolve("jump", "Mario", pack).Clips);
        }

        [Fact]
        public void WithOnlyASwitchedOffPackTheWheelShowsEverything()
        {
            PackSet pack = new PackSet("sg", SgIndex()) { Enabled = false };

            Assert.True(Resolver.WheelShows("Bowser", SetIndex.Empty, new[] { pack }));
        }

        [Fact]
        public void TheSwitchHoldsByGuidBeforeAndAfterRegistering()
        {
            var switches = new PackSwitches();
            Assert.True(switches.Enabled("Vippy.SmashAndGrab"));

            switches.Set("Vippy.SmashAndGrab", false);
            Assert.False(switches.Enabled("vippy.smashandgrab"));
            Assert.True(switches.Enabled("Some.Other"));

            switches.Set("Vippy.SmashAndGrab", true);
            Assert.True(switches.Enabled("Vippy.SmashAndGrab"));
        }

        [Fact]
        public void ALookWardrobeShowsUnderAnotherNameFindsThePacksSet()
        {
            // You have a Mario of your own, so Wardrobe shows the pack's as "Mario (Super Mario Bros.)".
            Func<string, string?> packName = look => look == "Mario (Super Mario Bros.)" ? "Mario" : null;
            PackSet sg = new PackSet("sg", SgIndex(), playsActions: false, lookName: packName);

            Assert.True(Resolver.HasSet("Mario (Super Mario Bros.)", SetIndex.Empty, new[] { sg }));
        }


        // 2026-09-30: after a Smash & Grab session Justin found Portal had lost Chell and Sonic most
        // of its cast. Chell has no voice folder at all and the thin folders hold none of the
        // lines that map to an action, so the pack had no set for them and the wheel hid them.
        // The mod now declares its whole roster and a declared look is on the wheel file or not.
        private static PackSet Declaring(params string[] looks) => new PackSet("sg", SgIndex(), playsActions: false, looks: looks);

        [Fact]
        public void ADeclaredLookWithNoFilesIsOnTheWheel()
        {
            PackSet sg = Declaring("Chell", "Amy Rose");

            Assert.True(Resolver.HasSet("Chell", SetIndex.Empty, new[] { sg }));
            Assert.True(Resolver.WheelShows("Chell", SetIndex.Empty, new[] { sg }));
            Assert.True(Resolver.WheelShows("amy rose", SetIndex.Empty, new[] { sg }));
            Assert.True(Resolver.WheelShows("Luigi", SetIndex.Empty, new[] { sg }));
            Assert.False(Resolver.WheelShows("Bowser", SetIndex.Empty, new[] { sg }));
        }

        [Fact]
        public void ADeclaredLookUnderAnotherNameStillCounts()
        {
            Func<string, string?> packName = look => look == "Chell (Portal)" ? "Chell" : null;
            PackSet sg = new PackSet("sg", SgIndex(), playsActions: false, lookName: packName, looks: new[] { "Chell" });

            Assert.True(Resolver.WheelShows("Chell (Portal)", SetIndex.Empty, new[] { sg }));
            Assert.False(Resolver.WheelShows("Bowser (Portal)", SetIndex.Empty, new[] { sg }));
        }

        [Fact]
        public void APackOfOnlyDeclaredLooksStillTakesTheWheel()
        {
            PackSet sg = new PackSet("sg", SetIndex.Empty, playsActions: false, looks: new[] { "Chell" });

            Assert.True(Resolver.WheelShows("Chell", SetIndex.Empty, new[] { sg }));
            Assert.False(Resolver.WheelShows("Bowser", SetIndex.Empty, new[] { sg }));
        }

        [Fact]
        public void ASwitchedOffPackDeclaresNothing()
        {
            PackSet sg = Declaring("Chell");
            sg.Enabled = false;

            Assert.False(Resolver.HasSet("Chell", Mine, new[] { sg }));
            Assert.True(Resolver.HasSet("Mario", Mine, new[] { sg }));
        }

        [Fact]
        public void DeclaredLooksPlayNothing()
        {
            PackSet sg = new PackSet("sg", SgIndex(), playsActions: true, looks: new[] { "Chell" });

            Assert.Equal(CueSource.Game, Resolve("hurt", "Chell", sg).Source);
        }
    }
}
