using System;
using System.Threading.Tasks;
using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // 2026-09-30 polish pass: the variant picker rerolled until it got a clip other than the last
    // one, so a cues.cfg line naming the same file twice ("jump = boing, boing") spun forever on
    // the second jump and froze the game.
    public class VariantTests
    {
        private static string PickWithin(string[] clips, string? previous, Func<int, int> roll)
        {
            Task<string> pick = Task.Run(() => Variant.Pick(clips, previous, roll));
            Assert.True(pick.Wait(TimeSpan.FromSeconds(2)), "the pick never came back");
            return pick.Result;
        }

        [Fact]
        public void TheSameFileTwiceStillPlays()
        {
            var random = new Random(1);
            Assert.Equal("boing", PickWithin(new[] { "boing", "boing" }, "boing", random.Next));
        }

        [Fact]
        public void AnotherVariantIsPickedWhenThereIsOne()
        {
            var random = new Random(2);
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal("b", PickWithin(new[] { "a", "b", "a" }, "a", random.Next));
            }
        }

        [Fact]
        public void EveryVariantComesUp()
        {
            var random = new Random(3);
            var seen = new System.Collections.Generic.HashSet<string>();
            string? last = null;
            for (int i = 0; i < 200; i++)
            {
                last = PickWithin(new[] { "a", "b", "c" }, last, random.Next);
                seen.Add(last);
            }
            Assert.Equal(3, seen.Count);
        }
    }
}
