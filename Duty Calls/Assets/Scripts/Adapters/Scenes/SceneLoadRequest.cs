#nullable enable

using System;
using DutyCalls.Adapters.Scenes.Transitions;

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Represents a request to load one or more scenes, including optional pre- and post-transitions.
    /// </summary>
    public sealed class SceneLoadRequest
    {
        /// <summary>
        /// Provides a builder for constructing and configuring instances of <see cref="SceneLoadRequest"/>;
        /// enables a fluent API for setting scene groups, transitions, and custom loading ranges before generating the final request object.
        /// </summary>
        public sealed class Builder
        {
            private SceneGroup? _sceneGroup;
            private ISceneTransition? _preTransition;
            private ISceneTransition? _postTransition;
            private bool _markScenesAsPersistent;

            internal Builder()
            {
            }

            /// <summary>
            /// Specifies the scene group to be loaded as part of the <see cref="SceneLoadRequest"/>;
            /// use this method to define the primary collection of scenes to target during the build process.
            /// </summary>
            /// <param name="sceneGroup">The <see cref="SceneGroup"/> to be assigned to the request.</param>
            /// <returns>
            /// The current <see cref="SceneLoadRequest.Builder"/> instance, allowing for method chaining.
            /// </returns>
            public Builder WithSceneGroup(SceneGroup sceneGroup)
            {
                _sceneGroup = sceneGroup;
                return this;
            }

            /// <summary>
            /// Assigns a pre-loading transition to be executed before the scene loading begins;
            /// use this method to define a transition effect or animation that should play before
            /// initiating the scene group loading process.
            /// </summary>
            /// <param name="preTransition">The <see cref="ISceneTransition"/> object representing the transition to be executed before the loading begins.</param>
            /// <returns>
            /// The current <see cref="SceneLoadRequest.Builder"/> instance, enabling method chaining to configure additional properties or finalize the request.
            /// </returns>
            public Builder WithPreTransition(ISceneTransition preTransition)
            {
                _preTransition = preTransition;
                return this;
            }

            /// <summary>
            /// Specifies the post-transition operation to be executed after the scene load completes;
            /// use this method to define a supplemental transition effect following the scene loading process.
            /// </summary>
            /// <param name="postTransition">The <see cref="ISceneTransition"/> instance to be applied after the scene has been loaded.</param>
            /// <returns>
            /// The current <see cref="SceneLoadRequest.Builder"/> instance, enabling method chaining.
            /// </returns>
            public Builder WithPostTransition(ISceneTransition postTransition)
            {
                _postTransition = postTransition;
                return this;
            }

            /// <summary>
            /// Configures the builder with transitions for ths specified context.
            /// </summary>
            /// <param name="transitionService">The transition service to configure pre-/post-transitions.</param>
            /// <returns>
            /// The current <see cref="SceneLoadRequest.Builder"/> instance for method chaining.
            /// </returns>
            public Builder WithTransition(ITransitionService transitionService)
            {
                transitionService.SetupTransition(this);
                return this;
            }

            /// <summary>
            /// Constructs a new <see cref="SceneLoadRequest"/> instance using the properties set on the builder.
            /// </summary>
            /// <returns>
            /// A fully configured <see cref="SceneLoadRequest"/> instance containing the specified scene group, transitions,
            /// and loading ranges, if provided.
            /// </returns>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the necessary properties, such as the scene group, are not configured before invoking this method.
            /// </exception>
            public SceneLoadRequest Build()
            {
                // Exit case - the scene group is null
                if (!_sceneGroup)
                {
                    throw new InvalidOperationException("SceneGroup is required. Call WithSceneGroup() before Build()");
                }

                // Build the request
                return new SceneLoadRequest
                {
                    SceneGroup = _sceneGroup,
                    PreTransition = _preTransition,
                    PostTransition = _postTransition,
                    MarkScenesAsPersistent = _markScenesAsPersistent,
                };
            }
            
            /// <summary>
            /// Marks all scenes loaded by this request as persistent, meaning the loader will not unload them
            /// during subsequent scene group loads. Use for "Systems"/app-lifetime scenes.
            /// </summary>
            /// <returns>The current builder instance for method chaining.</returns>
            public Builder MarkScenesAsPersistent()
            {
                _markScenesAsPersistent = true;
                return this;
            }
        }

        public SceneGroup SceneGroup { get; private set; } = null!;
        public ISceneTransition PreTransition { get; private set; }
        public ISceneTransition PostTransition { get; private set; }
        public bool MarkScenesAsPersistent { get; private set; }

        private SceneLoadRequest() { }

        /// <summary>
        /// Creates a new instance of the <see cref="SceneLoadRequest.Builder"/> class for constructing and configuring a scene load request.
        /// </summary>
        /// <returns>
        /// A new <see cref="SceneLoadRequest.Builder"/> instance, allowing customization of scene groups, transitions, and loading ranges.
        /// </returns>
        public static Builder Create() => new Builder();
    }
}