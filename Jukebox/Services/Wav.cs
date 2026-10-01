using System;

namespace Jukebox.Services
{
    // Minimal RIFF reader: 8, 16, 24 and 32-bit PCM plus 32-bit float, and 4-bit IMA ADPCM (what
    // the pak holds, a quarter the size), any channel count folded to mono, any sample rate
    // walked to the target with linear interpolation.
    internal static class Wav
    {
        public static float[]? Decode(byte[] bytes, int targetRate, out string problem)
        {
            problem = "";
            if (bytes.Length < 12 || ReadTag(bytes, 0) != "RIFF" || ReadTag(bytes, 8) != "WAVE")
            {
                problem = "not a RIFF WAVE file";
                return null;
            }
            int format = 0, channels = 0, rate = 0, bits = 0, blockAlign = 0;
            int dataStart = -1, dataLength = 0;
            int at = 12;
            while (at + 8 <= bytes.Length)
            {
                string tag = ReadTag(bytes, at);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (size < 0)
                {
                    break;
                }
                if (tag == "fmt ")
                {
                    if (size < 16 || body + 16 > bytes.Length)
                    {
                        problem = "fmt chunk cut short";
                        return null;
                    }
                    format = BitConverter.ToInt16(bytes, body);
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    blockAlign = BitConverter.ToInt16(bytes, body + 12);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                    if (format == -2 && size >= 26 && body + 26 <= bytes.Length)
                    {
                        format = BitConverter.ToInt16(bytes, body + 24);
                    }
                }
                else if (tag == "data")
                {
                    dataStart = body;
                    dataLength = Math.Min(size, bytes.Length - body);
                    break;
                }
                at = (int)Math.Min((long)body + size + (size & 1), bytes.Length);
            }
            if (dataStart < 0 || channels <= 0 || rate <= 0)
            {
                problem = "no fmt/data chunk";
                return null;
            }
            bool isFloat = format == 3;
            float[] mono;
            if (format == 0x11)
            {
                if (bits != 4 || blockAlign < 8 || blockAlign <= 4 * channels)
                {
                    problem = "unsupported ADPCM layout";
                    return null;
                }
                mono = Ima(bytes, dataStart, dataLength, channels, blockAlign);
            }
            else
            {
                if (!(isFloat && bits == 32) && !(format == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32)))
                {
                    problem = "unsupported format " + format + " at " + bits + " bits";
                    return null;
                }
                int bytesPerSample = bits / 8;
                int frames = dataLength / (bytesPerSample * channels);
                mono = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int p = dataStart + (f * channels + ch) * bytesPerSample;
                        sum += Sample(bytes, p, bits, isFloat);
                    }
                    mono[f] = sum / channels;
                }
            }
            int frames2 = mono.Length;
            if (rate == targetRate)
            {
                return mono;
            }
            int outFrames = (int)((long)frames2 * targetRate / rate);
            float[] resampled = new float[outFrames];
            double step = (double)rate / targetRate;
            for (int i = 0; i < outFrames; i++)
            {
                double src = i * step;
                int a = (int)src;
                int b = Math.Min(a + 1, frames2 - 1);
                float t = (float)(src - a);
                resampled[i] = mono[a] + (mono[b] - mono[a]) * t;
            }
            return resampled;
        }

        private static readonly int[] ImaIndex = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        private static readonly int[] ImaStep =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
            1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
            12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        // IMA ADPCM, block by block: a 16-bit start and a step index per channel, then nibbles,
        // four bytes per channel at a time when there is more than one. Folded to mono.
        private static float[] Ima(byte[] b, int start, int length, int channels, int blockAlign)
        {
            int blocks = length / blockAlign;
            int perBlock = (blockAlign - 4 * channels) * 2 / channels + 1;
            float[] outSamples = new float[blocks * perBlock];
            int[] pred = new int[channels];
            int[] idx = new int[channels];
            int w = 0;
            for (int blk = 0; blk < blocks; blk++)
            {
                int p = start + blk * blockAlign;
                for (int ch = 0; ch < channels; ch++)
                {
                    pred[ch] = BitConverter.ToInt16(b, p + ch * 4);
                    idx[ch] = Math.Min(88, Math.Max(0, (int)b[p + ch * 4 + 2]));
                }
                float first = 0f;
                for (int ch = 0; ch < channels; ch++) first += pred[ch];
                outSamples[w++] = first / channels / 32768f;
                int q = p + 4 * channels;
                int end = p + blockAlign;
                int made = 1;
                while (q + 4 * channels <= end && made < perBlock)
                {
                    // Eight nibbles per channel, channels interleaved four bytes at a time.
                    for (int n = 0; n < 8 && made < perBlock; n++)
                    {
                        float sum = 0f;
                        for (int ch = 0; ch < channels; ch++)
                        {
                            int byteAt = q + ch * 4 + n / 2;
                            int nib = n % 2 == 0 ? b[byteAt] & 0x0F : b[byteAt] >> 4;
                            int step = ImaStep[idx[ch]];
                            int diff = step >> 3;
                            if ((nib & 1) != 0) diff += step >> 2;
                            if ((nib & 2) != 0) diff += step >> 1;
                            if ((nib & 4) != 0) diff += step;
                            if ((nib & 8) != 0) pred[ch] -= diff; else pred[ch] += diff;
                            if (pred[ch] > 32767) pred[ch] = 32767; else if (pred[ch] < -32768) pred[ch] = -32768;
                            idx[ch] = Math.Min(88, Math.Max(0, idx[ch] + ImaIndex[nib]));
                            sum += pred[ch];
                        }
                        outSamples[w++] = sum / channels / 32768f;
                        made++;
                    }
                    q += 4 * channels;
                }
            }
            if (w < outSamples.Length)
            {
                Array.Resize(ref outSamples, w);
            }
            return outSamples;
        }

        private static float Sample(byte[] b, int p, int bits, bool isFloat)
        {
            switch (bits)
            {
                case 8: return (b[p] - 128) / 128f;
                case 16: return BitConverter.ToInt16(b, p) / 32768f;
                case 24: return ((b[p] << 8 | b[p + 1] << 16 | b[p + 2] << 24) >> 8) / 8388608f;
                default: return isFloat ? BitConverter.ToSingle(b, p) : BitConverter.ToInt32(b, p) / 2147483648f;
            }
        }

        private static string ReadTag(byte[] b, int at)
        {
            return at + 4 <= b.Length ? System.Text.Encoding.ASCII.GetString(b, at, 4) : "";
        }
    }
}
