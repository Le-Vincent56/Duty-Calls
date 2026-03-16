#nullable enable

using System;
using System.Threading.Tasks;
using DutyCalls.Adapters.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DutyCalls.Adapters.Bootstrapping
{
    /// <summary>
    /// App-lifetime bootstrapper responsible for loading:
    /// 1) Persistent "Systems" SceneGroup (kept loaded)
    /// 2) Initial SceneGroup (must contain exactly one SceneType.Gameplay owner)
    /// Then unloading the startup scene (Build index 0) in both Editor and builds.
    ///
    /// "Inject before Awake" is achieved by:
    /// - SceneLoader loading scenes with activateOnLoad:false
    /// - SceneLoader activating scenes deterministically (Systems -> Gameplay owner -> remaining)
    /// - Scene-local bootstrappers using very negative DefaultExecutionOrder to inject before other Awakes
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bootstrapper : MonoBehaviour
    {
        private const string ConfigResourcePath = "BootstrapperConfig";
        private const int StartupSceneBuildIndex = 0;

        private static bool _isInitialized;
        private static Bootstrapper? _instance;

        private SceneLoader? _sceneLoader;
        private bool _bootStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void Init()
        {
            // Exit case - already initialized
            if (_isInitialized) return;

            _isInitialized = true;

            GameObject root = new GameObject("Bootstrapper");
            DontDestroyOnLoad(root);
            root.AddComponent<Bootstrapper>();
        }

        private void Awake()
        {
            // Exit case - already initialized
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneLoader? loader = GetComponent<SceneLoader>();
            if (!loader) loader = gameObject.AddComponent<SceneLoader>();

            _sceneLoader = loader;
        }

        private async void Start()
        {
            // Exit case - already booted
            if (_bootStarted) return;

            _bootStarted = true;

            try
            {
                await ExecuteBootSequenceAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Executes the boot sequence asynchronously, which involves loading the persistent systems scene group
        /// and optionally an initial scene group based on the configuration. If no default initial scene group
        /// is configured, a warning is logged.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous boot sequence operation.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the SceneLoader instance is unavailable during the boot sequence execution.
        /// </exception>
        private async Task ExecuteBootSequenceAsync()
        {
            BootstrapperConfig config = LoadConfig();
            SceneLoader sceneLoader = _sceneLoader ?? throw new InvalidOperationException("SceneLoader not available.");
            
            // Load the persistent scene group
            SceneGroup? persistent = config.PersistentSystemsSceneGroup;
            if (persistent)
            {
                SceneLoadRequest persistentRequest = SceneLoadRequest.Create()
                    .WithSceneGroup(persistent)
                    .MarkScenesAsPersistent()
                    .Build();
                await sceneLoader.LoadAsync(persistentRequest);
            }

            // Attempt to load the initial scene group
            SceneGroup? initial = GetInitialSceneGroup(config);
            if (initial)
            {
                SceneLoadRequest request = SceneLoadRequest.Create()
                    .WithSceneGroup(initial)
                    .Build();
                await sceneLoader.LoadAsync(request);
            }
            else
            {
                Debug.LogWarning("[Bootstrapper] No DefaultInitialSceneGroup configured.");
            }

            await UnloadStartupSceneAsync();
        }

        /// <summary>
        /// Loads the Bootstrapper configuration from the Unity Resources folder.
        /// If the configuration is not found at the expected path, an exception is thrown.
        /// </summary>
        /// <returns>
        /// An instance of <see cref="BootstrapperConfig"/> containing the configuration details for the bootstrap process.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the Bootstrapper configuration file is not located at the expected resource path.
        /// </exception>
        private static BootstrapperConfig LoadConfig()
        {
            BootstrapperConfig? loaded = Resources.Load<BootstrapperConfig>(ConfigResourcePath);
            if (!loaded)
            {
                throw new InvalidOperationException($"BootstrapperConfig not found at Resources/{ConfigResourcePath}.asset");
            }

            return loaded;
        }

        /// <summary>
        /// Retrieves the initial scene group to load during the boot sequence.
        /// If an override is specified in the Unity Editor preferences, it attempts to load the override.
        /// Otherwise, the default initial scene group from the configuration is returned.
        /// </summary>
        /// <param name="config">
        /// The configuration object providing details about the default initial scene group
        /// and other bootstrapping settings.
        /// </param>
        /// <returns>
        /// The <see cref="SceneGroup"/> to be used as the initial scene group, or null if no valid group is determined.
        /// </returns>
        private static SceneGroup? GetInitialSceneGroup(BootstrapperConfig config)
        {
#if UNITY_EDITOR
            string overrideGuid = EditorPrefs.GetString("Bootstrapper_SceneGroupOverride", string.Empty);
            
            // Exit case - no override
            if (string.IsNullOrEmpty(overrideGuid)) return config.DefaultInitialSceneGroup;
            
            string overridePath = AssetDatabase.GUIDToAssetPath(overrideGuid);

            // Exit case - the override path is empty
            if (string.IsNullOrEmpty(overridePath)) return config.DefaultInitialSceneGroup;
                
            SceneGroup? overrideGroup = AssetDatabase.LoadAssetAtPath<SceneGroup>(overridePath);
                    
            // Exit case - the override group is not valid
            if (!overrideGroup) return config.DefaultInitialSceneGroup;
                    
            Debug.Log("[Bootstrapper] Using Editor override: " + overrideGroup.name);
            return overrideGroup;
#else
            return config.DefaultInitialSceneGroup;
#endif
        }

        /// <summary>
        /// Asynchronously unloads the startup scene, identified by the build index 0, if it is valid and currently loaded.
        /// Ensures the proper cleanup of the initial startup scene in both Editor and builds.
        /// </summary>
        /// <returns>
        /// A Task that represents the asynchronous unload operation. The Task completes when the scene is fully unloaded.
        /// </returns>
        private static async Task UnloadStartupSceneAsync()
        {
            string path = SceneUtility.GetScenePathByBuildIndex(StartupSceneBuildIndex);
            
            // Exit case - no path
            if (string.IsNullOrEmpty(path)) return;

            Scene startupScene = SceneManager.GetSceneByPath(path);
            
            // Exit case - startup scene invalid or not unloaded
            if (!startupScene.IsValid() || !startupScene.isLoaded) return;

            AsyncOperation? unload = SceneManager.UnloadSceneAsync(startupScene);
            
            // Exit case - no unload operation
            if (unload == null) return;

            while (!unload.isDone)
            {
                await Task.Yield();
            }
        }
    }
}