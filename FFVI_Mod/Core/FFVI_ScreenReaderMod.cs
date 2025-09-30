using MelonLoader;
using FFVI_ScreenReader.Utils;

[assembly: MelonInfo(typeof(FFVI_ScreenReader.Core.FFVI_ScreenReaderMod), "FFVI Screen Reader", "1.0.0", "YourName")]
[assembly: MelonGame("SQUARE ENIX, Inc.", "FINAL FANTASY VI")]

namespace FFVI_ScreenReader.Core
{
    /// <summary>
    /// Main mod class for FFVI Screen Reader.
    /// Provides screen reader accessibility support for Final Fantasy VI Pixel Remaster.
    /// </summary>
    public class FFVI_ScreenReaderMod : MelonMod
    {
        private static TolkWrapper tolk;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("FFVI Screen Reader Mod loaded!");
            LoggerInstance.Msg("*** COROUTINE CLEANUP SYSTEM ENABLED - TESTING MANAGED COROUTINES ***");

            // Initialize Tolk for screen reader support
            tolk = new TolkWrapper();
            tolk.Load();
        }

        public override void OnDeinitializeMelon()
        {
            CoroutineManager.CleanupAll();
            tolk?.Unload();
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// </summary>
        public static void SpeakText(string text)
        {
            tolk?.Speak(text);
        }
    }
}