using System;
using System.Collections.Generic;

namespace Jukebox.Models
{
    // The sound sets under one root: a folder per look, files named after actions inside it.
    // jump.wav, jump2.wav and jump21.wav are all "jump" and take turns at random. A pack whose
    // files are named its own way (win1, attack3) hands over an alias table from its names to
    // the action names, and a table from its folder names to the look names they belong to.
    //
    // Built from clip names as the library files them ("<folder>/<file>", no extension), so a
    // pack read out of a .pak and one read off the disk index the same way.
    internal sealed class SetIndex
    {
        public static readonly SetIndex Empty = new SetIndex();

        private readonly Dictionary<string, Dictionary<string, List<string>>> looks =
            new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        // The action a file stands for: its name, or its alias, else its name with any variant
        // number (and the _ or - before it) taken off, or that name's alias.
        public static string ActionOf(string stem, IDictionary<string, string>? aliases = null)
        {
            string name = stem.Trim().ToLowerInvariant();
            if (Alias(aliases, name, out string whole))
            {
                return whole;
            }
            string folded = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd('_', '-', ' ');
            if (folded.Length == 0)
            {
                return name;
            }
            return Alias(aliases, folded, out string alias) ? alias : folded;
        }

        private static bool Alias(IDictionary<string, string>? aliases, string name, out string action)
        {
            action = "";
            if (aliases == null)
            {
                return false;
            }
            foreach (KeyValuePair<string, string> pair in aliases)
            {
                if (string.Equals(pair.Key.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    action = pair.Value.Trim().ToLowerInvariant();
                    return action.Length > 0;
                }
            }
            return false;
        }

        // names: clip names relative to the root, "<folder>/<file>" (deeper folders count toward
        // the first one). prefix: what the library files them under, put back on each clip.
        // known: when given, a file whose action it does not know is left out, so a folder of
        // nothing but such files is no set.
        public static SetIndex Build(IEnumerable<string> names, string prefix,
            IDictionary<string, string>? aliases = null, IDictionary<string, string>? folderLooks = null, Func<string, bool>? known = null)
        {
            Dictionary<string, string>? lookOf = null;
            if (folderLooks != null)
            {
                lookOf = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in folderLooks)
                {
                    lookOf[LookFolder.Key(pair.Key)] = pair.Value;
                }
            }
            SetIndex index = new SetIndex();
            foreach (string name in names)
            {
                int slash = name.IndexOf('/');
                int last = name.LastIndexOf('/');
                if (slash <= 0 || last + 1 >= name.Length)
                {
                    continue;
                }
                string folder = LookFolder.Key(name.Substring(0, slash));
                string look = lookOf != null && lookOf.TryGetValue(folder, out string mapped) ? LookFolder.Key(mapped) : folder;
                if (look.Length == 0)
                {
                    continue;
                }
                string action = ActionOf(name.Substring(last + 1), aliases);
                if (known != null && !known(action))
                {
                    continue;
                }
                if (!index.looks.TryGetValue(look, out Dictionary<string, List<string>> actions))
                {
                    index.looks[look] = actions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }
                if (!actions.TryGetValue(action, out List<string> clips))
                {
                    actions[action] = clips = new List<string>();
                }
                clips.Add(prefix + name);
            }
            return index;
        }

        public IReadOnlyList<string>? Clips(string look, string action) =>
            looks.TryGetValue(LookFolder.Key(look), out Dictionary<string, List<string>> actions)
            && actions.TryGetValue(action, out List<string> clips) ? clips : null;

        // Whether the look has a set here with at least one clip in it.
        public bool Has(string look) => looks.ContainsKey(LookFolder.Key(look));

        public int Count => looks.Count;
    }
}
