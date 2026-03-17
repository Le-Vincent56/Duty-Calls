#nullable enable
using System;
using System.Collections.Generic;
using DutyCalls.Adapters.Bootstrapping.Installers;
using DutyCalls.Adapters.DI;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Scenes
{
    /// <summary>
    /// Additive scene binder. Presentation-only by default.
    /// Missing active scope: dev throws; release disables overlay root.
    /// </summary>
    [DefaultExecutionOrder(-9990)]
    public sealed class AdditiveSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameObject? overlayRoot;
        
        private ServiceContainer? _sceneContainer;

        private void Awake()
        {
            Bootstrapper kernel = Bootstrapper.RequireKernel();

            // Exit case - no active scene container
            if (!kernel.TryGetActiveSceneContainer(out ServiceContainer? ownerContainer) || ownerContainer == null)
            {
                HandleMissingScope();
                return;
            }
            
            // Create child container
            ServiceContainer additiveContainer = kernel.AppContainer.CreateChild();
            _sceneContainer = additiveContainer;
            
            try
            {
                ServiceRegistrationBatch batch = new ServiceRegistrationBatch();
                
                // Collect installers
                List<IAdditiveSceneInstaller> installers = new List<IAdditiveSceneInstaller>(16);
                InstallerCollector collector = new InstallerCollector();
                collector.CollectFromScene(gameObject.scene, installers);
                collector.SortByOrder(installers);
                
                // Install all installers
                AdditiveSceneInstallContext context = new AdditiveSceneInstallContext(kernel.AppServices, ownerContainer);
                for (int i = 0; i < installers.Count; i++)
                {
                    installers[i].Install(in context, batch);
                }
                
                // Finalize the batch and seal the container
                batch.ApplyTo(additiveContainer);
                additiveContainer.Seal();
                
                kernel.Injector.InjectScene(gameObject.scene, additiveContainer);
            }
            catch
            {
                additiveContainer.Dispose();
                _sceneContainer = null;
                throw;
            }
        }
        
        private void OnDestroy()
        {
            ServiceContainer? container = _sceneContainer;
            
            // Exit case - no container
            if (container == null) return;
            
            container.Dispose();
            _sceneContainer = null;
        }

        /// <summary>
        /// Handles scenarios where no active scene scope is available during the additive scene bootstrapping process.
        /// In a development environment or build, an <see cref="InvalidOperationException"/> is thrown to alert the developer.
        /// In a release build, the overlay root associated with the scene is disabled to prevent improper functionality,
        /// and the component is disabled to avoid further processing.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no active scene scope in development or debug builds.
        /// </exception>
        private void HandleMissingScope()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new InvalidOperationException("AdditiveSceneBootstrapper requires an active scene container.");
#else
            Debug.LogError("Additive scene loaded without an active scene container. Disabling overlay root.");
            GameObject? root = overlayRoot;
            if (root != null)
            {
                root.SetActive(false);
            }
            enabled = false;
#endif
        }
    }
}
