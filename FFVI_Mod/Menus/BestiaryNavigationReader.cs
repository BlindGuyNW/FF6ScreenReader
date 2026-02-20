using System;
using System.Collections.Generic;
using MelonLoader;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Patches;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Virtual buffer navigation for bestiary detail stats.
    /// Arrow keys navigate through a flat list of visible stats read from UI elements.
    /// </summary>
    public static class BestiaryNavigationReader
    {
        private static List<BestiaryStatEntry> statBuffer = null;
        private static List<int> groupStartIndices = null;
        private static int currentIndex = 0;

        /// <summary>
        /// Initialize the stat buffer from the current detail view's UI elements.
        /// Called when entering the bestiary detail view.
        /// </summary>
        public static void Initialize(List<BestiaryStatEntry> entries)
        {
            statBuffer = entries;
            currentIndex = 0;

            // Build group start indices
            groupStartIndices = new List<int>();
            if (statBuffer != null && statBuffer.Count > 0)
            {
                BestiaryStatGroup lastGroup = statBuffer[0].Group;
                groupStartIndices.Add(0);

                for (int i = 1; i < statBuffer.Count; i++)
                {
                    if (statBuffer[i].Group != lastGroup)
                    {
                        groupStartIndices.Add(i);
                        lastGroup = statBuffer[i].Group;
                    }
                }
            }
        }

        /// <summary>
        /// Clear navigation state.
        /// </summary>
        public static void Reset()
        {
            statBuffer = null;
            groupStartIndices = null;
            currentIndex = 0;
        }

        /// <summary>
        /// Whether navigation is currently active (has a populated stat buffer).
        /// </summary>
        public static bool IsActive => statBuffer != null && statBuffer.Count > 0 &&
                                        BestiaryNavigationTracker.Instance.IsNavigationActive;

        /// <summary>
        /// Navigate to the next stat (wraps to top).
        /// </summary>
        public static void NavigateNext()
        {
            if (!IsActive) return;

            currentIndex = (currentIndex + 1) % statBuffer.Count;
            ReadCurrentStat();
        }

        /// <summary>
        /// Navigate to the previous stat (wraps to bottom).
        /// </summary>
        public static void NavigatePrevious()
        {
            if (!IsActive) return;

            currentIndex--;
            if (currentIndex < 0)
                currentIndex = statBuffer.Count - 1;
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the first stat of the next group.
        /// </summary>
        public static void JumpToNextGroup()
        {
            if (!IsActive || groupStartIndices == null || groupStartIndices.Count == 0) return;

            int nextGroupIndex = -1;
            for (int i = 0; i < groupStartIndices.Count; i++)
            {
                if (groupStartIndices[i] > currentIndex)
                {
                    nextGroupIndex = groupStartIndices[i];
                    break;
                }
            }

            // Wrap to first group
            if (nextGroupIndex == -1)
                nextGroupIndex = groupStartIndices[0];

            currentIndex = nextGroupIndex;
            ReadCurrentStatWithGroup();
        }

        /// <summary>
        /// Jump to the first stat of the previous group.
        /// </summary>
        public static void JumpToPreviousGroup()
        {
            if (!IsActive || groupStartIndices == null || groupStartIndices.Count == 0) return;

            int prevGroupIndex = -1;
            for (int i = groupStartIndices.Count - 1; i >= 0; i--)
            {
                if (groupStartIndices[i] < currentIndex)
                {
                    prevGroupIndex = groupStartIndices[i];
                    break;
                }
            }

            // Wrap to last group
            if (prevGroupIndex == -1)
                prevGroupIndex = groupStartIndices[groupStartIndices.Count - 1];

            currentIndex = prevGroupIndex;
            ReadCurrentStatWithGroup();
        }

        /// <summary>
        /// Jump to the top (first stat).
        /// </summary>
        public static void JumpToTop()
        {
            if (!IsActive) return;

            currentIndex = 0;
            ReadCurrentStat();
        }

        /// <summary>
        /// Jump to the bottom (last stat).
        /// </summary>
        public static void JumpToBottom()
        {
            if (!IsActive) return;

            currentIndex = statBuffer.Count - 1;
            ReadCurrentStat();
        }

        /// <summary>
        /// Read the stat at the current index.
        /// </summary>
        public static void ReadCurrentStat()
        {
            if (!IsActive) return;

            if (currentIndex < 0 || currentIndex >= statBuffer.Count)
            {
                currentIndex = 0;
                if (statBuffer.Count == 0) return;
            }

            var entry = statBuffer[currentIndex];
            FFVI_ScreenReaderMod.SpeakText(entry.ToString(), true);
        }

        /// <summary>
        /// Read the current stat with group name prefix (used for group jumps).
        /// </summary>
        private static void ReadCurrentStatWithGroup()
        {
            if (!IsActive) return;

            if (currentIndex < 0 || currentIndex >= statBuffer.Count)
            {
                currentIndex = 0;
                if (statBuffer.Count == 0) return;
            }

            var entry = statBuffer[currentIndex];
            string groupName = GetGroupDisplayName(entry.Group);
            FFVI_ScreenReaderMod.SpeakText($"{groupName}. {entry}", true);
        }

        /// <summary>
        /// Get display-friendly name for a stat group.
        /// </summary>
        private static string GetGroupDisplayName(BestiaryStatGroup group)
        {
            switch (group)
            {
                case BestiaryStatGroup.MonsterData: return "Monster Data";
                case BestiaryStatGroup.Status: return "Status";
                case BestiaryStatGroup.Options: return "Rewards";
                case BestiaryStatGroup.Items: return "Items";
                case BestiaryStatGroup.Properties: return "Properties";
                default: return "Other";
            }
        }
    }
}
