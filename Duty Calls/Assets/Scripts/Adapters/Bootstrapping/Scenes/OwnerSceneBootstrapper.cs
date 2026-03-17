#nullable enable
using System;
using System.Collections.Generic;
using DutyCalls.Adapters.Bootstrapping.Installers;
using DutyCalls.Adapters.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DutyCalls.Adapters.Bootstrapping.Scenes
{
    /// <summary>
    /// Owner-scene composition root. Must exist in the single SceneType.Gameplay scene of a SceneGroup.
    /// Injects scene-loaded objects before their Awake via execution order.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class OwnerSceneBootstrapper : MonoBehaviour
    {
        private ServiceContainer? _sceneContainer;

        private void Awake()
        {
            Bootstrapper kernel = Bootstrapper.RequireKernel();
            IAppServices appServices = kernel.AppServices;
            
            Scene scene = gameObject.scene;
            string sceneKey = GetSceneKey(scene);
            int sceneSeed = appServices.GetSceneSeed(sceneKey);
            
            ServiceContainer container = kernel.AppContainer.CreateChild();
            _sceneContainer = container;
            
            try
            {
                ServiceRegistrationBatch batch = new ServiceRegistrationBatch();
                
                // Collect installers
                List<IOwnerSceneInstaller> installers = new List<IOwnerSceneInstaller>(16);
                InstallerCollector collector = new InstallerCollector();
                collector.CollectFromScene(scene, installers);
                collector.SortByOrder(installers);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (installers.Count == 0)
                    throw new InvalidOperationException("OwnerSceneBootstrapper found no IOwnerSceneInstaller in the owner scene.");
#endif
                // Install all installers
                OwnerSceneInstallContext context = new OwnerSceneInstallContext(appServices, sceneKey, sceneSeed);
                for (int i = 0; i < installers.Count; i++)
                {
                    installers[i].Install(in context, batch);
                }
                
                // Finalize the batch and seal the container
                batch.ApplyTo(container);
                container.Seal();
                
                kernel.SetActiveSceneContainer(container);
                kernel.Injector.InjectScene(scene, container);
            }
            catch
            {
                kernel.ClearActiveSceneContainer(container);
                _sceneContainer = null;
                throw;
            }
        }

        private void OnDestroy()
        {
            ServiceContainer? container = _sceneContainer;
            
            // Exit case - no scene container set
            if (container == null) return;

            // Exit case - could not get the kernel
            if (Bootstrapper.TryGetKernel(out Bootstrapper? kernel) && kernel) kernel.ClearActiveSceneContainer(container);

            container.Dispose();
            _sceneContainer = null;
        }

        /// <summary>
        /// Retrieves the unique key for a given scene. If the scene's path is not empty, the path is returned as the key.
        /// Otherwise, the scene's name is returned as the fallback key.
        /// </summary>
        /// <param name="scene">The scene for which the unique key is to be generated.</param>
        /// <returns>A string representing the unique key of the provided scene.</returns>
        private static string GetSceneKey(Scene scene)
        {
            string path = scene.path;
            return !string.IsNullOrEmpty(path) ? path : scene.name;
        }
    }
}
