using Jukebox.Configuration;

namespace Jukebox.Services
{
    // With Jukebox in, the Wardrobe wheel shows the looks that have a sound set: a folder of
    // yours under Jukebox/sets, or one a mod ships or declares as its own. With every mod of the
    // collection installed that is the whole cast again, each with its voice. Wardrobe asks the filter every time
    // it builds the wheel, so a set added on disk shows up on the next open. The filter stands
    // down (every starred look shows) when the config switch is off, when Jukebox is off for
    // the lobby, and while there is no set anywhere yet.
    internal static class WheelTakeover
    {
        public static void Start()
        {
            Wardrobe.Api.SetWheelFilter("Vippy.Jukebox", Shows);
        }

        private static bool Shows(string look) =>
            !PluginConfig.WheelOnlyLooksWithSounds.Value || !Gate.Active || SoundSets.WheelShows(look);
    }
}
