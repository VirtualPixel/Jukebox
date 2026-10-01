namespace Jukebox.Models
{
    // The host's switch. A host can install Jukebox only to turn it off for the lobby; while it
    // is off nobody's Jukebox does anything. Only an explicit off from a host that has Jukebox
    // counts: a host without it says nothing, and nothing said means allowed, so a join before
    // the host's word arrives never flashes an "off".
    internal static class HostRule
    {
        public const string OffLine = "The host has turned Jukebox off for this lobby.";
        public const string OnLine = "The host has turned Jukebox back on.";

        // inRoom: a multiplayer room, not singleplayer. hostSaid: the last word from the current
        // host, null when there has been none.
        public static bool Allowed(bool inRoom, bool iAmHost, bool mySetting, bool? hostSaid)
        {
            if (!inRoom)
            {
                return true;
            }
            if (iAmHost)
            {
                return mySetting;
            }
            return hostSaid != false;
        }

        // The HUD line for a change, or null. The host flipped it themselves and needs no line.
        public static string? Line(bool wasAllowed, bool nowAllowed, bool iAmHost)
        {
            if (iAmHost || wasAllowed == nowAllowed)
            {
                return null;
            }
            return nowAllowed ? OnLine : OffLine;
        }
    }
}
