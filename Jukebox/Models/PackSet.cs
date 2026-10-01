using System;
using System.Collections.Generic;

namespace Jukebox.Models
{
    // One mod's sound pack as the resolver sees it: its sets, whether they play on Jukebox's
    // actions or only say which looks have a voice, the looks the mod says are its own whatever
    // files they have, and the pack's own name for a look the player sees under another one
    // (Wardrobe's "Mario (Super Mario Bros.)").
    internal sealed class PackSet
    {
        public readonly string Guid;
        public readonly SetIndex Index;
        public readonly bool PlaysActions;
        public bool Enabled = true;
        private readonly Func<string, string?>? lookName;
        private readonly HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);

        public PackSet(string guid, SetIndex index, bool playsActions = true, Func<string, string?>? lookName = null,
            IEnumerable<string>? looks = null)
        {
            Guid = guid;
            Index = index;
            PlaysActions = playsActions;
            this.lookName = lookName;
            if (looks != null)
            {
                foreach (string look in looks)
                {
                    string key = LookFolder.Key(look);
                    if (key.Length > 0)
                    {
                        declared.Add(key);
                    }
                }
            }
        }

        // The look's name as the pack's folders know it.
        public string Key(string look) => lookName?.Invoke(look) ?? look;

        // The clips for an action on this look, when this pack plays actions and is on.
        public IReadOnlyList<string>? Clips(string look, string action) =>
            Enabled && PlaysActions ? Index.Clips(Key(look), action) : null;

        // Whether this pack gives the look a voice, whoever plays it. A look the mod declared
        // counts with no file at all: a character who never says a word is still theirs.
        public bool Has(string look)
        {
            if (!Enabled)
            {
                return false;
            }
            string key = Key(look);
            return Index.Has(key) || declared.Contains(LookFolder.Key(key));
        }

        public bool Empty => !Enabled || (Index.Count == 0 && declared.Count == 0);
    }

    // Packs a mod has switched off, kept by GUID so a switch that comes before the pack is
    // registered (or before it is registered again) still holds.
    internal sealed class PackSwitches
    {
        private readonly HashSet<string> off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Set(string guid, bool enabled)
        {
            guid = (guid ?? "").Trim();
            if (enabled)
            {
                off.Remove(guid);
            }
            else
            {
                off.Add(guid);
            }
        }

        public bool Enabled(string guid) => !off.Contains((guid ?? "").Trim());
    }
}
