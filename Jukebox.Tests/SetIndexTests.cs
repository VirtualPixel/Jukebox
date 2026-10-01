using System.Collections.Generic;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // A look's set is a folder of files named after actions; any number of variants per action.
    public class SetIndexTests
    {
        [Theory]
        [InlineData("jump", "jump")]
        [InlineData("jump2", "jump")]
        [InlineData("JUMP21", "jump")]
        [InlineData("hurt_1", "hurt")]
        [InlineData("land_hard", "land_hard")]
        [InlineData("land_hard3", "land_hard")]
        public void VariantNumbersFoldIntoTheAction(string stem, string action)
        {
            Assert.Equal(action, SetIndex.ActionOf(stem));
        }

        [Fact]
        public void AliasesRenameAPacksOwnFileNames()
        {
            Dictionary<string, string> aliases = new Dictionary<string, string> { { "win", "level_won" }, { "attack", "melee_swing" } };

            Assert.Equal("level_won", SetIndex.ActionOf("win1", aliases));
            Assert.Equal("melee_swing", SetIndex.ActionOf("Attack3", aliases));
            Assert.Equal("jump", SetIndex.ActionOf("jump21", aliases));
        }

        [Fact]
        public void AnAliasOnTheWholeNameBeatsTheFoldedOne()
        {
            Dictionary<string, string> aliases = new Dictionary<string, string> { { "gun", "gun" }, { "gun_big", "grenade" } };

            Assert.Equal("grenade", SetIndex.ActionOf("gun_big", aliases));
            Assert.Equal("gun", SetIndex.ActionOf("gun2", aliases));
        }

        [Fact]
        public void VariantsOfOneActionAreAllKept()
        {
            SetIndex index = SetIndex.Build(new[] { "Mario/jump", "Mario/jump2", "Mario/hurt" }, "sets/");

            Assert.Equal(new[] { "sets/Mario/jump", "sets/Mario/jump2" }, index.Clips("mario", "jump"));
        }

        [Fact]
        public void FolderMapsOntoALookName()
        {
            Dictionary<string, string> looks = new Dictionary<string, string> { { "chunli", "Chun-Li" } };
            SetIndex index = SetIndex.Build(new[] { "chunli/hurt1" }, "sg/", null, looks);

            Assert.NotNull(index.Clips("Chun-Li", "hurt"));
        }

        [Fact]
        public void AFileLooseAtTheRootIsNoLooksSet()
        {
            SetIndex index = SetIndex.Build(new[] { "jump" }, "sets/");

            Assert.False(index.Has("jump"));
        }

        [Fact]
        public void HasASetOnlyWithAClipInIt()
        {
            SetIndex index = SetIndex.Build(new[] { "Mario/jump" }, "sets/");

            Assert.True(index.Has("MARIO"));
            Assert.False(index.Has("Luigi"));
        }
    
        [Fact]
        public void FilesThatAreNoActionMakeNoSet()
        {
            SetIndex index = SetIndex.Build(new[] { "Mario/idle1", "Mario/smb_coin", "Luigi/jump" }, "sg/", null, null, ActionList.Known);

            Assert.False(index.Has("Mario"));
            Assert.True(index.Has("Luigi"));
        }
    }
}
