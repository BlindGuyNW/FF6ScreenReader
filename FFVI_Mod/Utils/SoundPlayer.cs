using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFVI_ScreenReader.Core;
using MelonLoader;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Sound channels for concurrent playback.
    /// Each channel has its own waveOut handle and plays completely independently.
    /// </summary>
    public enum SoundChannel
    {
        Movement,    // Footsteps only
        WallBump,    // Wall bump sounds (separate from footsteps to avoid timing conflicts)
        WallTone,    // Wall proximity tones (loopable)
        Beacon,      // Audio beacon pings
        Counter      // EXP counter beep (loopable, battle results)
    }

    /// <summary>
    /// Request for a wall tone in a specific direction (adjacent only).
    /// </summary>
    public struct WallToneRequest
    {
        public SoundPlayer.Direction Direction;

        public WallToneRequest(SoundPlayer.Direction dir)
        {
            Direction = dir;
        }
    }

    /// <summary>
    /// High-level sound playback facade.
    /// Delegates channel management to AudioChannel and tone synthesis to ToneGenerator.
    /// </summary>
    public static class SoundPlayer
    {
        #region Pre-cached Sounds

        private static byte[] wallBumpWav;
        private static byte[] footstepWav;
        private static byte[] expCounterWav;

        // One-shot wall tones (with decay)
        private static byte[] wallToneNorth;
        private static byte[] wallToneSouth;
        private static byte[] wallToneEast;
        private static byte[] wallToneWest;

        // Sustain wall tones (for looping)
        private static byte[] wallToneNorthSustain;
        private static byte[] wallToneSouthSustain;
        private static byte[] wallToneEastSustain;
        private static byte[] wallToneWestSustain;

        #endregion

        // Track current wall tone directions as a bitmask to avoid unnecessary loop restarts
        private static int currentWallDirectionsMask = 0;
        private static int lastWallToneVolume = 50;

        // Cache for generated tone buffers keyed by (directionMask, volume)
        private const int ToneCacheMaxSize = 16;
        private static readonly Dictionary<(int dirMask, int volume), byte[]> _toneCache = new Dictionary<(int, int), byte[]>();

        /// <summary>
        /// Cardinal direction enum for wall tones.
        /// </summary>
        public enum Direction
        {
            North,
            South,
            East,
            West
        }

        /// <summary>
        /// Initializes audio channels and pre-generates all cached tones.
        /// Call this once during mod initialization.
        /// </summary>
        public static void Initialize()
        {
            AudioChannel.Initialize();

            // Wall bump: deep thud with soft attack
            wallBumpWav = ToneGenerator.MonoToStereo(
                ToneGenerator.GenerateThudTone(
                    SoundConstants.WallBump.FREQUENCY,
                    SoundConstants.WallBump.DURATION_MS,
                    SoundConstants.WallBump.VOLUME));

            // Footstep: light click
            footstepWav = ToneGenerator.MonoToStereo(
                ToneGenerator.GenerateClickTone(
                    SoundConstants.Footstep.FREQUENCY,
                    SoundConstants.Footstep.DURATION_MS,
                    SoundConstants.Footstep.VOLUME));

            float bv = SoundConstants.WallToneVolumeMultipliers.BASE_VOLUME;
            int oneShot = SoundConstants.WallToneTiming.ONE_SHOT_DURATION_MS;
            int sustain = SoundConstants.WallToneTiming.SUSTAIN_DURATION_MS;

            // One-shot tones (with decay) for single-direction pings
            wallToneNorth = ToneGenerator.GenerateStereoTone(SoundConstants.WallToneFrequencies.NORTH, oneShot, bv * SoundConstants.WallToneVolumeMultipliers.NORTH, SoundConstants.WallTonePan.NORTH);
            wallToneSouth = ToneGenerator.GenerateStereoTone(SoundConstants.WallToneFrequencies.SOUTH, oneShot, bv * SoundConstants.WallToneVolumeMultipliers.SOUTH, SoundConstants.WallTonePan.SOUTH);
            wallToneEast  = ToneGenerator.GenerateStereoTone(SoundConstants.WallToneFrequencies.EAST,  oneShot, bv * SoundConstants.WallToneVolumeMultipliers.EAST,  SoundConstants.WallTonePan.EAST);
            wallToneWest  = ToneGenerator.GenerateStereoTone(SoundConstants.WallToneFrequencies.WEST,  oneShot, bv * SoundConstants.WallToneVolumeMultipliers.WEST,  SoundConstants.WallTonePan.WEST);

            // Sustain tones (no decay, cycle-aligned for seamless looping)
            wallToneNorthSustain = ToneGenerator.GenerateStereoToneSustain(SoundConstants.WallToneFrequencies.NORTH, sustain, bv * SoundConstants.WallToneVolumeMultipliers.NORTH, SoundConstants.WallTonePan.NORTH);
            wallToneSouthSustain = ToneGenerator.GenerateStereoToneSustain(SoundConstants.WallToneFrequencies.SOUTH, sustain, bv * SoundConstants.WallToneVolumeMultipliers.SOUTH, SoundConstants.WallTonePan.SOUTH);
            wallToneEastSustain  = ToneGenerator.GenerateStereoToneSustain(SoundConstants.WallToneFrequencies.EAST,  sustain, bv * SoundConstants.WallToneVolumeMultipliers.EAST,  SoundConstants.WallTonePan.EAST);
            wallToneWestSustain  = ToneGenerator.GenerateStereoToneSustain(SoundConstants.WallToneFrequencies.WEST,  sustain, bv * SoundConstants.WallToneVolumeMultipliers.WEST,  SoundConstants.WallTonePan.WEST);

            // EXP counter beep: short tone + silence for rapid ticking effect
            expCounterWav = ToneGenerator.GenerateBeepTone(
                SoundConstants.ExpCounter.FREQUENCY,
                SoundConstants.ExpCounter.BEEP_MS + SoundConstants.ExpCounter.SILENCE_MS,
                SoundConstants.ExpCounter.BEEP_MS,
                SoundConstants.ExpCounter.VOLUME,
                0.5f); // center pan
        }

        /// <summary>
        /// Shuts down all audio channels and clears cached sounds.
        /// </summary>
        public static void Shutdown()
        {
            AudioChannel.Shutdown();
            currentWallDirectionsMask = 0;
            lastWallToneVolume = 50;
            _toneCache.Clear();
        }

        #region Public Playback Methods

        /// <summary>
        /// Plays the wall bump sound effect on the WallBump channel.
        /// </summary>
        public static void PlayWallBump()
        {
            if (wallBumpWav == null) return;
            AudioChannel.Play(wallBumpWav, SoundChannel.WallBump, false,
                FFVI_ScreenReaderMod.WallBumpVolume);
        }

        /// <summary>
        /// Plays the footstep click sound on the Movement channel.
        /// </summary>
        public static void PlayFootstep()
        {
            if (footstepWav == null) return;
            AudioChannel.Play(footstepWav, SoundChannel.Movement, false,
                FFVI_ScreenReaderMod.FootstepVolume);
        }

        /// <summary>
        /// Plays a one-shot wall proximity tone for the given direction.
        /// </summary>
        public static void PlayWallTone(Direction dir)
        {
            byte[] tone = GetOneShotTone(dir);
            if (tone == null) return;
            AudioChannel.Play(tone, SoundChannel.WallTone, false,
                FFVI_ScreenReaderMod.WallToneVolume);
        }

        /// <summary>
        /// Plays one-shot wall tones (multiple directions mixed).
        /// </summary>
        public static void PlayWallTones(WallToneRequest[] requests)
        {
            if (requests == null || requests.Length == 0) return;

            var tonesToMix = new List<byte[]>();
            foreach (var req in requests)
            {
                byte[] tone = GetOneShotTone(req.Direction);
                if (tone != null)
                    tonesToMix.Add(tone);
            }

            if (tonesToMix.Count == 0) return;

            int volume = FFVI_ScreenReaderMod.WallToneVolume;

            if (tonesToMix.Count == 1)
            {
                AudioChannel.Play(tonesToMix[0], SoundChannel.WallTone, false, volume);
                return;
            }

            byte[] mixed = ToneGenerator.MixWavFiles(tonesToMix);
            if (mixed != null)
                AudioChannel.Play(mixed, SoundChannel.WallTone, false, volume);
        }

        /// <summary>
        /// Plays wall tones as a continuous looping sound.
        /// Volume is baked into tone generation to preserve dynamic range at low volumes.
        /// Only restarts the loop if directions or volume changed.
        /// Uses tone caching to avoid regenerating identical tones.
        /// </summary>
        public static void PlayWallTonesLooped(IList<Direction> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                StopWallTone();
                return;
            }

            int volume = FFVI_ScreenReaderMod.WallToneVolume;
            int newMask = DirectionsToBitmask(directions);
            if (newMask == currentWallDirectionsMask && volume == lastWallToneVolume)
                return;

            currentWallDirectionsMask = newMask;
            lastWallToneVolume = volume;

            // Check cache first
            var cacheKey = (newMask, volume);
            if (_toneCache.TryGetValue(cacheKey, out byte[] cachedBuffer))
            {
                AudioChannel.Play(cachedBuffer, SoundChannel.WallTone, loop: true, volumePercent: 50);
                return;
            }

            float bv = SoundConstants.WallToneVolumeMultipliers.BASE_VOLUME;
            float scaledVol = volume / 50.0f;
            int dur = SoundConstants.WallToneTiming.SUSTAIN_DURATION_MS;
            var tonesToMix = new List<byte[]>();

            foreach (var dir in directions)
            {
                float dirVol = bv * GetDirectionVolumeMultiplier(dir) * scaledVol;
                float pan = GetDirectionPan(dir);
                int freq = GetDirectionFrequency(dir);
                tonesToMix.Add(ToneGenerator.GenerateStereoToneSustain(freq, dur, dirVol, pan));
            }

            if (tonesToMix.Count == 0)
            {
                StopWallTone();
                return;
            }

            byte[] loopBuffer = tonesToMix.Count == 1
                ? tonesToMix[0]
                : ToneGenerator.MixWavFiles(tonesToMix);

            if (loopBuffer != null)
            {
                if (_toneCache.Count >= ToneCacheMaxSize)
                    _toneCache.Clear();
                _toneCache[cacheKey] = loopBuffer;

                // Volume already baked in during generation - use 50 (no scaling)
                AudioChannel.Play(loopBuffer, SoundChannel.WallTone, loop: true, volumePercent: 50);
            }
        }

        /// <summary>
        /// Stops the continuous wall tone loop.
        /// </summary>
        public static void StopWallTone()
        {
            currentWallDirectionsMask = 0;
            lastWallToneVolume = 50;
            AudioChannel.Stop(SoundChannel.WallTone);
        }

        /// <summary>
        /// Returns true if the wall tone channel is currently playing.
        /// </summary>
        public static bool IsWallTonePlaying() => AudioChannel.IsPlaying(SoundChannel.WallTone);

        /// <summary>
        /// Plays an audio beacon ping with directional panning.
        /// Writes PCM directly to the channel's unmanaged buffer for zero-allocation playback.
        /// </summary>
        public static void PlayBeacon(bool isSouth, float pan, float volumeScale)
        {
            try
            {
                int frequency = isSouth ? SoundConstants.Beacon.FREQUENCY_SOUTH : SoundConstants.Beacon.FREQUENCY_NORTH;
                int beaconVolumePref = FFVI_ScreenReaderMod.BeaconVolume;
                float prefMultiplier = beaconVolumePref / 50.0f;
                float volume = Math.Max(SoundConstants.Beacon.MIN_VOLUME,
                    Math.Min(SoundConstants.Beacon.MAX_VOLUME, volumeScale * prefMultiplier));

                int samples = (SoundConstants.SAMPLE_RATE * SoundConstants.Beacon.DURATION_MS) / 1000;
                int dataLength = samples * 4; // stereo 16-bit

                double panAngle = pan * Math.PI / 2;
                float leftVol = volume * (float)Math.Cos(panAngle);
                float rightVol = volume * (float)Math.Sin(panAngle);

                AudioChannel.PlayDirect(SoundChannel.Beacon, dataLength, bufferPtr =>
                {
                    int attackSamples = samples / 10;
                    for (int i = 0; i < samples; i++)
                    {
                        double t = (double)i / SoundConstants.SAMPLE_RATE;
                        double attack = Math.Min(1.0, (double)i / attackSamples);
                        double decay = (double)(samples - i) / samples;
                        double envelope = attack * decay;
                        double sineValue = Math.Sin(2 * Math.PI * frequency * t) * envelope;

                        Marshal.WriteInt16(bufferPtr, i * 4, (short)(sineValue * leftVol * 32767));
                        Marshal.WriteInt16(bufferPtr, i * 4 + 2, (short)(sineValue * rightVol * 32767));
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SoundPlayer] Error playing beacon: {ex.Message}");
            }
        }

        /// <summary>
        /// Plays the EXP counter beep on loop (rapid ticking during EXP bar animation).
        /// </summary>
        public static void PlayExpCounter()
        {
            if (expCounterWav == null) return;
            int volume = FFVI_ScreenReaderMod.ExpCounterVolume;
            AudioChannel.Play(expCounterWav, SoundChannel.Counter, loop: true, volumePercent: volume);
        }

        /// <summary>
        /// Stops the EXP counter beep.
        /// </summary>
        public static void StopExpCounter()
        {
            AudioChannel.Stop(SoundChannel.Counter);
        }

        /// <summary>
        /// Stops playback on a specific channel. Delegates to AudioChannel.
        /// </summary>
        public static void StopChannel(SoundChannel channel) => AudioChannel.Stop(channel);

        #endregion

        #region Direction Helpers

        private static byte[] GetOneShotTone(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return wallToneNorth;
                case Direction.South: return wallToneSouth;
                case Direction.East:  return wallToneEast;
                case Direction.West:  return wallToneWest;
                default: return null;
            }
        }

        private static int GetDirectionFrequency(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return SoundConstants.WallToneFrequencies.NORTH;
                case Direction.South: return SoundConstants.WallToneFrequencies.SOUTH;
                case Direction.East:  return SoundConstants.WallToneFrequencies.EAST;
                case Direction.West:  return SoundConstants.WallToneFrequencies.WEST;
                default: return SoundConstants.WallToneFrequencies.NORTH;
            }
        }

        private static float GetDirectionVolumeMultiplier(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return SoundConstants.WallToneVolumeMultipliers.NORTH;
                case Direction.South: return SoundConstants.WallToneVolumeMultipliers.SOUTH;
                case Direction.East:  return SoundConstants.WallToneVolumeMultipliers.EAST;
                case Direction.West:  return SoundConstants.WallToneVolumeMultipliers.WEST;
                default: return 1.0f;
            }
        }

        private static float GetDirectionPan(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return SoundConstants.WallTonePan.NORTH;
                case Direction.South: return SoundConstants.WallTonePan.SOUTH;
                case Direction.East:  return SoundConstants.WallTonePan.EAST;
                case Direction.West:  return SoundConstants.WallTonePan.WEST;
                default: return 0.5f;
            }
        }

        /// <summary>
        /// Converts a direction list to a bitmask for fast comparison.
        /// </summary>
        private static int DirectionsToBitmask(IList<Direction> dirs)
        {
            int mask = 0;
            int count = dirs.Count;
            for (int i = 0; i < count; i++)
                mask |= (1 << (int)dirs[i]);
            return mask;
        }

        #endregion
    }
}
