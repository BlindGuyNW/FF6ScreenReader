using System;
using System.Collections.Generic;
using MelonLoader;
using Il2CppLast.Management;
using Il2CppLast.Data.User;
using Il2CppLast.Systems;
using Il2CppLast.UI;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Patches;
using static FFVI_ScreenReader.Utils.TextUtils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Announces which acquired characters can equip the currently selected equipment
    /// when 'I' key is pressed in the Items menu.
    /// Only works for equipment (weapons/armor), silent for consumables/key items.
    /// </summary>
    public static class ItemEquipAnnouncer
    {
        private const int CONTENT_TYPE_WEAPON = 2;
        private const int CONTENT_TYPE_ARMOR = 3;

        /// <summary>
        /// Tries to announce equip requirements for the currently selected item.
        /// Returns true if the keypress was handled (item was equipment), false otherwise.
        /// </summary>
        public static bool TryAnnounceEquipRequirements()
        {
            try
            {
                var itemData = ItemMenuTracker.LastSelectedItem;
                if (itemData == null)
                    return false;

                int itemType = itemData.ItemType;

                // Only process equipment (weapons and armor)
                if (itemType != CONTENT_TYPE_WEAPON && itemType != CONTENT_TYPE_ARMOR)
                    return false;

                // Verify we're actually in the item list menu
                var itemListController = UnityEngine.Object.FindObjectOfType<Il2CppLast.UI.KeyInput.ItemListController>();
                if (itemListController == null || itemListController.gameObject == null ||
                    !itemListController.gameObject.activeInHierarchy)
                    return false;

                int contentId = itemData.contentId;

                var userDataManager = UserDataManager.Instance();
                if (userDataManager == null)
                    return false;

                // Get OwnedItemData from contentId for EquipUtility check
                var ownedItemData = userDataManager.SearchOwnedItem(contentId);
                if (ownedItemData == null)
                    return false;

                // Get acquired characters (isAll=false for only encountered/available)
                var characters = userDataManager.GetOwnedCharactersClone(false);
                if (characters == null || characters.Count == 0)
                    return false;

                // Check each character using their JobId for equipment compatibility
                var canEquipNames = new List<string>();
                for (int i = 0; i < characters.Count; i++)
                {
                    var character = characters[i];
                    if (character == null)
                        continue;

                    try
                    {
                        bool canEquip = EquipUtility.CanEquipped(ownedItemData, character.JobId);
                        if (canEquip)
                        {
                            string charName = character.Name;
                            if (!string.IsNullOrEmpty(charName))
                            {
                                charName = StripIconMarkup(charName);
                                if (!string.IsNullOrEmpty(charName))
                                    canEquipNames.Add(charName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ItemEquipAnnouncer] Error checking character: {ex.Message}");
                    }
                }

                // Build and announce the result
                string announcement;
                if (canEquipNames.Count == 0)
                {
                    announcement = T("No characters can equip");
                }
                else
                {
                    announcement = string.Format(T("Can equip: {0}"), string.Join(", ", canEquipNames));
                }

                FFVI_ScreenReaderMod.SpeakText(announcement);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ItemEquipAnnouncer] Error: {ex.Message}");
                return false;
            }
        }
    }
}
