using System.Text;

namespace Jukebox.Models
{
    // A set folder is named after a Wardrobe look by hand, and a look name can hold characters
    // no folder can (AC/DC, What?). Both sides are brought to the same key: the characters a
    // folder name refuses on any system dropped, runs of spaces made one, trailing dots and
    // spaces trimmed (Windows drops those itself), case ignored.
    internal static class LookFolder
    {
        private const string Refused = "<>:\"/\\|?*";

        public static string Key(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            StringBuilder key = new StringBuilder(name!.Length);
            foreach (char c in name)
            {
                if (c < ' ' || Refused.IndexOf(c) >= 0)
                {
                    continue;
                }
                if (c == ' ' && (key.Length == 0 || key[key.Length - 1] == ' '))
                {
                    continue;
                }
                key.Append(char.ToLowerInvariant(c));
            }
            return key.ToString().TrimEnd(' ', '.');
        }

        public static bool Same(string folder, string look) => Key(folder) == Key(look);
    }
}
