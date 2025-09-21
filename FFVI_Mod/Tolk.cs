using System;
using System.Runtime.InteropServices;

namespace Tolk
{
    /// <summary>
    /// Provides a simple interface for screen reader output using the Tolk library.
    /// Tolk allows applications to output information through supported screen readers.
    /// </summary>
    public class Tolk
    {
        private const string TOLK_DLL = "Tolk.dll";

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_IsLoaded();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI(bool trySAPI);

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_PreferSAPI(bool preferSAPI);

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_HasSpeech();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_HasBraille();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output(string str, bool interrupt);

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Speak(string str, bool interrupt);

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Braille(string str);

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_IsSpeaking();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_Silence();

        /// <summary>
        /// Initializes Tolk and loads screen reader drivers.
        /// </summary>
        public void Load()
        {
            Tolk_Load();
        }

        /// <summary>
        /// Checks if Tolk has been loaded and a screen reader driver is active.
        /// </summary>
        /// <returns>True if Tolk is loaded and a screen reader is available.</returns>
        public bool IsLoaded()
        {
            return Tolk_IsLoaded();
        }

        /// <summary>
        /// Unloads Tolk and frees resources.
        /// </summary>
        public void Unload()
        {
            Tolk_Unload();
        }

        /// <summary>
        /// Sets whether to try using SAPI if no screen reader is detected.
        /// </summary>
        /// <param name="trySAPI">True to try SAPI as fallback.</param>
        public void TrySAPI(bool trySAPI)
        {
            Tolk_TrySAPI(trySAPI);
        }

        /// <summary>
        /// Sets whether to prefer SAPI over detected screen readers.
        /// </summary>
        /// <param name="preferSAPI">True to prefer SAPI.</param>
        public void PreferSAPI(bool preferSAPI)
        {
            Tolk_PreferSAPI(preferSAPI);
        }

        /// <summary>
        /// Gets the name of the currently active screen reader.
        /// </summary>
        /// <returns>Name of the screen reader, or null if none detected.</returns>
        public string DetectScreenReader()
        {
            IntPtr ptr = Tolk_DetectScreenReader();
            if (ptr == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringUni(ptr);
        }

        /// <summary>
        /// Checks if the current screen reader supports speech output.
        /// </summary>
        /// <returns>True if speech is supported.</returns>
        public bool HasSpeech()
        {
            return Tolk_HasSpeech();
        }

        /// <summary>
        /// Checks if the current screen reader supports braille output.
        /// </summary>
        /// <returns>True if braille is supported.</returns>
        public bool HasBraille()
        {
            return Tolk_HasBraille();
        }

        /// <summary>
        /// Outputs text through both speech and braille if available.
        /// </summary>
        /// <param name="text">Text to output.</param>
        /// <param name="interrupt">Whether to interrupt current speech.</param>
        /// <returns>True if output was successful.</returns>
        public bool Output(string text, bool interrupt = false)
        {
            return Tolk_Output(text, interrupt);
        }

        /// <summary>
        /// Speaks text through the screen reader.
        /// </summary>
        /// <param name="text">Text to speak.</param>
        /// <param name="interrupt">Whether to interrupt current speech.</param>
        /// <returns>True if speech was successful.</returns>
        public bool Speak(string text, bool interrupt = false)
        {
            return Tolk_Speak(text, interrupt);
        }

        /// <summary>
        /// Outputs text to braille display.
        /// </summary>
        /// <param name="text">Text to display in braille.</param>
        /// <returns>True if braille output was successful.</returns>
        public bool Braille(string text)
        {
            return Tolk_Braille(text);
        }

        /// <summary>
        /// Checks if the screen reader is currently speaking.
        /// </summary>
        /// <returns>True if speech is in progress.</returns>
        public bool IsSpeaking()
        {
            return Tolk_IsSpeaking();
        }

        /// <summary>
        /// Silences current speech output.
        /// </summary>
        /// <returns>True if silence was successful.</returns>
        public bool Silence()
        {
            return Tolk_Silence();
        }
    }
}