using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jukebox.Services;
using Xunit;

namespace Jukebox.Tests
{
    // 2026-09-30, Justin's log: Smash & Grab's pak "loaded" as 10580 clips under smashandgrab/
    // and again as 5290 under its pack, 15870 in all, for a 170 MB pak. Those are names, not
    // audio: every clip answers to its plain name and its spaced one, and the pack's names point
    // at the arrays the first registration decoded. These pin that the pack never decodes a
    // second copy, loose files or pak.
    public class LibraryShareTests
    {
        public LibraryShareTests()
        {
            Library.Log = _ => { };
        }

        private static byte[] Wav(short level)
        {
            MemoryStream all = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(all))
            {
                short[] samples = { 0, level, (short)-level, level, 0, (short)-level };
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + samples.Length * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(48000);
                w.Write(48000 * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(samples.Length * 2);
                foreach (short s in samples)
                {
                    w.Write(s);
                }
            }
            return all.ToArray();
        }

        private static string Folder(string name)
        {
            string dir = Path.Combine(Path.GetTempPath(), "jukebox-tests-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // The house make_pak.py layout, as PakFile reads it.
        private static void Pak(string file, IDictionary<string, byte[]> entries)
        {
            List<(string path, byte[] data)> list = new List<(string, byte[])>();
            foreach (KeyValuePair<string, byte[]> e in entries)
            {
                list.Add((e.Key, e.Value));
            }
            long header = 12;
            foreach ((string path, byte[] _) in list)
            {
                header += 4 + Encoding.UTF8.GetByteCount(path) + 8 + 4;
            }
            using BinaryWriter w = new BinaryWriter(File.Create(file));
            w.Write(Encoding.ASCII.GetBytes("SGPK"));
            w.Write(1);
            w.Write(list.Count);
            long offset = header;
            foreach ((string path, byte[] data) in list)
            {
                byte[] name = Encoding.UTF8.GetBytes(path);
                w.Write(name.Length);
                w.Write(name);
                w.Write(offset);
                w.Write(data.Length);
                offset += data.Length;
            }
            foreach ((string _, byte[] data) in list)
            {
                w.Write(data);
            }
        }

        [Fact]
        public void APackOverLooseFilesDecodesNothingTwice()
        {
            string root = Folder("loose");
            Directory.CreateDirectory(Path.Combine(root, "sounds", "mario"));
            File.WriteAllBytes(Path.Combine(root, "sounds", "mario", "share_loose_jump.wav"), Wav(8000));
            File.WriteAllBytes(Path.Combine(root, "sounds", "share_loose_coin.wav"), Wav(12000));

            int held = Library.Held;
            Assert.True(Library.Register(root, "looseshare"));
            Assert.Equal(held + 2, Library.Held);
            Assert.True(Library.Register(root, "pack/Looseshare", spacedOnly: true));

            Assert.Equal(held + 2, Library.Held);
            Assert.Same(Library.Samples("looseshare/mario/share_loose_jump"), Library.Samples("pack/Looseshare/mario/share_loose_jump"));
            Assert.Same(Library.Samples("share_loose_coin"), Library.Samples("pack/Looseshare/share_loose_coin"));
            Directory.Delete(root, true);
        }

        [Fact]
        public void APackOverAPakDecodesNothingTwice()
        {
            string root = Folder("pak");
            Pak(Path.Combine(root, "Mod.pak"), new Dictionary<string, byte[]>
            {
                ["sounds/luigi/share_pak_jump.wav"] = Wav(9000),
                ["sounds/share_pak_coin.wav"] = Wav(11000),
                ["sounds/share_pak_hurt.wav"] = Wav(7000)
            });

            int held = Library.Held;
            Assert.True(Library.Register(root, "pakshare"));
            Assert.Equal(held + 3, Library.Held);
            Assert.True(Library.Register(root, "pack/Pakshare", spacedOnly: true));

            Assert.Equal(held + 3, Library.Held);
            Assert.NotNull(Library.Samples("pakshare/luigi/share_pak_jump"));
            Assert.Same(Library.Samples("pakshare/luigi/share_pak_jump"), Library.Samples("pack/Pakshare/luigi/share_pak_jump"));
            Assert.Same(Library.Samples("share_pak_hurt"), Library.Samples("pack/Pakshare/share_pak_hurt"));
            Directory.Delete(root, true);
        }
    }
}
