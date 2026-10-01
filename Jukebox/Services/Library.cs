using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Jukebox.Models;

namespace Jukebox.Services
{
    // Every clip the jukebox can reach, by name. A mod registers a folder or a .pak under a
    // namespace of its own; whatever rate or channel count a file came in at, it is folded to
    // 48 kHz mono and levelled on the way in, so a file that came in screaming and one that
    // came in a whisper sit at the same place under a voice.
    //
    // A namespace is a prefix, not a rename: register "smashandgrab" and every clip answers to
    // its own name and to "smashandgrab/<name>", so two mods that both ship a "jump" each keep
    // one that is unmistakably theirs. The first mod to claim a plain name keeps it.
    //
    // Your own clips, the ones in the save folder, are always loaded and answer to "me/<name>".
    // They take a plain name only when no mod wants it, so dropping a coin.wav in there never
    // quietly replaces a mod's coin. Point a cue at it in cues.cfg and it does.
    internal static class Library
    {
        public const int Rate = 48000;
        // Every clip is brought to Levels.TargetRms on load, peaks held under Levels.PeakCeiling.
        private const float TargetRms = Levels.TargetRms;
        private const float PeakCeiling = Levels.PeakCeiling;

        private sealed class Source
        {
            public string Path = "";
            public string Space = "";
            public bool Weak;
            // Filed under "<space>/<name>" only, never the plain name: the sound sets, which are
            // looked up by their full name and have no business shadowing anything.
            public bool SpacedOnly;
            // The folder is the root as given, no sounds subfolder looked for and no .pak read:
            // the sets folder, where a look could be called anything, "sounds" included.
            public bool Exact;
        }

        private static readonly List<Source> sources = new List<Source>();
        private static readonly Dictionary<string, float[]> clips = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        // Which namespace filed each name, so a shared sound can ask that mod who covers it.
        private static readonly Dictionary<string, string> owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> weak = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Every file decoded once, by where it came from, so a pack registered over folders a
        // mod has already loaded costs no second copy of the audio.
        private static readonly Dictionary<string, float[]> decoded = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private static string[] listing = Array.Empty<string>();
        private static string? soundboard;
        private static int sharedPuts;

        // Where the library's lines go. A lambda, not the method itself, so the tests can load
        // this class without BepInEx behind it.
        internal static Action<string> Log = message => Jukebox.LogAlways(message);

        // Clips decoded and held in memory, however many names point at them.
        public static int Held => decoded.Count;

        // The game's own save folder, not the mod profile: Gale and r2modman export the profile
        // whole, and this is the one place your own clips are not carried off with it.
        public static string SoundboardFolder
        {
            get
            {
                if (soundboard == null)
                {
                    soundboard = Path.Combine(Path.Combine(Application.persistentDataPath, "Jukebox"), "sounds");
                    Directory.CreateDirectory(soundboard);
                }
                return soundboard;
            }
        }

        public static void LoadSoundboard()
        {
            Register(SoundboardFolder, MineSpace, true);
        }

