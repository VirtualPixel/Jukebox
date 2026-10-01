using System.Linq;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // Every key the numbered sections held finds a home, so nobody's setting is lost.
    public class ConfigMovesTests
    {
        [Theory]
        [InlineData("1 Start here", "Volume")]
        [InlineData("1 Start here", "DirectSound")]
        [InlineData("1 Start here", "MusicThroughVoice")]
        [InlineData("1 Start here", "QuietInMenus")]
        [InlineData("2 Mix", "DuckUnderVoice")]
        [InlineData("2 Mix", "DuckRatio")]
        [InlineData("2 Mix", "HearOwnSounds")]
        [InlineData("2 Mix", "OwnCopyVolume")]
        [InlineData("3 Soundboard", "Hotkeys")]
        [InlineData("3 Soundboard", "ReloadKey")]
        [InlineData("3 Soundboard", "SoundboardVolume")]
        [InlineData("4 Endpoint", "Endpoint")]
        [InlineData("4 Endpoint", "Port")]
        [InlineData("9 Misc", "Logging")]
        public void OldKeyHasANewHome(string section, string key)
        {
            Assert.Single(ConfigMoves.All, m => m.FromSection == section && m.FromKey == key);
        }

        [Fact]
        public void NoTwoOldKeysLandOnOne()
        {
            Assert.Equal(ConfigMoves.All.Count, ConfigMoves.All.Select(m => m.ToSection + "/" + m.ToKey).Distinct().Count());
        }
    }
}
