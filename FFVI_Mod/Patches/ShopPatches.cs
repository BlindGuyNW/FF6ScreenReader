using HarmonyLib;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;
using FFVI_ScreenReader.Utils;
using System.Collections;
using MelonLoader;

namespace FFVI_ScreenReader.Patches
{
    /// <summary>
    /// Patches for shop menu navigation.
    ///
    /// Working:
    /// - Shop command menu (Buy/Sell/Equipment/Back)
    /// - Item lists for buying/selling (item name + price)
    /// - Item descriptions (description + MP cost)
    /// - Quantity selection (quantity + total price)
    ///
    /// Not Yet Implemented:
    /// - Equipment submenu (entire screen is inaccessible)
    ///   Uses different navigation patterns (RB/LB for characters, LEFT/RIGHT for slots)
    ///   Requires visual debugging to identify the correct controller methods
    /// </summary>
    [HarmonyPatch]
    public static class ShopPatches
    {
        /// <summary>
        /// Announces shop command menu options (Buy, Sell, Equipment, Back).
        /// </summary>
        [HarmonyPatch(typeof(ShopCommandMenuController), nameof(ShopCommandMenuController.SetCursor))]
        [HarmonyPostfix]
        private static void AfterShopCommandSetCursor(ShopCommandMenuController __instance, int index)
        {
            try
            {
                if (__instance?.contentList == null || index < 0 || index >= __instance.contentList.Count)
                    return;

                var content = __instance.contentList[index];
                if (content?.view?.nameText == null)
                    return;

                string commandText = content.view.nameText.text;
                if (string.IsNullOrEmpty(commandText))
                    return;

                CoroutineManager.StartManaged(DelayedAnnounceShopCommand(commandText));
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopCommandSetCursor: {ex.Message}");
            }
        }

        private static string lastAnnouncedItem = "";

        /// <summary>
        /// Announces individual items in shop buy/sell lists with name and price.
        /// </summary>
        [HarmonyPatch(typeof(ShopListItemContentController), nameof(ShopListItemContentController.SetFocus))]
        [HarmonyPostfix]
        private static void AfterShopItemSetFocus(ShopListItemContentController __instance, bool isFocus)
        {
            try
            {
                if (!isFocus || __instance == null)
                    return;

                // Get item name from iconTextView
                string itemName = __instance.iconTextView?.nameText?.text;
                if (string.IsNullOrEmpty(itemName))
                    return;

                // Get price from shopListItemContentView
                string price = __instance.shopListItemContentView?.priceText?.text;
                string announcement = string.IsNullOrEmpty(price) ? itemName : $"{itemName}, {price}";

                // Store for later description announcement
                lastAnnouncedItem = itemName;

                CoroutineManager.StartManaged(DelayedAnnounceShopItem(announcement));
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AfterShopItemSetFocus: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces quantity changes in the buy/sell trade window.
        /// </summary>
        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.AddCount))]
        [HarmonyPostfix]
        private static void AfterAddCount(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
        }

        [HarmonyPatch(typeof(ShopTradeWindowController), nameof(ShopTradeWindowController.TakeCount))]
        [HarmonyPostfix]
        private static void AfterTakeCount(ShopTradeWindowController __instance)
        {
            AnnounceTradeWindowQuantity(__instance);
        }

        private static void AnnounceTradeWindowQuantity(ShopTradeWindowController controller)
        {
            try
            {
                if (controller?.view == null)
                    return;

                // Get quantity and total price
                string quantity = controller.view.selectCountText?.text;
                string totalPrice = controller.view.totarlPriceText?.text;

                if (!string.IsNullOrEmpty(quantity))
                {
                    string announcement = string.IsNullOrEmpty(totalPrice)
                        ? quantity
                        : $"{quantity}, {totalPrice}";

                    CoroutineManager.StartManaged(DelayedAnnounceQuantity(announcement));
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceTradeWindowQuantity: {ex.Message}");
            }
        }

        private static IEnumerator DelayedAnnounceShopCommand(string commandText)
        {
            yield return null; // Wait one frame for UI to update
            FFVI_ScreenReaderMod.SpeakText($"{commandText}");
        }

        private static IEnumerator DelayedAnnounceShopItem(string itemText)
        {
            yield return null; // Wait one frame for UI to update
            FFVI_ScreenReaderMod.SpeakText($"{itemText}");
        }

        private static IEnumerator DelayedAnnounceQuantity(string quantityText)
        {
            yield return null; // Wait one frame for UI to update
            FFVI_ScreenReaderMod.SpeakText($"{quantityText}");
        }

        // TODO: Shop equipment menu is not yet accessible.
        // The shop equipment screen uses different navigation patterns than the regular equipment menu:
        // - Character selection (RB/LB buttons)
        // - Equipment slot navigation (LEFT/RIGHT arrows)
        //
        // We haven't identified which controller methods are called during this navigation.
        // This will require visual debugging or sighted assistance to understand the UI behavior.
        //
        // Note: Regular equipment menu (from main menu) works fine with existing ItemMenuPatches.
    }
}
