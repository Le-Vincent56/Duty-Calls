#nullable enable

using System.Threading.Tasks;
using DutyCalls.Adapters.Utilities;

namespace DutyCalls.Adapters.Scenes.Transitions
{
    /// <summary>
    /// Represents a contract for implementing custom scene transitions.
    /// </summary>
    /// <remarks>
    /// This interface defines the structure required for creating asynchronous
    /// scene transition mechanisms; implementing this interface allows the
    /// creation of reusable and customizable transitions, which can be
    /// integrated into a scene loading workflow.
    /// </remarks>
    public interface ISceneTransition
    {
        /// <summary>
        /// Represents a contract for implementing custom scene transitions;
        /// receives an observable of load progress to allow transitions to
        /// react dynamically to the loading pipeline.
        /// </summary>
        /// <param name="progress">The load progress to observe.</param>
        /// <returns>A task representing the state of completion of the transition.</returns>
        Task PlayAsync(IObservable<LoadProgress> progress);
    }
}