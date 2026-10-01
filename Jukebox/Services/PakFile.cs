using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Jukebox.Services
{
    // One packed archive: a short index and the bytes, nothing clever. Written by the house
    // make_pak.py, which every mod here already uses, so a mod that ships one pak file instead
    // of a thousand loose WAVs hands it over unchanged.
    //
    // Layout: "SGPK", int32 version (1), int32 count, then per file int32 path length, utf-8
    // path, int64 offset, int32 length, then the bytes in the same order. Paths are relative
    // with forward slashes, "sounds/mario/hello.wav".
    internal sealed class PakFile
    {
        private readonly Dictionary<string, (long offset, int length)> index = new Dictionary<string, (long, int)>(StringComparer.OrdinalIgnoreCase);
        private readonly string path;

        private PakFile(string path)
        {
            this.path = path;
        }

        public string Path => path;
        public int Count => index.Count;

        // The archive read, or null when the file is missing or is not one of ours.
        public static PakFile? Open(string file)
        {
            if (!File.Exists(file))
            {
                return null;
            }
            PakFile pak = new PakFile(file);
            try
            {
                using FileStream fs = File.OpenRead(file);
                using BinaryReader br = new BinaryReader(fs, Encoding.UTF8);
                byte[] magic = br.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != (byte)'S' || magic[1] != (byte)'G' || magic[2] != (byte)'P' || magic[3] != (byte)'K')
                {
                    Library.Log("pak: " + file + " is not a pak file.");
                    return null;
                }
                int version = br.ReadInt32();
                int count = br.ReadInt32();
                if (version != 1 || count < 0)
                {
                    Library.Log("pak: unknown pak version " + version + " in " + file + ".");
                    return null;
                }
                for (int i = 0; i < count; i++)
                {
                    int len = br.ReadInt32();
                    string entry = Encoding.UTF8.GetString(br.ReadBytes(len));
                    long offset = br.ReadInt64();
                    int size = br.ReadInt32();
                    pak.index[entry.Replace('\\', '/').TrimStart('/')] = (offset, size);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
            {
                Library.Log("pak: could not read " + file + ": " + e.Message);
                return null;
            }
            return pak;
        }

        // Every entry under a folder with an extension, in a settled order.
        public List<string> List(string folder, string extension)
        {
            List<string> found = new List<string>();
            string prefix = folder.TrimEnd('/') + "/";
            foreach (string entry in index.Keys)
            {
                if (entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && entry.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(entry);
                }
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        public byte[]? ReadBytes(string entry)
        {
            if (!index.TryGetValue(entry.Replace('\\', '/').TrimStart('/'), out var at))
            {
                return null;
            }
            try
            {
                using FileStream fs = File.OpenRead(path);
                fs.Seek(at.offset, SeekOrigin.Begin);
                byte[] data = new byte[at.length];
                int read = 0;
                while (read < data.Length)
                {
                    int n = fs.Read(data, read, data.Length - read);
                    if (n <= 0)
                    {
                        break;
                    }
                    read += n;
                }
                return read == data.Length ? data : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
            {
                Library.Log("pak: could not read " + entry + " from " + path + ": " + e.Message);
                return null;
            }
        }
    }
}
