using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Jukebox.Configuration;
using Jukebox.Models;

namespace Jukebox.Services
{
    // Your side of the jukebox. Drop WAVs in the sounds folder and point cues at them in
    // cues.cfg: a line "coin = mycoin" means every coin any mod asks for plays your file
    // instead, in the room, through your Semibot, with nothing installed on the other side.
    //
    // A cue name is matched three ways against the clip a mod asked for: the whole name
    // ("smb_coin"), the last part of a path ("mario/jump" matches "jump"), and the last word of
    // an underscored name ("smb_coin" matches "coin"). So the short names are enough for the
    // usual moments and the full name is there when you want one file and not its family.
    //
    // The file is re-read a couple of seconds after you save it, so you can sit in the truck
    // with a text editor open and hear each change on the next jump.
    internal static class Soundboard
    {
        private const float CheckSeconds = 2f;

        private static CueTable table = new CueTable();
        private static readonly Dictionary<string, string?> resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<KeyValuePair<KeyCode, string>> hotkeys = new List<KeyValuePair<KeyCode, string>>();
        private static readonly HashSet<string> emptyWarned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static DateTime stamp;
        private static float nextCheck;
        private static string? path;

        public static string CuesFile
        {
            get
            {
                if (path == null)
                {
                    path = Path.Combine(Path.GetDirectoryName(Library.SoundboardFolder) ?? ".", "cues.cfg");
                }
                return path;
            }
        }

        public static void Load()
        {
            table = new CueTable();
            resolved.Clear();
            emptyWarned.Clear();
            if (!File.Exists(CuesFile))
            {
                Write();
            }
            try
            {
                stamp = File.GetLastWriteTimeUtc(CuesFile);
                table = CueTable.Parse(File.ReadAllLines(CuesFile));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Jukebox.LogAlways("cues: could not read " + CuesFile + ": " + e.Message);
            }
            ReadHotkeys();
            Jukebox.LogAlways("cues: " + table.Count + " in " + CuesFile + ", " + hotkeys.Count + " hotkeys.");
        }

        private static void ReadHotkeys()
        {
            hotkeys.Clear();
            foreach (string pair in PluginConfig.Hotkeys.Value.Split(','))
            {
                string row = pair.Trim();
                int split = row.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }
                string key = row.Substring(0, split).Trim();
                string file = row.Substring(split + 1).Trim();
                if (file.Length == 0 || !Enum.TryParse(key, true, out KeyCode code) || code == KeyCode.None)
                {
                    if (row.Length > 0)
                    {
                        Jukebox.LogAlways("cues: " + row + " is not a key name the game knows.");
                    }
                    continue;
                }
                hotkeys.Add(new KeyValuePair<KeyCode, string>(code, file));
            }
        }

        // The file a cue has been pointed at, "" when it is set to off, or null when the mod's own
        // clip should play.
        public static string? Redirect(string name)
        {
            string? cue = table.Match(name);
            return cue == null ? null : Look(cue);
        }

        // The line for one action, exactly as the action is named: no path or last-word
        // matching, so "land = thud" never reaches land_hard.
        public static CueTable Table => table;

        private static string? Look(string cue)
        {
            if (resolved.TryGetValue(cue, out string? known))
            {
                return known;
            }
            string? raw = table.Raw(cue);
            if (raw == null)
            {
                return null;
            }
            string? answer;
            if (string.Equals(raw.Trim(), Resolver.Off, StringComparison.OrdinalIgnoreCase))
            {
                answer = "";
            }
            else
            {
                List<string> found = Resolver.Files(raw, Library.Has);
                answer = found.Count == 0 ? null : string.Join(",", found.ToArray());
            }
            if (answer == null && emptyWarned.Add(cue))
            {
                Jukebox.LogAlways("cues: " + cue + " points at " + raw + ", which is not in " + Library.SoundboardFolder + ".");
            }
            resolved[cue] = answer;
            return answer;
        }

        public static void Tick()
        {
            // A hotkey on a letter would go off on every chat message otherwise.
            if (SemiFunc.NoTextInputsActive())
            {
                ReadKeys();
            }
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + CheckSeconds;
            try
            {
                if (File.Exists(CuesFile) && File.GetLastWriteTimeUtc(CuesFile) != stamp)
                {
                    Load();
                }
            }
            catch (IOException)
            {
                // Saved on top of us this instant; the next check picks it up.
            }
        }

        private static void ReadKeys()
        {
            if (PluginConfig.ReloadKey.Value != KeyCode.None && Input.GetKeyDown(PluginConfig.ReloadKey.Value))
            {
                Library.Reload();
                Load();
                SoundSets.Rebuild();
            }
            for (int i = 0; i < hotkeys.Count; i++)
            {
                if (Input.GetKeyDown(hotkeys[i].Key))
                {
                    Api.Play(hotkeys[i].Value, PluginConfig.SoundboardVolume.Value);
                }
            }
        }

        // The file as it ships: every action, then every cue Smash & Grab asks for, commented
        // out, so the list is in front of you and a line is one '#' away from working.
        private static void Write()
        {
            StringBuilder text = new StringBuilder();
            text.Append("# Jukebox cues. A line is  action = file  and the file is one of your own,\n");
            text.Append("# under " + Library.SoundboardFolder + ", with or without .wav.\n");
            text.Append("# Several files, one of them each time:  hurt = ow1, ow2, ow3\n");
            text.Append("# No sound at all, the game's own included:  land = off\n");
            text.Append("# Trim one that comes in hot:  jump = boing@0.6\n");
            text.Append("# A look's own set (sets/<look>/<action>.wav) wins over a line here.\n");
            text.Append("# What each action means is in ACTIONS.txt next to this file.\n");
            text.Append("# Saved changes are picked up in a couple of seconds.\n");
            text.Append("\n# Actions:\n");
            foreach (ActionList.Entry entry in ActionList.All)
            {
                text.Append("# ").Append(entry.Name).Append(" =\n");
            }
            text.Append("\n# A line also works for a clip a mod plays: it matches the whole clip name,\n");
            text.Append("# the last part of a path (mario/jump matches jump) or the last word of an\n");
            text.Append("# underscored name (smb_coin matches coin). The cues Smash & Grab asks for:\n");
            foreach (string cue in SmashAndGrabCues)
            {
                text.Append("# ").Append(cue).Append(" =\n");
            }
            try
            {
                File.WriteAllText(CuesFile, text.ToString());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Jukebox.LogAlways("cues: could not write " + CuesFile + ": " + e.Message);
            }
        }

        private static readonly string[] SmashAndGrabCues =
        {
            "hello", "start", "idle", "sad", "hurt", "death", "deathjingle", "revive",
            "jump", "jump2", "jump3", "land", "spin", "headspin", "attack", "win",
            "coin", "heal", "oneup", "powerup", "powerdown", "star", "starloop",
            "portal", "chest", "secret", "fireball", "climb", "block", "bump",
            "lowhealth", "hurry", "stageclear", "worldclear", "gameover", "fireworks",
            "select", "roll", "rollloop", "rollend", "rollimpact"
        };
    }
}
