using Il2CppLast.Defaine;

namespace FFVI_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading title menu commands.
    /// Converts enum values to user-friendly names.
    /// </summary>
    public static class TitleMenuReader
    {
        /// <summary>
        /// Convert TitleCommandId enum to user-friendly names.
        /// </summary>
        public static string GetTitleCommandName(TitleCommandId commandId)
        {
            return commandId switch
            {
                TitleCommandId.NewGame => "New Game",
                TitleCommandId.LoadGame => "Load Game",
                TitleCommandId.Extra => "Extra",
                TitleCommandId.Option => "Options",
                TitleCommandId.ExitGame => "Exit Game",
                TitleCommandId.StrongNewGame => "New Game Plus",
                TitleCommandId.Config => "Config",
                TitleCommandId.PictureBook => "Picture Book",
                TitleCommandId.SoundPlayer => "Sound Player",
                TitleCommandId.Gallery => "Gallery",
                TitleCommandId.ExtraBack => "Back",
                TitleCommandId.ExtraDungeon => "Extra Dungeon",
                TitleCommandId.PORTAL => "Portal",
                TitleCommandId.PrivacyPolicy => "Privacy Policy",
                TitleCommandId.License => "License",
                TitleCommandId.GamePadSetting => "Gamepad Settings",
                TitleCommandId.KeyboardSetting => "Keyboard Settings",
                TitleCommandId.Language => "Language",
                TitleCommandId.ScreenSettings => "Screen Settings",
                TitleCommandId.Back => "Back",
                TitleCommandId.SettingConfigBack => "Back",
                TitleCommandId.SoundSettings => "Sound Settings",
                _ => commandId.ToString() // Fallback to enum name
            };
        }
    }
}