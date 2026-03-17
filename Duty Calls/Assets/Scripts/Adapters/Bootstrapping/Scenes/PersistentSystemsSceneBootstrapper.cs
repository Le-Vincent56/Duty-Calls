#nullable enable
using System;
using System.Collections.Generic;
using DutyCalls.Adapters.Bootstrapping.Installers;
using DutyCalls.Adapters.DI;
using UnityEngine;

namespace DutyCalls.Adapters.Bootstrapping.Scenes
{
    /// <summary>
    /// Composition root for Persistent Systems scenes.
    /// Installs global Unity-backed services into the app container, then injects this scene.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PersistentSystemsSceneBootstrapper : MonoBehaviour
    {
        private readonly List<IPersistentServicesInstaller> _installers = new List<IPersistentServicesInstaller>(16);
        private readonly InstallerCollector _collector = new InstallerCollector();

        private void Awake()
        {
            Bootstrapper kernel = Bootstrapper.RequireKernel();
            
            IAppServices appServices = kernel.AppServices;
            ServiceContainer appContainer = kernel.AppContainer;
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (appContainer.IsSealed)
            {
                throw new InvalidOperationException("Persistent systems cannot register after the app container is sealed.");
            }
#endif
            
            ServiceRegistrationBatch batch = new ServiceRegistrationBatch();
            
            // Collect installers
            _collector.CollectFromScene(gameObject.scene, _installers);
            _collector.SortByOrder(_installers);
            
            // Install all installers
            PersistentInstallContext context = new PersistentInstallContext(appServices);
            for (int i = 0; i < _installers.Count; i++)
            {
                _installers[i].Install(in context, batch);
            }
            
            // Finalize the batch
            batch.ApplyTo(appContainer);
            
            kernel.Injector.InjectScene(gameObject.scene, appContainer);
        }
    }
}