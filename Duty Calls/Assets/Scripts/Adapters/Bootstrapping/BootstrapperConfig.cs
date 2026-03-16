#nullable enable

using DutyCalls.Adapters.Scenes;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping
{
    [CreateAssetMenu(fileName = "BootstrapperConfig", menuName = "Duty Calls/Bootstrapping/Bootstrapper Config")]
    public sealed class BootstrapperConfig : ScriptableObject
    {
        [SerializeField] private SceneGroup? persistentSystemsSceneGroup;
        [SerializeField] private SceneGroup? defaultInitialSceneGroup;
        public SceneGroup? PersistentSystemsSceneGroup => persistentSystemsSceneGroup;
        public SceneGroup? DefaultInitialSceneGroup => defaultInitialSceneGroup;
    }
}