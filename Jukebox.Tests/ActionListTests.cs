using System.Linq;
using System.Text.RegularExpressions;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    public class ActionListTests
    {
        [Fact]
        public void NamesAreUnique()
        {
            Assert.Equal(ActionList.All.Count, ActionList.All.Select(a => a.Name).Distinct().Count());
        }

        // A name has to survive being a file name, and must not end in a digit, which would be
        // read as a variant number (jump2 is jump).
        [Fact]
        public void NamesAreLowercaseWordsWithNoTrailingDigit()
        {
            Assert.All(ActionList.All, a => Assert.Matches(new Regex("^[a-z]+(_[a-z]+)*$"), a.Name));
        }

        [Fact]
        public void EveryNameFoldsToItself()
        {
            Assert.All(ActionList.All, a => Assert.Equal(a.Name, SetIndex.ActionOf(a.Name)));
        }

        [Fact]
        public void TheReadmeListsEveryAction()
        {
            string text = ActionList.Readme("sets", "cues.cfg");

            Assert.All(ActionList.All, a => Assert.Contains("  " + a.Name + " ", text));
        }
    }
}
