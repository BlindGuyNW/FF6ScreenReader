using System;
using System.Collections.Generic;
using System.IO;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Generates WAV tone buffers for wall tones, footsteps, beacons, and beep tones.
    /// All methods return byte[] WAV data with 44-byte headers.
    /// </summary>
    public static class ToneGenerator
    {
        /// <summary>
        /// Writes a standard 44-byte PCM WAV header to a BinaryWriter.
        /// </summary>
        public static void WriteWavHeader(BinaryWriter writer, int channels, int sampleRate, int bitsPerSample, int dataSize)
        {
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                          // fmt chunk size
            writer.Write((short)1);                    // PCM format
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
        }

        /// <summary>
        /// Converts a mono 16-bit WAV to stereo by duplicating each sample to both channels.
        /// </summary>
        public static byte[] MonoToStereo(byte[] monoWav)
        {
            if (monoWav == null || monoWav.Length < 44) return monoWav;

            using (var reader = new BinaryReader(new MemoryStream(monoWav)))
            {
                reader.ReadBytes(4);  // "RIFF"
                reader.ReadInt32();   // file size
                reader.ReadBytes(4);  // "WAVE"
                reader.ReadBytes(4);  // "fmt "
                int fmtSize = reader.ReadInt32();
                reader.ReadInt16();   // audio format
                int channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                reader.ReadInt32();   // byte rate
                reader.ReadInt16();   // block align
                int bitsPerSample = reader.ReadInt16();

                if (fmtSize > 16)
                    reader.ReadBytes(fmtSize - 16);

                reader.ReadBytes(4);  // "data"
                int dataSize = reader.ReadInt32();

                if (channels == 2)
                    return monoWav;

                byte[] monoData = reader.ReadBytes(dataSize);

                int stereoDataSize = dataSize * 2;

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    WriteWavHeader(writer, 2, sampleRate, bitsPerSample, stereoDataSize);

                    for (int i = 0; i < monoData.Length; i += 2)
                    {
                        writer.Write(monoData[i]);
                        writer.Write(monoData[i + 1]);
                        writer.Write(monoData[i]);
                        writer.Write(monoData[i + 1]);
                    }

                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Generates a 16-bit WAV file containing a "thud" sound with soft attack and noise mix.
        /// Used for wall bump sounds.
        /// </summary>
        public static byte[] GenerateThudTone(int frequency, int durationMs, float volume)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;
            int samples = (sampleRate * durationMs) / 1000;
            int attackSamples = samples / 4;
            var random = new Random(42);
            int dataSize = samples * 2;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 1, sampleRate, SoundConstants.BITS_PER_SAMPLE, dataSize);

                double filteredNoise = 0;

                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / sampleRate;

                    double attackLinear = Math.Min(1.0, (double)i / attackSamples);
                    double attack = attackLinear * attackLinear;
                    double decay = (double)(samples - i) / samples;
                    double envelope = attack * decay;

                    double sine = Math.Sin(2 * Math.PI * frequency * t);
                    double rawNoise = (random.NextDouble() * 2 - 1);
                    filteredNoise = filteredNoise * 0.9 + rawNoise * 0.1;
                    double noise = filteredNoise * 0.3 * attack;
                    double value = (sine * 0.7 + noise) * volume * envelope;

                    writer.Write((short)(value * 32767));
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a short 16-bit click/tap sound for footsteps using filtered noise burst.
        /// </summary>
        public static byte[] GenerateClickTone(int frequency, int durationMs, float volume)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;
            int samples = (sampleRate * durationMs) / 1000;
            var random = new Random(42);
            int dataSize = samples * 2;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 1, sampleRate, SoundConstants.BITS_PER_SAMPLE, dataSize);

                for (int i = 0; i < samples; i++)
                {
                    double decay = Math.Exp(-10.0 * i / samples);
                    double noise = (random.NextDouble() * 2 - 1) * volume * decay;
                    writer.Write((short)(noise * 32767));
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a 16-bit stereo WAV tone with constant-power panning and decay envelope.
        /// Used for one-shot wall tone pings.
        /// </summary>
        public static byte[] GenerateStereoTone(int frequency, int durationMs, float volume, float pan)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;
            int samples = (sampleRate * durationMs) / 1000;
            int dataSize = samples * 4;

            double panAngle = pan * Math.PI / 2;
            float leftVol = volume * (float)Math.Cos(panAngle);
            float rightVol = volume * (float)Math.Sin(panAngle);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, sampleRate, SoundConstants.BITS_PER_SAMPLE, dataSize);

                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / sampleRate;

                    int attackSamples = samples / 10;
                    double attack = Math.Min(1.0, (double)i / attackSamples);
                    double decay = (double)(samples - i) / samples;
                    double envelope = attack * decay;

                    double sineValue = Math.Sin(2 * Math.PI * frequency * t) * envelope;

                    writer.Write((short)(sineValue * leftVol * 32767));
                    writer.Write((short)(sineValue * rightVol * 32767));
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a stereo WAV tone with sustain and volume baked in during generation.
        /// This preserves dynamic range at low volumes (no post-scaling quantization).
        /// </summary>
        public static byte[] GenerateStereoToneSustainWithVolume(int frequency, int durationMs, float baseVolume, float pan, int volumePercent)
        {
            float scaledVolume = baseVolume * (volumePercent / 50.0f);
            return GenerateStereoToneSustain(frequency, durationMs, scaledVolume, pan);
        }

        /// <summary>
        /// Generates a 16-bit stereo WAV tone with sustain (no decay) for seamless hardware looping.
        /// Uses cycle-aligned sample counts to ensure the waveform starts and ends at the same phase.
        /// </summary>
        public static byte[] GenerateStereoToneSustain(int frequency, int durationMs, float volume, float pan)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;

            double samplesPerCycle = (double)sampleRate / frequency;
            int targetSamples = (sampleRate * durationMs) / 1000;
            int numCycles = (int)Math.Round(targetSamples / samplesPerCycle);
            if (numCycles < 1) numCycles = 1;
            int samples = (int)Math.Round(numCycles * samplesPerCycle);
            int dataSize = samples * 4;

            double panAngle = pan * Math.PI / 2;
            float leftVol = volume * (float)Math.Cos(panAngle);
            float rightVol = volume * (float)Math.Sin(panAngle);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, sampleRate, SoundConstants.BITS_PER_SAMPLE, dataSize);

                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / sampleRate;
                    double sineValue = Math.Sin(2 * Math.PI * frequency * t);

                    writer.Write((short)(sineValue * leftVol * 32767));
                    writer.Write((short)(sineValue * rightVol * 32767));
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a 16-bit stereo WAV containing a short beep followed by silence.
        /// When hardware-looped, the silence gap creates a pulsing effect.
        /// Uses cycle-aligned beep duration for clean sound at loop boundary.
        /// Used for EXP counter beeps.
        /// </summary>
        public static byte[] GenerateBeepTone(int frequency, int totalDurationMs, int beepDurationMs, float volume, float pan)
        {
            int sampleRate = SoundConstants.SAMPLE_RATE;
            int totalSamples = (sampleRate * totalDurationMs) / 1000;
            int beepSamples = (sampleRate * beepDurationMs) / 1000;

            double samplesPerCycle = (double)sampleRate / frequency;
            int numCycles = (int)Math.Round(beepSamples / samplesPerCycle);
            if (numCycles < 1) numCycles = 1;
            beepSamples = (int)Math.Round(numCycles * samplesPerCycle);

            if (totalSamples <= beepSamples)
                totalSamples = beepSamples + (sampleRate * 50) / 1000;

            int dataSize = totalSamples * 4;

            double panAngle = pan * Math.PI / 2;
            float leftVol = volume * (float)Math.Cos(panAngle);
            float rightVol = volume * (float)Math.Sin(panAngle);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, sampleRate, SoundConstants.BITS_PER_SAMPLE, dataSize);

                int attackSamples = beepSamples / 8;
                int decaySamples = beepSamples / 4;
                int decayStart = beepSamples - decaySamples;

                for (int i = 0; i < totalSamples; i++)
                {
                    if (i < beepSamples)
                    {
                        double t = (double)i / sampleRate;
                        double envelope = 1.0;

                        if (i < attackSamples)
                            envelope = (double)i / attackSamples;
                        else if (i >= decayStart)
                            envelope = (double)(beepSamples - i) / decaySamples;

                        double sineValue = Math.Sin(2 * Math.PI * frequency * t) * envelope;

                        writer.Write((short)(sineValue * leftVol * 32767));
                        writer.Write((short)(sineValue * rightVol * 32767));
                    }
                    else
                    {
                        writer.Write((short)0);
                        writer.Write((short)0);
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Mixes multiple 16-bit WAV files into a single stereo WAV.
        /// Used for playing multiple wall tones in different directions simultaneously.
        /// </summary>
        public static byte[] MixWavFiles(List<byte[]> wavFiles)
        {
            if (wavFiles == null || wavFiles.Count == 0) return null;

            const int HEADER_SIZE = SoundConstants.WAV_HEADER_SIZE;

            int maxDataLength = 0;
            foreach (var wav in wavFiles)
            {
                if (wav.Length > HEADER_SIZE)
                {
                    int dataLen = wav.Length - HEADER_SIZE;
                    if (dataLen > maxDataLength)
                        maxDataLength = dataLen;
                }
            }

            if (maxDataLength == 0) return null;

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteWavHeader(writer, 2, SoundConstants.SAMPLE_RATE, SoundConstants.BITS_PER_SAMPLE, maxDataLength);

                int sampleCount = maxDataLength / 2;
                for (int i = 0; i < sampleCount; i++)
                {
                    int mixedValue = 0;
                    int count = 0;

                    foreach (var wav in wavFiles)
                    {
                        int pos = HEADER_SIZE + (i * 2);
                        if (pos + 1 < wav.Length)
                        {
                            short sample = (short)(wav[pos] | (wav[pos + 1] << 8));
                            mixedValue += sample;
                            count++;
                        }
                    }

                    if (count > 1)
                    {
                        double headroom = 1.0 / Math.Sqrt(count);
                        mixedValue = (int)(mixedValue * headroom);
                    }

                    mixedValue = Math.Max(short.MinValue, Math.Min(short.MaxValue, mixedValue));
                    writer.Write((short)mixedValue);
                }

                return ms.ToArray();
            }
        }
    }
}
