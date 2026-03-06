using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using Il2CppLast.UI.KeyInput;
using FFVI_ScreenReader.Core;
using static FFVI_ScreenReader.Utils.TextUtils;
using static FFVI_ScreenReader.Utils.ModTextTranslator;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Section-based navigation buffer for the item equipment detail screen.
    /// Auto-activates when the detail screen opens. User navigates with Up/Down arrows.
    /// </summary>
    public static class ItemDetailNavigator
    {
        public static bool IsActive { get; private set; }

        private static List<string> sections = new List<string>();
        private static int currentIndex;
        private static ItemEquipmentController cachedController;

        /// <summary>
        /// Open the navigator with data from the equipment detail screen.
        /// Called from ItemDetailPatches when UpdateView fires.
        /// </summary>
        public static void Open(ItemEquipmentController controller)
        {
            try
            {
                cachedController = controller;
                BuildSections(controller);

                if (sections.Count == 0)
                {
                    MelonLogger.Warning("[ItemDetailNavigator] No sections built, not activating");
                    return;
                }

                currentIndex = 0;
                IsActive = true;
                MelonLogger.Msg($"[ItemDetailNavigator] Opened with {sections.Count} sections");
                FFVI_ScreenReaderMod.SpeakText(sections[0]);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[ItemDetailNavigator] Error opening: {ex.Message}");
                Close();
            }
        }

        /// <summary>
        /// Close the navigator and clear state.
        /// </summary>
        public static void Close()
        {
            IsActive = false;
            sections.Clear();
            currentIndex = 0;
            cachedController = null;
            MelonLogger.Msg("[ItemDetailNavigator] Closed");
        }

        /// <summary>
        /// Rebuild sections (e.g., after page toggle between description/parameter).
        /// </summary>
        public static void Refresh()
        {
            if (!IsActive || cachedController == null)
                return;

            try
            {
                int previousIndex = currentIndex;
                BuildSections(cachedController);
                // Clamp index to new range
                if (previousIndex >= sections.Count)
                    currentIndex = sections.Count > 0 ? sections.Count - 1 : 0;
                else
                    currentIndex = previousIndex;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[ItemDetailNavigator] Error refreshing: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle Up/Down input. Returns true if input was consumed.
        /// Also checks if the detail screen is still active and auto-closes if not.
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsActive)
                return false;

            // Auto-close if controller is no longer valid/active
            if (cachedController == null || cachedController.gameObject == null ||
                !cachedController.gameObject.activeInHierarchy)
            {
                Close();
                return false;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateDown();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateUp();
                return true;
            }

            return false;
        }

        private static void NavigateDown()
        {
            if (sections.Count == 0) return;

            if (currentIndex < sections.Count - 1)
            {
                currentIndex++;
                FFVI_ScreenReaderMod.SpeakText(sections[currentIndex]);
            }
            else
            {
                FFVI_ScreenReaderMod.SpeakText(T("Bottom"));
            }
        }

        private static void NavigateUp()
        {
            if (sections.Count == 0) return;

            if (currentIndex > 0)
            {
                currentIndex--;
                FFVI_ScreenReaderMod.SpeakText(sections[currentIndex]);
            }
            else
            {
                FFVI_ScreenReaderMod.SpeakText(T("Top"));
            }
        }

        private static void BuildSections(ItemEquipmentController controller)
        {
            sections.Clear();

            try
            {
                // Section 1: Item name + quantity
                string nameSection = BuildNameSection(controller);
                if (!string.IsNullOrEmpty(nameSection))
                    sections.Add(nameSection);

                // Read from the detail view's content lists
                var detailView = controller.detailController?.view;
                if (detailView != null)
                {
                    // Sections 2-5: Stats in logical groups
                    AddStatGroups(detailView);

                    // Section 6: Properties/Abilities
                    string abilitiesSection = BuildAbilitiesSection(detailView);
                    if (!string.IsNullOrEmpty(abilitiesSection))
                        sections.Add(abilitiesSection);

                    // Sections 7+: Elemental/Attribute info (one per category)
                    AddAttributeSections(detailView);

                    // Section 5: Magic info
                    string magicSection = BuildMagicSection(detailView);
                    if (!string.IsNullOrEmpty(magicSection))
                        sections.Add(magicSection);
                }

                // Section 6: Description text (always included)
                string description = GetDescription(controller);
                if (!string.IsNullOrEmpty(description))
                    sections.Add(description);

                // Section 7: Parameter message (always included)
                string paramMessage = GetParameterMessage(controller);
                if (!string.IsNullOrEmpty(paramMessage))
                    sections.Add(paramMessage);

                // Section 8: Equippable characters
                string equippable = BuildEquippableSection(controller);
                if (!string.IsNullOrEmpty(equippable))
                    sections.Add(equippable);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[ItemDetailNavigator] Error building sections: {ex.Message}");
            }
        }

        private static string BuildNameSection(ItemEquipmentController controller)
        {
            try
            {
                var data = controller.targetData;
                if (data == null) return null;

                string name = data.Name;
                if (string.IsNullOrEmpty(name)) return null;

                name = StripIconMarkup(name);
                int count = data.Count;
                return count > 0 ? string.Format(T("{0}, quantity {1}"), name, count) : name;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building name section: {ex.Message}");
                return null;
            }
        }

        private static void AddStatGroups(ItemEquipmentDetailView detailView)
        {
            try
            {
                var statusList = detailView.statusContentList;
                if (statusList == null || statusList.Count == 0)
                    return;

                var baseStats = new List<string>();
                var attackStats = new List<string>();
                var defenseStats = new List<string>();
                var evasionStats = new List<string>();

                for (int i = 0; i < statusList.Count; i++)
                {
                    var content = statusList[i];
                    if (content?.view == null) continue;

                    if (content.gameObject != null && !content.gameObject.activeInHierarchy)
                        continue;

                    string statName = GetTextSafe(content.view.nameText);
                    string statValue = GetTextSafe(content.view.valueText);
                    string plus = GetTextSafe(content.view.plusText);

                    if (string.IsNullOrEmpty(statName)) continue;

                    string formatted;
                    if (!string.IsNullOrEmpty(plus) && !string.IsNullOrEmpty(statValue))
                        formatted = $"{statName} {plus}{statValue}";
                    else if (!string.IsNullOrEmpty(statValue))
                        formatted = $"{statName} {statValue}";
                    else
                        formatted = statName;

                    // Categorize by name keywords
                    string nameLower = statName.ToLower();
                    if (nameLower.Contains("attack"))
                        attackStats.Add(formatted);
                    else if (nameLower.Contains("defense") || nameLower.Contains("def"))
                        defenseStats.Add(formatted);
                    else if (nameLower.Contains("evasion") || nameLower.Contains("eva"))
                        evasionStats.Add(formatted);
                    else
                        baseStats.Add(formatted);
                }

                if (baseStats.Count > 0)
                    sections.Add(string.Join(". ", baseStats));
                if (attackStats.Count > 0)
                    sections.Add(string.Join(". ", attackStats));
                if (defenseStats.Count > 0)
                    sections.Add(string.Join(". ", defenseStats));
                if (evasionStats.Count > 0)
                    sections.Add(string.Join(". ", evasionStats));
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building stat groups: {ex.Message}");
            }
        }

        private static string BuildAbilitiesSection(ItemEquipmentDetailView detailView)
        {
            try
            {
                var abilityList = detailView.abilityContentList;
                if (abilityList == null || abilityList.Count == 0)
                    return null;

                var parts = new List<string>();
                for (int i = 0; i < abilityList.Count; i++)
                {
                    var content = abilityList[i];
                    if (content?.view == null) continue;

                    if (content.gameObject != null && !content.gameObject.activeInHierarchy)
                        continue;

                    string abilityName = GetTextSafe(content.view.nameText);
                    string abilityValue = GetTextSafe(content.view.valueText);

                    if (string.IsNullOrEmpty(abilityName)) continue;

                    if (!string.IsNullOrEmpty(abilityValue))
                        parts.Add($"{abilityName} {abilityValue}");
                    else
                        parts.Add(abilityName);
                }

                return parts.Count > 0 ? string.Format(T("Properties: {0}"), string.Join(". ", parts)) : null;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building abilities section: {ex.Message}");
                return null;
            }
        }

        // Known FF6 element keywords to match against sprite names
        private static readonly string[] ElementKeywords = {
            "fire", "ice", "lightning", "thunder", "poison",
            "wind", "holy", "earth", "water"
        };

        // Hardcoded attributeType → element name (sprite names are numbered, not named)
        private static readonly Dictionary<int, string> AttributeTypeNames = new Dictionary<int, string>
        {
            { 1, "Fire" },
            { 5, "Ice" },
            { 3, "Lightning" },
            { 6, "Poison" },
            { 10, "Wind" },
            { 8, "Holy" },
            { 7, "Earth" },
            { 9, "Water" }
        };

        private static void AddAttributeSections(ItemEquipmentDetailView detailView)
        {
            try
            {
                var attributeList = detailView.attributeContentList;
                if (attributeList == null || attributeList.Count == 0)
                    return;

                for (int i = 0; i < attributeList.Count; i++)
                {
                    var content = attributeList[i];
                    if (content?.view == null) continue;

                    if (content.gameObject != null && !content.gameObject.activeInHierarchy)
                        continue;

                    string categoryName = GetTextSafe(content.view.nameText);
                    if (string.IsNullOrEmpty(categoryName)) continue;

                    var enabledElements = new List<string>();
                    var iconsList = content.view.iconsList;
                    if (iconsList != null)
                    {
                        for (int j = 0; j < iconsList.Count; j++)
                        {
                            var icon = iconsList[j];
                            if (icon == null) continue;

                            string spriteName = null;
                            try { spriteName = icon.image?.sprite?.name; } catch { }
                            string enableName = null;
                            try { enableName = icon.enableIconName; } catch { }
                            int attrType = -1;
                            try { attrType = (int)icon.attributeType; } catch { }

                            // Check if this element is enabled
                            // Strip "(Clone)" suffix Unity appends to instantiated sprites
                            bool isEnabled = false;
                            try
                            {
                                string cleanSprite = spriteName;
                                if (cleanSprite != null && cleanSprite.EndsWith("(Clone)"))
                                    cleanSprite = cleanSprite.Substring(0, cleanSprite.Length - 7);
                                isEnabled = (cleanSprite != null && enableName != null &&
                                             cleanSprite == enableName);
                            }
                            catch { }

                            if (!isEnabled) continue;

                            // Resolve element name
                            string elementName = ResolveElementName(enableName, attrType);
                            enabledElements.Add(elementName);
                        }
                    }

                    if (enabledElements.Count > 0)
                        sections.Add(string.Format(T("{0}: {1}"), categoryName, string.Join(", ", enabledElements)));
                    else
                        sections.Add(string.Format(T("{0}: {1}"), categoryName, T("none")));
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building attribute sections: {ex.Message}");
            }
        }

        private static string ResolveElementName(string enableIconName, int attributeType)
        {
            // Primary: hardcoded dictionary (sprite names are numbered, not named by element)
            if (attributeType >= 0 && AttributeTypeNames.TryGetValue(attributeType, out string name))
                return T(name);

            // Fallback: try keyword matching from enableIconName
            if (!string.IsNullOrEmpty(enableIconName))
            {
                string lowerName = enableIconName.ToLower();
                foreach (string keyword in ElementKeywords)
                {
                    if (lowerName.Contains(keyword))
                    {
                        string displayName = char.ToUpper(keyword[0]) + keyword.Substring(1);
                        if (keyword == "thunder") displayName = "Lightning";
                        return T(displayName);
                    }
                }
            }

            // Last resort: report raw type
            return string.Format(T("Element {0}"), attributeType);
        }

        private static string BuildMagicSection(ItemEquipmentDetailView detailView)
        {
            try
            {
                var magicList = detailView.magicContentList;
                if (magicList == null || magicList.Count == 0)
                    return null;

                var parts = new List<string>();
                for (int i = 0; i < magicList.Count; i++)
                {
                    var content = magicList[i];
                    if (content?.view == null) continue;

                    if (content.gameObject != null && !content.gameObject.activeInHierarchy)
                        continue;

                    string magicName = GetTextSafe(content.view.nameText);
                    string magicValue = GetTextSafe(content.view.valueText);

                    if (string.IsNullOrEmpty(magicName)) continue;

                    if (!string.IsNullOrEmpty(magicValue))
                        parts.Add($"{magicName} {magicValue}");
                    else
                        parts.Add(magicName);
                }

                return parts.Count > 0 ? string.Format(T("Magic: {0}"), string.Join(". ", parts)) : null;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building magic section: {ex.Message}");
                return null;
            }
        }

        private static string GetDescription(ItemEquipmentController controller)
        {
            try
            {
                var data = controller.targetData;
                if (data == null) return null;

                string desc = data.Description;
                if (string.IsNullOrEmpty(desc)) return null;

                desc = StripIconMarkup(desc);
                return string.IsNullOrEmpty(desc) ? null : desc;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error getting description: {ex.Message}");
                return null;
            }
        }

        private static string GetParameterMessage(ItemEquipmentController controller)
        {
            try
            {
                var data = controller.targetData;
                if (data == null) return null;

                string param = data.ParameterMessage;
                if (string.IsNullOrEmpty(param)) return null;

                param = StripIconMarkup(param);
                return string.IsNullOrEmpty(param) ? null : param;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error getting parameter message: {ex.Message}");
                return null;
            }
        }

        private static string BuildEquippableSection(ItemEquipmentController controller)
        {
            try
            {
                var equipableList = controller.equipableList;
                if (equipableList == null || equipableList.Count == 0)
                    return null;

                var names = new List<string>();
                for (int i = 0; i < equipableList.Count; i++)
                {
                    var equipable = equipableList[i];
                    if (equipable?.contentList == null) continue;

                    for (int j = 0; j < equipable.contentList.Count; j++)
                    {
                        var content = equipable.contentList[j];
                        if (content?.view == null) continue;

                        if (content.gameObject != null && !content.gameObject.activeInHierarchy)
                            continue;

                        string charName = GetTextSafe(content.view.nameText);
                        if (!string.IsNullOrEmpty(charName))
                            names.Add(charName);
                    }
                }

                return names.Count > 0 ? string.Format(T("Can equip: {0}"), string.Join(", ", names)) : null;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[ItemDetailNavigator] Error building equippable section: {ex.Message}");
                return null;
            }
        }

        private static string GetTextSafe(UnityEngine.UI.Text textComponent)
        {
            if (textComponent == null) return null;
            try
            {
                string text = textComponent.text;
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
