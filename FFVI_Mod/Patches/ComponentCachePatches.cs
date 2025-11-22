using HarmonyLib;
using Il2CppLast.Map;
using FFVI_ScreenReader.Utils;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Harmony patches to automatically register game components in GameObjectCache.
    /// Eliminates need for expensive FindObjectOfType calls throughout the mod.
    /// </summary>
    public static class ComponentCachePatches
    {
        // Patches will be added here
    }
}
