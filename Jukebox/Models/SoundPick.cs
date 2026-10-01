using System;
using System.Collections.Generic;
using System.Linq;

namespace Jukebox.Models
{
    internal enum CueSource
    {
        Game,
        Set,
        Cues,
        Off
    }

    internal readonly struct SoundPick
    {
        public readonly CueSource Source;
        public readonly IReadOnlyList<string> Clips;

        public SoundPick(CueSource source, IReadOnlyList<string> clips)
        {
            Source = source;
            Clips = clips;
        }

        public static readonly SoundPick Game = new SoundPick(CueSource.Game, Array.Empty<string>());
    }

    // One of an action's variants, never the one that played last while there is another.
    internal static class Variant
    {
        // roll(n) is a random index below n.
        public static string Pick(IReadOnlyList<string> clips, string? previous, Func<int, int> roll)
        {
            if (clips.Count == 1)
            {
                return clips[0];
            }
            int others = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != previous)
                {
                    others++;
                }
            }
            if (others == 0)
            {
                return clips[roll(clips.Count)];
            }
            int n = roll(others);
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != previous && n-- == 0)
                {
                    return clips[i];
                }
            }
            return clips[0];
        }
    }

    // Where the sound for an action comes from, in order: the set of the look you are wearing
    // (your own sets folder, then any pack that is on and plays its sets on actions), then the
    // action's line in cues.cfg, then the game's own sound. "off" in cues.cfg silences the
    // action; a look's set still beats it, so a look with a sound for it always plays it.
    internal static class Resolver
    {
        public const string Off = "off";

        public static SoundPick Resolve(string action, string? look, SetIndex mine, IReadOnlyList<PackSet> packs, CueTable cues, Func<string, bool> has)
        {
            if (!string.IsNullOrWhiteSpace(look))
            {
                IReadOnlyList<string>? clips = mine.Clips(look!, action);
                for (int i = 0; clips == null && i < packs.Count; i++)
                {
                    clips = packs[i].Clips(look!, action);
                }
                if (clips != null && clips.Count > 0)
                {
                    return new SoundPick(CueSource.Set, clips);
                }
            }
            string? raw = cues.Raw(action);
            if (raw == null)
            {
                return SoundPick.Game;
            }
            if (string.Equals(raw.Trim(), Off, StringComparison.OrdinalIgnoreCase))
            {
                return new SoundPick(CueSource.Off, Array.Empty<string>());
            }
            List<string> found = Files(raw, has);
            return found.Count == 0 ? SoundPick.Game : new SoundPick(CueSource.Cues, found);
        }

        // The files of a cues.cfg line that are loaded: one of your own ("me/<file>") first, any
        // clip a mod registered under that name after. A level trim ("wilhelm@0.6") is looked up
        // without the trim and handed on with it.
        public static List<string> Files(string raw, Func<string, bool> has)
        {
            List<string> found = new List<string>();
            foreach (string entry in CueTable.Files(raw))
            {
                int at = entry.LastIndexOf('@');
                string file = at > 0 ? entry.Substring(0, at).Trim() : entry;
                string trim = at > 0 ? entry.Substring(at) : "";
                if (file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    file = file.Substring(0, file.Length - 4);
                }
                if (has("me/" + file))
                {
                    found.Add("me/" + file + trim);
                }
                else if (has(file))
                {
                    found.Add(file + trim);
                }
            }
            return found;
        }

        // Whether the Wardrobe wheel shows a look while Jukebox has taken it over: only looks with
        // a sound set, unless there is no set anywhere yet, when an empty wheel would help nobody.
        public static bool WheelShows(string look, SetIndex mine, IReadOnlyList<PackSet> packs)
        {
            if (mine.Count == 0 && packs.All(p => p.Empty))
            {
                return true;
            }
            return HasSet(look, mine, packs);
        }

        // Whether the player's own sets folder has a file for this look and action. A pack never
        // counts: this is what a mod that voices its own moments asks before it stays quiet.
        public static bool HasUserSound(string look, string action, SetIndex mine) => UserClips(look, action, mine).Count > 0;

        // The player's own files for this look and action, none when there are none.
        public static IReadOnlyList<string> UserClips(string? look, string? action, SetIndex mine)
        {
            if (string.IsNullOrWhiteSpace(look) || string.IsNullOrWhiteSpace(action))
            {
                return Array.Empty<string>();
            }
            return mine.Clips(look!, action!) ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        // Whether a look has a sound set anywhere: your sets folder or a registered pack.
        public static bool HasSet(string look, SetIndex mine, IReadOnlyList<PackSet> packs)
        {
            if (mine.Has(look))
            {
                return true;
            }
            for (int i = 0; i < packs.Count; i++)
            {
                if (packs[i].Has(look))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
