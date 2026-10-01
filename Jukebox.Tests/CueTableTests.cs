using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // cues.cfg is the one file a player edits by hand, so its reading rules are pinned here.
    public class CueTableTests
    {
        private static CueTable Parse(params string[] lines) => CueTable.Parse(lines);

        [Fact]
        public void ReadsCueEqualsFile()
        {
            CueTable table = Parse("coin = mycoin");

            Assert.Equal("mycoin", table.Raw("coin"));
        }

        [Fact]
        public void CommentsBlanksAndHalfLinesAreSkipped()
        {
            CueTable table = Parse("# coin = nope", "", "jump =", "= boing", "no equals here", "land = thud");

            Assert.Equal(1, table.Count);
            Assert.Equal("thud", table.Raw("land"));
        }

        [Fact]
        public void CueNamesIgnoreCase()
        {
            CueTable table = Parse("Coin = mycoin");

            Assert.Equal("mycoin", table.Raw("COIN"));
        }

        [Fact]
        public void LaterLineWins()
        {
            CueTable table = Parse("coin = one", "coin = two");

            Assert.Equal("two", table.Raw("coin"));
        }

        [Theory]
        [InlineData("smb_coin", "smb_coin")]
        [InlineData("mario/jump", "jump")]
        [InlineData("smashandgrab/smb_coin", "smb_coin")]
        [InlineData("smw_coin", "coin")]
        public void MatchesWholeNameThenPathTailThenLastWord(string asked, string expected)
        {
            CueTable table = Parse("smb_coin = a", "jump = b", "coin = c");

            Assert.Equal(expected, table.Match(asked));
        }

        [Fact]
        public void WholeNameBeatsLastWord()
        {
            CueTable table = Parse("coin = plain", "smb_coin = exact");

            Assert.Equal("smb_coin", table.Match("smb_coin"));
        }

        [Fact]
        public void NoMatchIsNull()
        {
            CueTable table = Parse("coin = a");

            Assert.Null(table.Match("jump"));
            Assert.Null(table.Match(""));
        }

        [Fact]
        public void FilesSplitOnCommasAndLoseTheirWavEnding()
        {
            Assert.Equal(new[] { "ow1", "ow2", "ow3" }, CueTable.Files(" ow1.wav, ow2 ,, ow3.WAV"));
        }
    }
}
