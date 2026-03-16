#nullable enable

using System;
using System.Collections.Generic;

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Represents the result of a scene loading operation, containing information
    /// about the loaded scenes, skipped scenes, errors encountered, and performance metrics
    /// </summary>
    public sealed class SceneLoadResult
    {
        public SceneGroup LoadedGroup;
        public List<string> LoadedScenes;
        public List<string> SkippedScenes;
        public List<Exception> Errors;
        public float TotalLoadTime;
        public Dictionary<string, float> RangeTimings;
        public bool IsSuccess => Errors.Count == 0;
    }
}