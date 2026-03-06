using System;
using Il2CppInterop.Runtime;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Reads gallery list item data and formats speech output.
    /// Uses unsafe pointer access for IL2CPP field reads.
    /// </summary>
    public static class GalleryReader
    {
        // GalleryTopListController (Last.UI.KeyInput) field offsets
        private const int OFFSET_SELECT_DATA = 0x50;  // selectData (GalleryListCotentData)

        // GalleryListCotentData field offsets
        private const int OFFSET_NUMBER = 0x10;  // <number>k__BackingField (int)
        private const int OFFSET_NAME = 0x18;    // <name>k__BackingField (string ptr)

        /// <summary>
        /// Read gallery item data from a GalleryTopListController pointer.
        /// Reads selectData at +0x50, then number/name from the content data.
        /// Returns false if any pointer is null/zero.
        /// </summary>
        public static unsafe bool ReadContentFromPointer(IntPtr controllerPtr, out int number, out string name)
        {
            number = 0;
            name = null;

            if (controllerPtr == IntPtr.Zero)
                return false;

            // Read selectData (GalleryListCotentData*) at +0x50
            IntPtr contentDataPtr = *(IntPtr*)((byte*)controllerPtr.ToPointer() + OFFSET_SELECT_DATA);
            if (contentDataPtr == IntPtr.Zero)
                return false;

            // Read number (int) at +0x10 from contentData
            number = *(int*)((byte*)contentDataPtr.ToPointer() + OFFSET_NUMBER);

            // Read name (Il2CppString*) at +0x18 from contentData
            IntPtr namePtr = *(IntPtr*)((byte*)contentDataPtr.ToPointer() + OFFSET_NAME);
            if (namePtr == IntPtr.Zero)
                return false;

            name = IL2CPP.Il2CppStringToManaged(namePtr);
            return !string.IsNullOrEmpty(name);
        }

        /// <summary>
        /// Format a gallery list entry: "001: Image Name"
        /// </summary>
        public static string ReadListEntry(int number, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return $"{number:D3}: {name}";
        }
    }
}
