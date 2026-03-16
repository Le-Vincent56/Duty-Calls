#nullable enable

using System.Threading.Tasks;
using DutyCalls.Adapters.Utilities;

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Provides functionality for loading scenes and tracking the progress of the loading operation
    /// </summary>
    public interface ISceneLoader
    {
        IObservable<LoadProgress> Progress { get; }

        /// <summary>
        /// Asynchronously loads a scene or a group of scenes based on the specified request
        /// </summary>
        /// <param name="request">The request object containing details about the scenes to be loaded, transitions, and custom ranges</param>
        /// <returns>A task that represents the asynchronous operation and returns a <see cref="SceneLoadResult"/> containing the result of the scene load operation</returns>
        Task<SceneLoadResult> LoadAsync(SceneLoadRequest request);
    }
}