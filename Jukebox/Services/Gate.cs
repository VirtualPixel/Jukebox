using Jukebox.Configuration;

namespace Jukebox.Services
{
    // Whether Jukebox does anything at all right now: your own On switch, and the host's.
    internal static class Gate
    {
        public static bool Active => PluginConfig.On.Value && HostSwitch.Allowed;
    }
}
