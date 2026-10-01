using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // A set folder is named after a Wardrobe look by hand, so the match forgives case and the
    // characters a folder name cannot hold.
    public class LookFolderTests
    {
        [Theory]
        [InlineData("Mario", "mario")]
        [InlineData("Master Chief", "master chief")]
        [InlineData("What? Me Worry", "What Me Worry")]
        [InlineData("AC/DC", "ACDC")]
        [InlineData("Star: \"The\" <Best>", "Star The Best")]
        [InlineData("Dr. Robotnik.", "Dr. Robotnik")]
        public void FolderMatchesLook(string look, string folder)
        {
            Assert.True(LookFolder.Same(folder, look));
        }

        [Theory]
        [InlineData("Mario", "Luigi")]
        [InlineData("Chun-Li", "Chun Li")]
        public void DifferentNamesStayDifferent(string look, string folder)
        {
            Assert.False(LookFolder.Same(folder, look));
        }
    }
}
