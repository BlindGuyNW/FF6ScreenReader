using System;
using System.Runtime.InteropServices;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Low-level waveOut channel management for concurrent audio playback.
    /// Each SoundChannel has its own waveOut handle and pre-allocated buffer.
    /// </summary>
    public static class AudioChannel
    {
        #region waveOut P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID,
            ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        #endregion

        #region Channel State

        private class ChannelState
        {
            public IntPtr WaveOutHandle;
            public IntPtr BufferPtr;
            public WAVEHDR Header;
            public bool IsPlaying;
            public bool HeaderPrepared;
            public bool IsLooping;
            public readonly object Lock = new object();
        }

        private static ChannelState[] channels;
        private static WAVEFORMATEX waveFormat;
        private static bool initialized = false;
        private static int channelCount;

        #endregion

        public static void Initialize()
        {
            if (initialized) return;

            waveFormat = new WAVEFORMATEX
            {
                wFormatTag = 1,
                nChannels = 2,
                nSamplesPerSec = (uint)SoundConstants.SAMPLE_RATE,
                nAvgBytesPerSec = (uint)(SoundConstants.SAMPLE_RATE * 4),
                nBlockAlign = 4,
                wBitsPerSample = 16,
                cbSize = 0
            };

            channelCount = Enum.GetValues(typeof(SoundChannel)).Length;
            channels = new ChannelState[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                channels[i] = new ChannelState();
                int result = waveOutOpen(out channels[i].WaveOutHandle,
                    SoundConstants.WaveFlags.WAVE_MAPPER,
                    ref waveFormat, IntPtr.Zero, IntPtr.Zero,
                    (uint)SoundConstants.WaveFlags.CALLBACK_NULL);

                if (result != 0)
                {
                    MelonLogger.Error($"[AudioChannel] Failed to open waveOut for channel {i}: error {result}");
                    channels[i].WaveOutHandle = IntPtr.Zero;
                }
                else
                {
                    channels[i].BufferPtr = Marshal.AllocHGlobal(SoundConstants.CHANNEL_BUFFER_SIZE);
                }
            }

            initialized = true;
        }

        public static void Shutdown()
        {
            if (!initialized || channels == null) return;

            for (int i = 0; i < channelCount; i++)
            {
                if (channels[i] != null)
                {
                    lock (channels[i].Lock)
                    {
                        if (channels[i].WaveOutHandle != IntPtr.Zero)
                        {
                            waveOutReset(channels[i].WaveOutHandle);
                            if (channels[i].HeaderPrepared)
                            {
                                waveOutUnprepareHeader(channels[i].WaveOutHandle, ref channels[i].Header,
                                    (uint)Marshal.SizeOf<WAVEHDR>());
                                channels[i].HeaderPrepared = false;
                            }
                            waveOutClose(channels[i].WaveOutHandle);
                            channels[i].WaveOutHandle = IntPtr.Zero;
                        }
                        if (channels[i].BufferPtr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(channels[i].BufferPtr);
                            channels[i].BufferPtr = IntPtr.Zero;
                        }
                    }
                }
            }

            initialized = false;
        }

        public static void Play(byte[] wavData, SoundChannel channel, bool loop = false, int volumePercent = 50)
        {
            if (wavData == null || !initialized) return;

            var state = channels[(int)channel];
            if (state?.WaveOutHandle == IntPtr.Zero) return;

            lock (state.Lock)
            {
                ResetChannel(state);

                if (wavData.Length <= SoundConstants.WAV_HEADER_SIZE) return;

                int dataLength = wavData.Length - SoundConstants.WAV_HEADER_SIZE;
                Marshal.Copy(wavData, SoundConstants.WAV_HEADER_SIZE, state.BufferPtr, dataLength);

                if (volumePercent != 50)
                    ScaleSamples(state.BufferPtr, dataLength, volumePercent);

                PrepareAndWrite(state, dataLength, loop);
            }
        }

        public static void PlayDirect(SoundChannel channel, int dataLength, Action<IntPtr> fillBuffer)
        {
            if (!initialized || fillBuffer == null) return;

            var state = channels[(int)channel];
            if (state?.WaveOutHandle == IntPtr.Zero) return;

            lock (state.Lock)
            {
                ResetChannel(state);
                fillBuffer(state.BufferPtr);
                PrepareAndWrite(state, dataLength, loop: false);
            }
        }

        public static void Stop(SoundChannel channel)
        {
            if (!initialized || channels == null) return;

            var state = channels[(int)channel];
            if (state?.WaveOutHandle == IntPtr.Zero) return;

            lock (state.Lock)
            {
                ResetChannel(state);
            }
        }

        public static bool IsPlaying(SoundChannel channel)
        {
            if (!initialized || channels == null) return false;
            var state = channels[(int)channel];
            if (state == null) return false;
            lock (state.Lock)
            {
                return state.IsPlaying;
            }
        }

        private static void ResetChannel(ChannelState state)
        {
            if (state.IsPlaying || state.HeaderPrepared)
            {
                waveOutReset(state.WaveOutHandle);
                if (state.HeaderPrepared)
                {
                    waveOutUnprepareHeader(state.WaveOutHandle, ref state.Header,
                        (uint)Marshal.SizeOf<WAVEHDR>());
                    state.HeaderPrepared = false;
                }
                state.IsPlaying = false;
                state.IsLooping = false;
            }
        }

        private static void PrepareAndWrite(ChannelState state, int dataLength, bool loop)
        {
            state.Header = new WAVEHDR
            {
                lpData = state.BufferPtr,
                dwBufferLength = (uint)dataLength,
                dwBytesRecorded = 0,
                dwUser = IntPtr.Zero,
                dwFlags = loop ? (SoundConstants.WaveFlags.WHDR_BEGINLOOP | SoundConstants.WaveFlags.WHDR_ENDLOOP) : 0,
                dwLoops = loop ? 0xFFFFFFFF : 0,
                lpNext = IntPtr.Zero,
                reserved = IntPtr.Zero
            };

            int prepResult = waveOutPrepareHeader(state.WaveOutHandle, ref state.Header,
                (uint)Marshal.SizeOf<WAVEHDR>());

            if (prepResult == 0)
            {
                state.HeaderPrepared = true;
                int writeResult = waveOutWrite(state.WaveOutHandle, ref state.Header,
                    (uint)Marshal.SizeOf<WAVEHDR>());

                if (writeResult == 0)
                {
                    state.IsPlaying = true;
                    state.IsLooping = loop;
                }
                else
                {
                    waveOutUnprepareHeader(state.WaveOutHandle, ref state.Header,
                        (uint)Marshal.SizeOf<WAVEHDR>());
                    state.HeaderPrepared = false;
                }
            }
        }

        private static void ScaleSamples(IntPtr bufferPtr, int length, int volumePercent)
        {
            if (volumePercent == 50) return;
            float multiplier = volumePercent / 50.0f;
            int sampleCount = length / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = Marshal.ReadInt16(bufferPtr, i * 2);
                int scaled = (int)(sample * multiplier);
                scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
                Marshal.WriteInt16(bufferPtr, i * 2, (short)scaled);
            }
        }
    }
}
