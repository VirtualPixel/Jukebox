using System;
using System.Collections.Generic;
using System.IO;
using Jukebox.Models;
using UnityEngine;

namespace Jukebox.Services
{
    // The per-look sound sets. Your own live next to cues.cfg:
    //
    //   Jukebox/sets/<look name>/<action>.wav      jump.wav, jump2.wav, hurt.wav...
    //
    // and a mod can hand over its own tree in the same shape (a pack). The look you have on in
    // Wardrobe picks the set; an action it has no file for falls back to cues.cfg, then to the
    // game's own sound. The folder is watched: add, rename or delete a file and it is picked up
    // a couple of seconds later.
    internal static class SoundSets
    {
        private const float CheckSeconds = 2f;

        private sealed class Pack
        {
            public string Guid = "";
            public string Space = "";
            public IDictionary<string, string>? Aliases;
            public IDictionary<string, string>? FolderLooks;
            public bool PlaysActions = true;
        }

        private static readonly List<Pack> packs = new List<Pack>();
        private static SetIndex mine = SetIndex.Empty;
        private static readonly HashSet<string> strangers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static PackSet[] packSets = Array.Empty<PackSet>();
        private static readonly PackSwitches switches = new PackSwitches();
        private static readonly Dictionary<string, string[]> declared = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private static string? folder;
        private static string stamp = "";
        private static float nextCheck;

        public static string Folder
        {
            get
            {
                if (folder == null)
                {
                    folder = Path.Combine(Path.GetDirectoryName(Library.SoundboardFolder) ?? ".", "sets");
                    Directory.CreateDirectory(folder);
                }
                return folder;
            }
        }

        public static void Load()
        {
            stamp = Stamp();
            Library.Register(Folder, Library.SetsSpace, weakNames: true, spacedOnly: true, exact: true);
            Rebuild();
        }

        // A mod's own tree of sets. root is a folder or a .pak in the same shape Api.Load takes;
        // aliases rename the mod's own file names to action names (win -> level_won), folderLooks
        // names the look a folder belongs to when the two are spelled differently.
        // playsActions false: the pack's mod voices those moments itself, so its sets only say which
        // looks have a voice (the wheel) and never play on an action.
        public static bool RegisterPack(string guid, string root, IDictionary<string, string>? aliases, IDictionary<string, string>? folderLooks,
            bool playsActions = true)
        {
            string space = "pack/" + guid.Trim();
            if (!Library.Register(root, space, spacedOnly: true))
            {
                return false;
            }
            packs.RemoveAll(p => string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase));
            packs.Add(new Pack
            {
                Guid = guid,
                Space = space,
                Aliases = aliases == null ? null : new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase),
                FolderLooks = folderLooks == null ? null : new Dictionary<string, string>(folderLooks, StringComparer.OrdinalIgnoreCase),
                PlaysActions = playsActions
            });
            Rebuild();
            Jukebox.LogAlways("sets: " + guid + " brought sets for " + packSets[packSets.Length - 1].Index.Count + " looks"
                + (playsActions ? "." : ", voiced by the mod itself."));
            return true;
        }

        // A switched off pack is no set for the wheel and plays nothing until it is switched on.
        public static void SetPackEnabled(string guid, bool enabled)
        {
            if (switches.Enabled(guid) == enabled)
            {
                return;
            }
            switches.Set(guid, enabled);
            foreach (PackSet pack in packSets)
            {
                pack.Enabled = switches.Enabled(pack.Guid);
            }
            Jukebox.LogAlways("sets: " + guid + "'s pack is " + (enabled ? "on." : "off."));
        }

        // The looks a pack's mod says are its own, set or no set. Held by GUID like the switch, so
        // it holds whichever call comes first.
        public static void DeclareLooks(string guid, IEnumerable<string>? looks)
        {
            guid = (guid ?? "").Trim();
            declared[guid] = looks == null ? Array.Empty<string>() : new List<string>(looks).ToArray();
            Rebuild();
            Jukebox.LogAlways("sets: " + guid + " declared " + declared[guid].Length + " looks.");
        }

        public static void Rebuild()
        {
            List<string> names = Library.NamesIn(Library.SetsSpace);
            mine = SetIndex.Build(names, Library.SetsSpace + "/", null, null, ActionList.Known);
            foreach (string name in names)
            {
                int slash = name.LastIndexOf('/');
                string action = SetIndex.ActionOf(name.Substring(slash + 1));
                if (slash > 0 && !ActionList.Known(action) && strangers.Add(name))
                {
                    Jukebox.LogAlways("sets: " + name + ".wav is not an action name, so it never plays. The names are in ACTIONS.txt.");
                }
            }
            PackSet[] built = new PackSet[packs.Count];
            for (int i = 0; i < packs.Count; i++)
            {
                Pack pack = packs[i];
                string guid = pack.Guid;
                SetIndex index = SetIndex.Build(Library.NamesIn(pack.Space), pack.Space + "/", pack.Aliases, pack.FolderLooks, ActionList.Known);
                declared.TryGetValue(guid.Trim(), out string[] looks);
                built[i] = new PackSet(guid, index, pack.PlaysActions, look => Wardrobe.Api.PackLookOf(guid, look), looks)
                {
                    Enabled = switches.Enabled(guid)
                };
            }
            packSets = built;
        }

        public static SoundPick Resolve(string action, string? look) =>
            Resolver.Resolve(action, look, mine, packSets, Soundboard.Table, Library.Has);

        public static bool HasSet(string look) => Resolver.HasSet(look, mine, packSets);

        public static bool HasUserSound(string look, string action) => Resolver.HasUserSound(look, action, mine);

        public static IReadOnlyList<string> UserClips(string? look, string? action) => Resolver.UserClips(look, action, mine);

        public static bool WheelShows(string look) => Resolver.WheelShows(look, mine, packSets);

        public static void Tick()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + CheckSeconds;
            string now = Stamp();
            if (now == stamp)
            {
                return;
            }
            stamp = now;
            Library.Reread(Library.SetsSpace);
            Rebuild();
            Jukebox.LogAlways("sets: re-read, " + mine.Count + " looks with a set in " + Folder + ".");
        }

        // Every file's name, size and time in one string: any change on disk changes it.
        private static string Stamp()
        {
            try
            {
                string[] files = Directory.GetFiles(Folder, "*.wav", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                foreach (string file in files)
                {
                    FileInfo info = new FileInfo(file);
                    text.Append(file).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
                }
                return text.ToString();
            }
            catch (IOException)
            {
                return stamp;
            }
        }
    }
}
