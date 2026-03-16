#nullable enable

using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DutyCalls.Adapters.Scenes
{
    public enum SceneType
    {
        None,
        Gameplay,
        UI,
        Systems,
        Environment,
        Audio,
        VFX,
        Debug
    }

    /// <summary>
    /// A ScriptableObject that represents a group of scene assets in Unity;
    /// this class is primarily used for organizing and managing multiple
    /// related additive scenes into a single logical collection.
    /// </summary>
    [CreateAssetMenu(menuName = "Duty Calls/Scene Group", fileName = "New Scene Group")]
    public sealed class SceneGroup : ScriptableObject
    {
        [Serializable]
        public class SceneEntry
        {
            public AssetReference SceneReference;
            public string SceneName;
            public SceneType SceneType = SceneType.None;
        }

        [SerializeField] private SceneEntry[] scenes = Array.Empty<SceneEntry>();

        public SceneEntry[] Scenes => scenes;
    }
}