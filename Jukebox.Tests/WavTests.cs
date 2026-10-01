using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jukebox.Services;
using Xunit;

namespace Jukebox.Tests
{
    // A broken file dropped in the sounds folder is skipped with a reason. It must never throw
    // out of the load or hang the game at startup.
    public class WavTests
    {
        private static byte[] Riff(Action<BinaryWriter> chunks)
        {
            MemoryStream body = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(body, Encoding.ASCII, true))
            {
                chunks(w);
            }
            MemoryStream all = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(all))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write((int)body.Length + 4);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(body.ToArray());
            }
            return all.ToArray();
        }

        private static void Fmt(BinaryWriter w, short format, short channels, int rate, short bits, short blockAlign)
        {
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write(format);
            w.Write(channels);
            w.Write(rate);
            w.Write(rate * blockAlign);
            w.Write(blockAlign);
            w.Write(bits);
        }

        [Fact]
        public void APlainSixteenBitFileDecodes()
        {
            byte[] wav = Riff(w =>
            {
                Fmt(w, 1, 1, 48000, 16, 2);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(4);
                w.Write((short)16384);
                w.Write((short)-16384);
            });

            float[]? data = Wav.Decode(wav, 48000, out _);

            Assert.Equal(new[] { 0.5f, -0.5f }, data);
        }

        [Fact]
        public async Task ANegativeChunkSizeIsSkippedNotLoopedOn()
        {
            byte[] wav = Riff(w =>
            {
                w.Write(Encoding.ASCII.GetBytes("junk"));
                w.Write(-8);
                w.Write(new byte[16]);
            });

            Task<float[]?> decode = Task.Run(() => Wav.Decode(wav, 48000, out _));

            Assert.Same(decode, await Task.WhenAny(decode, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Null(await decode);
        }

        [Fact]
        public void AFmtChunkCutShortIsSkipped()
        {
            byte[] wav = Riff(w =>
            {
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
            });

            Assert.Null(Wav.Decode(wav, 48000, out string problem));
            Assert.NotEqual("", problem);
        }

        [Fact]
        public void AnAdpcmBlockTooSmallForItsChannelsIsSkipped()
        {
            byte[] wav = Riff(w =>
            {
                Fmt(w, 0x11, 4, 48000, 4, 8);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(8);
                w.Write(new byte[8]);
            });

            Assert.Null(Wav.Decode(wav, 48000, out _));
        }
    }
}