        // A folder or a .pak, under a namespace. A folder with a sounds subfolder is read from
        // there, and any .pak sitting next to it is read too, which is how a mod hands over its
        // whole plugin folder in one call whether it shipped loose files or packed ones.
        public static bool Register(string path, string space, bool weakNames = false, bool spacedOnly = false, bool exact = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            space = (space ?? "").Trim().Trim('/');
            foreach (Source known in sources)
            {
                if (string.Equals(known.Path, path, StringComparison.OrdinalIgnoreCase) && string.Equals(known.Space, space, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Log("library: nothing at " + path + ".");
                return false;
            }
            sources.Add(new Source { Path = path, Space = space, Weak = weakNames, SpacedOnly = spacedOnly, Exact = exact });
            int names = clips.Count;
            int held = decoded.Count;
            sharedPuts = 0;
            Read(sources[sources.Count - 1]);
            Relist();
            // A clip answers to its plain name and its spaced one, so names run ahead of clips;
            // a pack over a folder already loaded adds names and no audio.
            Log("library: " + (decoded.Count - held) + " clips from " + path
                + (space.Length > 0 ? " as " + space + "/" : "") + ", " + (clips.Count - names) + " names"
                + (sharedPuts > 0 ? " (" + sharedPuts + " on clips already loaded)" : "")
                + ", " + decoded.Count + " clips held.");
            return true;
        }

        // Everything registered, read again. The files on disk may have changed underneath, so
        // the store is emptied first rather than topped up.
        public static void Reload()
        {
            clips.Clear();
            weak.Clear();
            decoded.Clear();
            foreach (Source source in sources)
            {
                Read(source);
            }
            Relist();
            Log("library: re-read, " + decoded.Count + " clips under " + clips.Count + " names.");
        }

        // One namespace read again on its own, for a folder that changed on disk (the sound
        // sets), without touching the hundreds of clips every other mod registered.
        public static void Reread(string space)
        {
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, string> pair in owner)
            {
                if (string.Equals(pair.Value, space, StringComparison.OrdinalIgnoreCase))
                {
                    gone.Add(pair.Key);
                }
            }
            foreach (string name in gone)
            {
                clips.Remove(name);
                owner.Remove(name);
                weak.Remove(name);
            }
            foreach (Source source in sources)
            {
                if (string.Equals(source.Space, space, StringComparison.OrdinalIgnoreCase))
                {
                    List<string> stale = new List<string>();
                    foreach (string where in decoded.Keys)
                    {
                        if (where.StartsWith(source.Path, StringComparison.Ordinal))
                        {
                            stale.Add(where);
                        }
                    }
                    foreach (string where in stale)
                    {
                        decoded.Remove(where);
                    }
                    Read(source);
                }
            }
            Relist();
        }

        private static void Read(Source source)
        {
            if (Directory.Exists(source.Path))
            {
                string root = Path.Combine(source.Path, "sounds");
                if (source.Exact || !Directory.Exists(root))
                {
                    root = source.Path;
                }
                foreach (string file in Directory.GetFiles(root, "*.wav", SearchOption.AllDirectories))
                {
                    string name = Rel(root, file);
                    Put(source, name, () => File.ReadAllBytes(file), file);
                }
                if (source.Exact)
                {
                    return;
                }
                foreach (string pakPath in Directory.GetFiles(source.Path, "*.pak", SearchOption.TopDirectoryOnly))
                {
                    ReadPak(source, pakPath);
                }
                return;
            }
            ReadPak(source, source.Path);
        }

        private static void ReadPak(Source source, string pakPath)
        {
            PakFile? pak = PakFile.Open(pakPath);
            if (pak == null)
            {
                return;
            }
            foreach (string entry in pak.List("sounds", ".wav"))
            {
                string name = entry.Substring("sounds/".Length);
                int dot = name.LastIndexOf('.');
                if (dot > 0)
                {
                    name = name.Substring(0, dot);
                }
                string capture = entry;
                Put(source, name, () => pak.ReadBytes(capture), pakPath + ":" + entry);
            }
        }

        private static string Rel(string root, string file)
        {
            string name = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        private static void Put(Source source, string name, Func<byte[]?> read, string where)
        {
            string spaced = source.Space.Length > 0 ? source.Space + "/" + name : "";
            bool wantPlain = !source.SpacedOnly && (!clips.ContainsKey(name) || (weak.Contains(name) && !source.Weak));
            bool wantSpaced = spaced.Length > 0 && !clips.ContainsKey(spaced);
            if (!wantPlain && !wantSpaced)
            {
                return;
            }
            if (decoded.TryGetValue(where, out float[] data))
            {
                sharedPuts++;
            }
            else
            {
                byte[]? bytes = read();
                if (bytes == null)
                {
                    Log("library: skipped " + where + ", could not read it.");
                    return;
                }
                float[]? fresh = Wav.Decode(bytes, Rate, out string problem);
                if (fresh == null)
                {
                    Log("library: skipped " + where + ", " + problem);
                    return;
                }
                Normalize(fresh);
                decoded[where] = data = fresh;
            }
            if (wantPlain)
            {
                clips[name] = data;
                owner[name] = source.Space;
                if (source.Weak)
                {
                    weak.Add(name);
                }
                else
                {
                    weak.Remove(name);
                }
            }
            if (wantSpaced)
            {
                clips[spaced] = data;
                owner[spaced] = source.Space;
                if (source.Weak)
                {
                    weak.Add(spaced);
                }
            }
        }

        private static void Relist()
        {
            string[] names = new string[clips.Count];
            clips.Keys.CopyTo(names, 0);
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            listing = names;
        }

        // The average is taken over the sounding part only, so leading and trailing silence in
        // a short clip does not push its gain up.
        private static void Normalize(float[] data)
        {
            double sum = 0;
            int counted = 0;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i];
                float a = v < 0f ? -v : v;
                if (a > peak)
                {
                    peak = a;
                }
                if (a > 0.01f)
                {
                    sum += v * v;
                    counted++;
                }
            }
            if (data.Length == 0 || peak <= 0f || counted == 0)
            {
                return;
            }
            float rms = (float)Math.Sqrt(sum / counted);
            float gain = rms > 0f ? TargetRms / rms : 1f;
            if (peak * gain > PeakCeiling)
            {
                gain = PeakCeiling / peak;
            }
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= gain;
            }
        }

        // The names filed under one namespace, with the "<space>/" taken off: what a sound set
        // index is built from.
        public static List<string> NamesIn(string space)
        {
            string prefix = space.Trim().Trim('/') + "/";
            List<string> found = new List<string>();
            foreach (string name in listing)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(SpaceOf(name), space, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(name.Substring(prefix.Length));
                }
            }
            return found;
        }

        // A clip only this machine has: your own sounds folder and your sound sets. A friend's
        // Jukebox has nothing filed under those names, so these go through the voice even in a
        // room where everyone has the mod.
        public static bool LocalOnly(string name)
        {
            string space = SpaceOf(name);
            return string.Equals(space, MineSpace, StringComparison.OrdinalIgnoreCase)
                || string.Equals(space, SetsSpace, StringComparison.OrdinalIgnoreCase);
        }

        public const string MineSpace = "me";
        public const string SetsSpace = "sets";

        public static bool Has(string name) => !string.IsNullOrWhiteSpace(name) && clips.ContainsKey(name.Trim());

        // The namespace a clip was filed under, or "" for a clip nobody claimed.
        public static string SpaceOf(string name) => !string.IsNullOrWhiteSpace(name) && owner.TryGetValue(name.Trim(), out string space) ? space : "";

        public static float[]? Samples(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && clips.TryGetValue(name.Trim(), out float[] data) ? data : null;
        }

        // Read from the endpoint's own thread as well as the game's, so it hands back the
        // settled array rather than walking the store while a load is under way.
        public static string[] Names() => listing;
    }
}
