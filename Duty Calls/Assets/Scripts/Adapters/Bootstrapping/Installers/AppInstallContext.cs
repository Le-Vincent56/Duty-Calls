#nullable enable
using System;
using DutyCalls.Adapters.Scenes;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    public readonly struct AppInstallContext
    {
        public IAppServices AppServices { get; }
        public ISceneLoader SceneLoader { get; }

        public string PersistentDataPath { get; }

        public AppInstallContext(IAppServices appServices, ISceneLoader sceneLoader, string persistentDataPath)
        {
            AppServices = appServices ?? throw new ArgumentNullException(nameof(appServices));
            SceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            PersistentDataPath = persistentDataPath ?? throw new ArgumentNullException(nameof(persistentDataPath));
        }
    }
}