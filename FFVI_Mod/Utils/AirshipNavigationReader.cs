using System;

namespace FFVI_ScreenReader.Utils
{
    /// <summary>
    /// Utility class for reading airship navigation state (direction, altitude).
    /// Converts airship controller data into human-readable announcements.
    /// </summary>
    public static class AirshipNavigationReader
    {
        /// <summary>
        /// Convert rotation angle (in degrees) to 8-way compass direction.
        /// </summary>
        /// <param name="rotation">Rotation angle in degrees (0-359)</param>
        /// <returns>Compass direction string (N, NE, E, SE, S, SW, W, NW)</returns>
        public static string GetCompassDirection(float rotation)
        {
            // Normalize rotation to 0-360 range
            float normalized = ((rotation % 360) + 360) % 360;

            // Divide into 8 segments of 45 degrees each
            // The rotation system has East/West reversed, so we swap them
            // N: 337.5-22.5 (0), NW: 22.5-67.5 (45), W: 67.5-112.5 (90), etc.
            if (normalized >= 337.5f || normalized < 22.5f)
                return "North";
            else if (normalized >= 22.5f && normalized < 67.5f)
                return "Northwest";  // Swapped from NE
            else if (normalized >= 67.5f && normalized < 112.5f)
                return "West";  // Swapped from E
            else if (normalized >= 112.5f && normalized < 157.5f)
                return "Southwest";  // Swapped from SE
            else if (normalized >= 157.5f && normalized < 202.5f)
                return "South";
            else if (normalized >= 202.5f && normalized < 247.5f)
                return "Southeast";  // Swapped from SW
            else if (normalized >= 247.5f && normalized < 292.5f)
                return "East";  // Swapped from W
            else // 292.5 - 337.5
                return "Northeast";  // Swapped from NW
        }

        /// <summary>
        /// Convert altitude ratio to readable description.
        /// </summary>
        /// <param name="altitudeRatio">Altitude ratio from FieldController (0.0 = ground, 1.0 = max altitude)</param>
        /// <returns>Human-readable altitude description</returns>
        public static string GetAltitudeDescription(float altitudeRatio)
        {
            // Divide altitude into meaningful levels
            if (altitudeRatio <= 0.0f)
                return "Ground level";
            else if (altitudeRatio < 0.33f)
                return "Low altitude";
            else if (altitudeRatio < 0.67f)
                return "Cruising altitude";
            else if (altitudeRatio < 1.0f)
                return "High altitude";
            else
                return "Maximum altitude";
        }
    }
}
