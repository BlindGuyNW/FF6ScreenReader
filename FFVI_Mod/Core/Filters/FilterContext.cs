using Il2Cpp;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;

namespace FFVI_ScreenReader.Core.Filters
{
    /// <summary>
    /// Provides shared context for filter operations to avoid repeated lookups.
    /// </summary>
    public class FilterContext
    {
        /// <summary>
        /// Current player position in world coordinates.
        /// </summary>
        public Vector3 PlayerPosition { get; set; }

        /// <summary>
        /// Reference to the player controller.
        /// </summary>
        public FieldPlayerController PlayerController { get; set; }

        /// <summary>
        /// Reference to the current map handle.
        /// </summary>
        public IMapAccessor MapHandle { get; set; }

        /// <summary>
        /// Reference to the player's field entity.
        /// </summary>
        public FieldPlayer FieldPlayer { get; set; }

        /// <summary>
        /// Creates a new filter context from the current game state.
        /// </summary>
        public FilterContext()
        {
            PlayerController = Utils.GameObjectCache.Get<FieldPlayerController>();

            if (PlayerController?.fieldPlayer != null)
            {
                FieldPlayer = PlayerController.fieldPlayer;
                PlayerPosition = FieldPlayer.transform.position;
                MapHandle = PlayerController.mapHandle;
            }
            else
            {
                PlayerPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Creates a filter context with explicit player controller.
        /// </summary>
        public FilterContext(FieldPlayerController controller)
        {
            PlayerController = controller;

            if (controller?.fieldPlayer != null)
            {
                FieldPlayer = controller.fieldPlayer;
                PlayerPosition = FieldPlayer.transform.position;
                MapHandle = controller.mapHandle;
            }
            else
            {
                PlayerPosition = Vector3.zero;
            }
        }
    }
}
