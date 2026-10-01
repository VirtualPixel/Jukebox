using Jukebox.Models;
using Xunit;

namespace Jukebox.Tests
{
    // A host can turn Jukebox off for the whole lobby. Only an explicit off from a host that has
    // Jukebox counts; a host without it, or one that has not said anything yet, allows it.
    public class HostRuleTests
    {
        [Fact]
        public void NoWordFromTheHostMeansAllowed()
        {
            Assert.True(HostRule.Allowed(inRoom: true, iAmHost: false, mySetting: false, hostSaid: null));
        }

        [Fact]
        public void TheHostSayingOffTurnsItOff()
        {
            Assert.False(HostRule.Allowed(inRoom: true, iAmHost: false, mySetting: true, hostSaid: false));
        }

        [Fact]
        public void TheHostSayingOnAllowsIt()
        {
            Assert.True(HostRule.Allowed(inRoom: true, iAmHost: false, mySetting: false, hostSaid: true));
        }

        [Fact]
        public void AsHostYourOwnSettingDecides()
        {
            Assert.False(HostRule.Allowed(inRoom: true, iAmHost: true, mySetting: false, hostSaid: null));
            Assert.True(HostRule.Allowed(inRoom: true, iAmHost: true, mySetting: true, hostSaid: false));
        }

        [Fact]
        public void OutsideARoomNothingIsOff()
        {
            Assert.True(HostRule.Allowed(inRoom: false, iAmHost: true, mySetting: false, hostSaid: false));
        }

        [Fact]
        public void TurningOffSaysSo()
        {
            Assert.Equal("The host has turned Jukebox off for this lobby.", HostRule.Line(wasAllowed: true, nowAllowed: false, iAmHost: false));
        }

        [Fact]
        public void TurningBackOnSaysSo()
        {
            Assert.Equal("The host has turned Jukebox back on.", HostRule.Line(wasAllowed: false, nowAllowed: true, iAmHost: false));
        }

        [Fact]
        public void NoChangeSaysNothing()
        {
            Assert.Null(HostRule.Line(wasAllowed: true, nowAllowed: true, iAmHost: false));
            Assert.Null(HostRule.Line(wasAllowed: false, nowAllowed: false, iAmHost: false));
        }

        [Fact]
        public void TheHostIsNotToldAboutTheirOwnSwitch()
        {
            Assert.Null(HostRule.Line(wasAllowed: true, nowAllowed: false, iAmHost: true));
        }
    }
}
