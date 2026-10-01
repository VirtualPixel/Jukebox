using System;
using System.Collections.Generic;

namespace Jukebox.Models
{
    // cues.cfg, read. A line is  cue = file  or  cue = file1, file2  and anything after a '#'
    // at the start of a line is a comment. Nothing here touches the disk or the game, so the
    // rules can be pinned down by tests.
    //
    // A name a mod asked for is matched three ways: the whole name ("smb_coin"), the last part
    // of a path ("mario/jump" matches "jump") and the last word of an underscored name
    // ("smb_coin" matches "coin").
    internal sealed class CueTable
    {
        private readonly Dictionary<string, string> lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int Count => lines.Count;

        public static CueTable Parse(IEnumerable<string> text)
        {
            CueTable table = new CueTable();
            foreach (string line in text)
            {
                string row = line.Trim();
                if (row.Length == 0 || row[0] == '#')
                {
                    continue;
                }
                int split = row.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }
                string cue = row.Substring(0, split).Trim();
                string files = row.Substring(split + 1).Trim();
                if (cue.Length > 0 && files.Length > 0)
                {
                    table.lines[cue] = files;
                }
            }
            return table;
        }

        // The right-hand side of one cue, exactly as written, or null.
        public string? Raw(string cue) => lines.TryGetValue(cue, out string files) ? files : null;

        // The cue a name falls under, by the three matches in order, or null.
        public string? Match(string name)
        {
            if (lines.Count == 0 || name.Length == 0)
            {
                return null;
            }
            if (lines.ContainsKey(name))
            {
                return name;
            }
            int slash = name.LastIndexOf('/');
            string tail = slash >= 0 ? name.Substring(slash + 1) : name;
            if (tail != name && lines.ContainsKey(tail))
            {
                return tail;
            }
            int under = tail.LastIndexOf('_');
            if (under > 0 && under + 1 < tail.Length)
            {
                string word = tail.Substring(under + 1);
                return lines.ContainsKey(word) ? word : null;
            }
            return null;
        }

        // The files of a line, split on commas, trimmed, ".wav" taken off, empties dropped.
        public static List<string> Files(string raw)
        {
            List<string> found = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string file = part.Trim();
                if (file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    file = file.Substring(0, file.Length - 4);
                }
                if (file.Length > 0)
                {
                    found.Add(file);
                }
            }
            return found;
        }
    }
}
