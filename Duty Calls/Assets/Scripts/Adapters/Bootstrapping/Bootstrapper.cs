#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DutyCalls.Adapters.Bootstrapping.Installers;
using DutyCalls.Adapters.DI;
using DutyCalls.Adapters.Scenes;
using DutyCalls.Simulation.Core;
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

        private IAppServices? _appServices;
        private ServiceContainer? _appContainer;
        private SceneInjector? _injector;
        private bool _isAppContainerSealed;

        private ServiceContainer? _activeSceneContainer;

        private SceneLoader? _sceneLoader;
        private bool _bootStarted;

        internal IAppServices AppServices
        {
            get
            {
                if (_appServices == null)
                {
                    throw new InvalidOperationException("Bootstrapper kernel is not initialized.");
                }

                return _appServices;
            }
        }

        internal ServiceContainer AppContainer
        {
            get
            {
                if (_appContainer == null)
                {
                    throw new InvalidOperationException("Bootstrapper kernel is not initialized.");
                }

                return _appContainer;
            }
        }

        internal SceneInjector Injector
        {
            get
            {
                if (_injector == null)
                {
                    throw new InvalidOperationException("Bootstrapper kernel is not initialized.");
                }

                return _injector;
            }
        }

        /// <summary>
        /// Initializes the Bootstrapper by creating a new instance of the Bootstrapper component
        /// in a persistent GameObject. Ensures that the bootstrapper is instantiated only once
        /// during the application's lifecycle.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
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

            EnsureKernelInitialized();
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

        private void OnDestroy()
        {
            // Exit case - this is not the instance
            if (_instance != this) return;
            
            _instance = null;
            _activeSceneContainer = null;
            _isAppContainerSealed = false;
            ServiceContainer? appContainer = _appContainer;
            _appServices = null;
            _appContainer = null;
            _injector = null;
            appContainer?.Dispose();
        }
        
        /// <summary>
        /// Resets static fields of the Bootstrapper class, ensuring that the instance and initialization
        /// state are cleared. This is primarily used to reinitialize the Bootstrapper in scenarios where it
        /// needs to be reset, such as during a subsystem registration.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _isInitialized = false;
            _instance = null;
        }
        
        /// <summary>
        /// Attempts to retrieve the current instance of the Bootstrapper kernel if available.
        /// </summary>
        /// <param name="kernel">When this method returns, contains the current instance of the Bootstrapper kernel if available, or null if no instance exists.</param>
        /// <returns>True if the Bootstrapper kernel instance is successfully retrieved; otherwise, false.</returns>
        internal static bool TryGetKernel(out Bootstrapper? kernel)
        {
            kernel = _instance;
            return kernel;
        }

        /// <summary>
        /// Retrieves the current instance of the Bootstrapper kernel if available.
        /// </summary>
        /// <returns>The active instance of the Bootstrapper kernel.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the Bootstrapper kernel is not available.</exception>
        internal static Bootstrapper RequireKernel()
        {
            Bootstrapper? kernel = _instance;
            
            // Exit case - no kernel available
            if (!kernel)
                throw new InvalidOperationException("Bootstrapper kernel is not available.");

            return kernel;
        }

        /// <summary>
        /// Attempts to retrieve the currently active scene's service container if available.
        /// </summary>
        /// <param name="container">When this method returns, contains the active scene's service container if available, or null if no container exists.</param>
        /// <returns>True if the active scene's service container is successfully retrieved; otherwise, false.</returns>
        internal bool TryGetActiveSceneContainer(out ServiceContainer? container)
        {
            container = _activeSceneContainer;
            return container != null;
        }

        /// <summary>
        /// Sets the active scene container for the bootstrapper. This container is used to manage scene-specific services.
        /// </summary>
        /// <param name="container">The service container to be set as the active scene container. Must not be null.</param>
        /// <exception cref="InvalidOperationException">Thrown if an active scene container is already set and cannot be replaced.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the provided container is null.</exception>
        internal void SetActiveSceneContainer(ServiceContainer container)
        {
            // Exit case - no active scene container
            if (_activeSceneContainer != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException("Active scene container is already set.");
#else
                Debug.LogError("Active scene container is already set. Replacing container.");
#endif
            }

            _activeSceneContainer = container ?? throw new ArgumentNullException(nameof(container));
        }

        /// <summary>
        /// Clears the active scene container if it matches the specified container.
        /// </summary>
        /// <param name="container">The scene container to unset as the active scene container if it matches the current one.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="container"/> is null.</exception>
        internal void ClearActiveSceneContainer(ServiceContainer container)
        {
            // Exit case - no container provided
            if (container == null) throw new ArgumentNullException(nameof(container));

            // Exit case - the active scene container does not match the given one
            if (_activeSceneContainer != container) return;
            
            _activeSceneContainer = null;
        }

        /// <summary>
        /// Seals the application-level service container, marking it as immutable.
        /// Once sealed, no further modifications can be made to the container's
        /// service registrations. Ensures the application container is sealed only once
        /// during the application's lifecycle.
        /// </summary>
        internal void SealAppContainer()
        {
            // Exit case - the app container is sealed
            if (_isAppContainerSealed) return;
            
            ServiceContainer appContainer = AppContainer;
            appContainer.Seal();
            _isAppContainerSealed = true;
        }

        /// <summary>
        /// Ensures the initialization of the kernel by creating core application services, a service container,
        /// and a scene injector if they have not already been instantiated. Registers the simulation tick rate
        /// into the service container and outputs diagnostic information in editor or development builds.
        /// </summary>
        private void EnsureKernelInitialized()
        {
            // Exit case - already initialized
            if (_appServices != null) return;

            // Create app services
            AppServices appServices = DutyCalls.Adapters.Bootstrapping.AppServices.CreateFromCommandLine();
            ServiceContainer container = new ServiceContainer(null);
            SceneInjector injector = new SceneInjector();
            
            // Attempt to install services
            try
            {
                InstallServices(appServices, container);
            }
            catch
            {
                container.Dispose();
                throw;
            }
            
            _appServices = appServices;
            _appContainer = container;
            _injector = injector;
            _isAppContainerSealed = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("RunSeed: " + appServices.RunSeed + " TickRateHz: " + appServices.TickRate.TickRateHz);
#endif
        }

        /// <summary>
        /// Executes the process of initializing and installing application-specific services.
        /// This method creates a list of application service installers, allows for any necessary
        /// sorting by order, and installs them using the application service container. By completing
        /// this step, the method ensures that all required services are registered and prepared
        /// for application execution.
        /// </summary>
        private void InstallServices(AppServices appServices, ServiceContainer appContainer)
        {
            SceneLoader sceneLoader = _sceneLoader ?? throw new InvalidOperationException("SceneLoader not available.");
            
            // Create initial batch
            ServiceRegistrationBatch batch = new ServiceRegistrationBatch();
            batch.Register<IAppServices>(appServices);
            batch.Register<SimulationTickRate>(appServices.TickRate);
            batch.Register<ISceneLoader>(sceneLoader);
            
            List<IAppInstaller> installers = new List<IAppInstaller>(4);
            
            // TODO: Add hard-coded app installers here.
            // installers.Add(new PersistenceAppInstaller());
            
            SortByOrder(installers);
            
            AppInstallContext context = new AppInstallContext(appServices, sceneLoader, Application.persistentDataPath);
            for (int i = 0; i < installers.Count; i++)
            {
                installers[i].Install(in context, batch);
            }
            
            batch.ApplyTo(appContainer);
        }
        
        /// <summary>
        /// Sorts the provided list of installers in ascending order based on their order value.
        /// </summary>
        /// <typeparam name="TInstaller">The type of installer, which must implement <see cref="IOrderedInstaller"/>.</typeparam>
        /// <param name="installers">The list of installers to be sorted. The list will be modified in place.</param>
        private static void SortByOrder<TInstaller>(List<TInstaller> installers) where TInstaller : IOrderedInstaller
        {
            // Stable insertion sort (lists are expected to be small).
            for (int i = 1; i < installers.Count; i++)
            {
                TInstaller key = installers[i];
                int keyOrder = key.Order;
                int j = i - 1;
                
                while (j >= 0 && installers[j].Order > keyOrder)
                {
                    installers[j + 1] = installers[j];
                    j--;
                }

                installers[j + 1] = key;
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
            
            SealAppContainer();

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
