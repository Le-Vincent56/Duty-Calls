#nullable enable
using System;

namespace DutyCalls.Adapters.Bootstrapping.Installers
{
    /// <summary>
    /// Represents context information required for installing owner-scene services,
    /// containing application services, a unique scene key, and a scene-specific seed value.
    /// </summary>
    public readonly struct OwnerSceneInstallContext
    {
        public IAppServices AppServices { get; }

        public string SceneKey { get; }

        public int SceneSeed { get; }

        public OwnerSceneInstallContext(IAppServices appServices, string sceneKey, int sceneSeed)
        {
            AppServices = appServices ?? throw new ArgumentNullException(nameof(appServices));
            SceneKey = sceneKey ?? throw new ArgumentNullException(nameof(sceneKey));
            SceneSeed = sceneSeed;
        }
    }
}