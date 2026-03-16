#nullable enable

namespace DutyCalls.Adapters.Scenes.Transitions
{
    /// <summary>
    /// Provides transition configurations for scene changes;
    /// resolves the appropriate pre-/post-effects and loading screen
    /// profile based on a TransitionContext.
    /// </summary>
    public interface ITransitionService
    {
        /// <summary>
        /// Configures the builder with pre-/post-transitions for the given context.
        /// </summary>
        /// <param name="builder">The <see cref="SceneLoadRequest.Builder"/> to configure.</param>
        void SetupTransition(SceneLoadRequest.Builder builder);
    }
}