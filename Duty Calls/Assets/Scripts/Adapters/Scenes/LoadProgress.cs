#nullable enable

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Represents the progress of a loading operation, providing details on
    /// normalized progress, the current range, and the progress of the current range
    /// </summary>
    public struct LoadProgress
    {
        public float NormalizedProgress;
        public string CurrentRange;
        public float RangeProgress;
        public string CurrentOperation;
    }
}