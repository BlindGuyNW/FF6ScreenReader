namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Centralized announcement deduplication context strings.
    /// Each context represents a distinct UI element or state that tracks
    /// what was last announced to avoid repeating the same text.
    /// </summary>
    public static class AnnouncementContexts
    {
        // Title menu
        public const string TITLE_MENU_COMMAND = "TitleMenu.Command";

        // Map state
        public const string GAME_STATE_MAP_ID = "GameState.MapId";

        // Bestiary
        public const string BESTIARY_LIST_ENTRY = "Bestiary.ListEntry";
        public const string BESTIARY_DETAIL_STAT = "Bestiary.DetailStat";
        public const string BESTIARY_FORMATION = "Bestiary.Formation";
        public const string BESTIARY_MAP = "Bestiary.Map";
        public const string BESTIARY_STATE = "Bestiary.State";

        // Music Player
        public const string MUSIC_LIST_ENTRY = "MusicPlayer.ListEntry";

        // Gallery
        public const string GALLERY_LIST_ENTRY = "Gallery.ListEntry";
    }
}
