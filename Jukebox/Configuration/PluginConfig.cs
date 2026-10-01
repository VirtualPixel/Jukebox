using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Jukebox.Models;
using UnityEngine;

namespace Jukebox.Configuration
{
    // A few plain keys up top; everything a normal player never needs sits under Advanced.
    // The sounds themselves are not here at all: they are files (cues.cfg and the sets folder).
    internal static class PluginConfig
    {
        public const string General = "General";
        public const string Door = "Soundboard door";
        public const string Advanced = "Advanced";

        public static ConfigEntry<bool> On = null!;
        public static ConfigEntry<float> Volume = null!;
        public static ConfigEntry<bool> AllowedInMyLobby = null!;
        public static ConfigEntry<bool> WheelOnlyLooksWithSounds = null!;

        public static ConfigEntry<bool> Endpoint = null!;
        public static ConfigEntry<int> EndpointPort = null!;

        public static ConfigEntry<bool> DirectSound = null!;
        public static ConfigEntry<bool> MusicThroughVoice = null!;
        public static ConfigEntry<bool> QuietInMenus = null!;
        public static ConfigEntry<bool> DuckUnderVoice = null!;
        public static ConfigEntry<float> DuckRatio = null!;
        public static ConfigEntry<bool> HearOwnSounds = null!;
        public static ConfigEntry<float> HearOwnSoundsVolume = null!;
        public static ConfigEntry<string> Hotkeys = null!;
        public static ConfigEntry<KeyCode> ReloadKey = null!;
        public static ConfigEntry<float> SoundboardVolume = null!;
        public static ConfigEntry<bool> Logging = null!;
        public static ConfigEntry<bool> HearWhatGoesOut = null!;

        public static void Init(ConfigFile config)
        {
            Migrate(config);

            On = config.Bind(General, "On", true,
                "Jukebox plays your action sounds and anything a mod asks for. Off, it plays nothing and leaves the game's own sounds alone.");
            Volume = config.Bind(General, "Volume", 1f,
                new ConfigDescription("Everything Jukebox plays, scaled by this. 1 is the level the clip was made at.", new AcceptableValueRange<float>(0f, 2f)));
            AllowedInMyLobby = config.Bind(General, "AllowedInMyLobby", true,
                "When you host: off turns Jukebox off for everyone in your lobby who has it, you included, and tells them so. Changes take effect at once.");
            WheelOnlyLooksWithSounds = config.Bind(General, "WheelOnlyLooksWithSounds", true,
                "The Wardrobe wheel shows only looks that have a sound set (a folder under Jukebox/sets, or one a mod ships). Off shows every starred look.");

            Endpoint = config.Bind(Door, "Endpoint", false,
                "Listen on 127.0.0.1 so soundboard software or a stream deck can play a clip. POST /play {\"name\":\"airhorn\"} and GET /clips. This machine only, nothing reaches the network.");
            EndpointPort = config.Bind(Door, "Port", 8793,
                new ConfigDescription("The port the endpoint listens on.", new AcceptableValueRange<int>(1024, 65535)));

            DirectSound = config.Bind(Advanced, "DirectSound", true,
                "In a room where everyone has Jukebox, send clips straight to them instead of through your voice. They play at your Semibot, clean, and your microphone stays yours. Off, everything always goes through your voice.");
            MusicThroughVoice = config.Bind(Advanced, "MusicThroughVoice", false,
                "Let a looping track ride your voice in a room with plain clients in it. Off means loops stay yours alone there; one-shots still go out.");
            QuietInMenus = config.Bind(Advanced, "QuietInMenus", true,
                "Nothing plays until you are somewhere the room can see you: the truck, a level, the shop, the arena.");
            DuckUnderVoice = config.Bind(Advanced, "DuckUnderVoice", true,
                "Hold clips under your own voice while you talk, so a line over a song still reads.");
            DuckRatio = config.Bind(Advanced, "DuckRatio", 0.5f,
                new ConfigDescription("With DuckUnderVoice on, the most a clip may reach while you talk, as a fraction of your voice level: 0.5 is half as loud as you.", new AcceptableValueRange<float>(0.1f, 1f)));
            HearOwnSounds = config.Bind(Advanced, "HearOwnSounds", true,
                "Play your own copy of everything you send. The game keeps your voice silent locally, so without this you play deaf.");
            HearOwnSoundsVolume = config.Bind(Advanced, "OwnCopyVolume", 0.85f,
                new ConfigDescription("How loud your own copy is against what the room hears.", new AcceptableValueRange<float>(0f, 2f)));
            Hotkeys = config.Bind(Advanced, "Hotkeys", "",
                "Keys that play one of your own clips, comma separated: F1=airhorn, F2=silly/boing. Names are files under the sounds folder, with or without .wav.");
            ReloadKey = config.Bind(Advanced, "ReloadKey", KeyCode.None,
                "Re-reads cues.cfg, the sounds folder and the sets folder on the spot. They are re-read on their own when they change, so this is only for a hurry.");
            SoundboardVolume = config.Bind(Advanced, "SoundboardVolume", 1f,
                new ConfigDescription("The level a hotkey or an HTTP request plays at, before the clip's own gain.", new AcceptableValueRange<float>(0f, 2f)));
            Logging = config.Bind(Advanced, "Logging", false,
                "Write a line for every clip, every action and every path change.");
            HearWhatGoesOut = config.Bind(Advanced, "HearWhatGoesOut", false,
                "Hear your own voice chat the way the lobby gets it, mic and clips together, and log every clip sent into it. For checking your clips really go out; turn it off to play.");
        }

        // Old keys become new ones without a word: the value a player set under the old name is
        // handed to the new name before it is bound, and the old line is gone on the next save.
        private static void Migrate(ConfigFile config)
        {
            if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) is not Dictionary<ConfigDefinition, string> orphans)
            {
                return;
            }
            foreach (ConfigMoves.Move move in ConfigMoves.All)
            {
                ConfigDefinition from = new ConfigDefinition(move.FromSection, move.FromKey);
                ConfigDefinition to = new ConfigDefinition(move.ToSection, move.ToKey);
                if (orphans.TryGetValue(from, out string value))
                {
                    orphans.Remove(from);
                    if (!orphans.ContainsKey(to))
                    {
                        orphans[to] = value;
                    }
                }
            }
        }
    }
}
