using System.Collections.Generic;

namespace Jukebox.Models
{
    // Where every key of the old numbered sections lives now.
    internal static class ConfigMoves
    {
        public readonly struct Move
        {
            public readonly string FromSection;
            public readonly string FromKey;
            public readonly string ToSection;
            public readonly string ToKey;

            public Move(string fromSection, string fromKey, string toSection, string toKey)
            {
                FromSection = fromSection;
                FromKey = fromKey;
                ToSection = toSection;
                ToKey = toKey;
            }
        }

        public static readonly IReadOnlyList<Move> All = new[]
        {
            new Move("1 Start here", "Volume", "General", "Volume"),
            new Move("1 Start here", "DirectSound", "Advanced", "DirectSound"),
            new Move("1 Start here", "MusicThroughVoice", "Advanced", "MusicThroughVoice"),
            new Move("1 Start here", "QuietInMenus", "Advanced", "QuietInMenus"),
            new Move("2 Mix", "DuckUnderVoice", "Advanced", "DuckUnderVoice"),
            new Move("2 Mix", "DuckRatio", "Advanced", "DuckRatio"),
            new Move("2 Mix", "HearOwnSounds", "Advanced", "HearOwnSounds"),
            new Move("2 Mix", "OwnCopyVolume", "Advanced", "OwnCopyVolume"),
            new Move("3 Soundboard", "Hotkeys", "Advanced", "Hotkeys"),
            new Move("3 Soundboard", "ReloadKey", "Advanced", "ReloadKey"),
            new Move("3 Soundboard", "SoundboardVolume", "Advanced", "SoundboardVolume"),
            new Move("4 Endpoint", "Endpoint", "Soundboard door", "Endpoint"),
            new Move("4 Endpoint", "Port", "Soundboard door", "Port"),
            new Move("9 Misc", "Logging", "Advanced", "Logging"),
        };
    }
}
