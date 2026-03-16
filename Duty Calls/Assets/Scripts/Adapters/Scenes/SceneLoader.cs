#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DutyCalls.Adapters.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace DutyCalls.Adapters.Scenes
{
    /// <summary>
    /// Handles the process of loading scenes asynchronously in a Unity environment;
    /// Implements the <see cref="ISceneLoader"/> interface for scene management operations.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour, ISceneLoader
    {
        /// <summary>
        /// Represents a handle to a loaded scene, encapsulating information about the scene's load operation,
        /// its persistence, and its associated type. Used internally by <see cref="SceneLoader"/> for scene management.
        /// </summary>
        private sealed class LoadedSceneHandle
        {
            public AsyncOperationHandle<SceneInstance> Handle { get; }
            public bool IsPersistent { get; }
            public SceneType SceneType { get; }

            public LoadedSceneHandle(AsyncOperationHandle<SceneInstance> handle, bool isPersistent, SceneType sceneType)
            {
                Handle = handle;
                IsPersistent = isPersistent;
                SceneType = sceneType;
            }
        }

        private readonly Dictionary<SceneGroup, List<LoadedSceneHandle>> _loadedGroups = new Dictionary<SceneGroup, List<LoadedSceneHandle>>();

        private readonly Observable<LoadProgress> _progress = new Observable<LoadProgress>();
        private bool _isLoading;

        public Utilities.IObservable<LoadProgress> Progress => _progress;
        
        private void OnDestroy()
        {
            foreach (KeyValuePair<SceneGroup, List<LoadedSceneHandle>> group in _loadedGroups)
            {
                for (int i = 0; i < group.Value.Count; i++)
                {
                    AsyncOperationHandle<SceneInstance> handle = group.Value[i].Handle;

                    // Skip if the handle is invalid
                    if (!handle.IsValid()) continue;

                    Addressables.UnloadSceneAsync(handle);
                }
            }

            // Clear the cache
            _loadedGroups.Clear();
        }

        /// <summary>
        /// Asynchronously loads scenes based on the specified scene load request.
        /// </summary>
        /// <param name="request">The scene load request containing details about the scenes to be loaded.</param>
        /// <returns>A task representing the asynchronous operation, which resolves to a <see cref="SceneLoadResult"/> containing the outcome of the load operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a scene load operation is already in progress.</exception>
        public async Task<SceneLoadResult> LoadAsync(SceneLoadRequest request)
        {
            // Exit case - the Scene Loader is already loading scenes
            if (_isLoading)
            {
                throw new InvalidOperationException(
                    "Scene load is already in progress. Wait for the current load to complete before starting a new one"
                );
            }

            // Set loading to true
            _isLoading = true;

            // Reset progress observable for new load
            _progress.Reset();

            // Track loading times using Stopwatch
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Initialize the result
            SceneLoadResult result = new SceneLoadResult
            {
                LoadedGroup = request.SceneGroup,
                LoadedScenes = new List<string>(),
                SkippedScenes = new List<string>(),
                Errors = new List<Exception>(),
                RangeTimings = new Dictionary<string, float>()
            };

            try
            {
                await ExecutePreTransitionAsync(request);
                await ExecuteUnloadPhaseAsync(result);
                await ExecuteLoadAndActivatePhaseAsync(request, result);
                await ExecutePostTransitionAsync(request);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Scene load failed: {ex}");
                result.Errors.Add(ex);
                throw;
            }
            finally
            {
                // Store the total load time
                stopwatch.Stop();
                result.TotalLoadTime = (float)stopwatch.Elapsed.TotalSeconds;

                // Stop loading
                _isLoading = false;

                // Clean up progress and subscribers
                _progress.Complete();
            }

            return result;
        }

        /// <summary>
        /// Executes the pre-transition phase of the scene loading process asynchronously if a pre-transition is defined in the request.
        /// </summary>
        /// <param name="request">The scene load request containing details about the scenes to be loaded and the pre-transition to be executed.</param>
        /// <returns>A task representing the asynchronous operation for executing the pre-transition phase.</returns>
        private async Task ExecutePreTransitionAsync(SceneLoadRequest request)
        {
            // Exit case - no pre-transition is defined
            if (request.PreTransition == null) return;

            await request.PreTransition.PlayAsync(_progress);
        }

        /// <summary>
        /// Executes the unloading phase of the scene transition process, releasing previously loaded scenes and updating progress.
        /// </summary>
        /// <param name="result">An instance of <see cref="SceneLoadResult"/> that captures the results and timings of the unload process.</param>
        /// <returns>A task representing the asynchronous unload operation.</returns>
        private async Task ExecuteUnloadPhaseAsync(SceneLoadResult result)
        {
            Stopwatch rangeStopwatch = Stopwatch.StartNew();
            List<SceneGroup> groupsToUnload = new List<SceneGroup>(_loadedGroups.Keys);

            for (int i = 0; i < groupsToUnload.Count; i++)
            {
                SceneGroup group = groupsToUnload[i];

                // Skip if the handles cannot be retrieved
                if (!_loadedGroups.TryGetValue(group, out List<LoadedSceneHandle> handles)) continue;

                List<LoadedSceneHandle> persistent = new List<LoadedSceneHandle>();
                for (int j = 0; j < handles.Count; j++)
                {
                    LoadedSceneHandle record = handles[j];

                    // Track if the handle is persistent
                    if (record.IsPersistent)
                    {
                        persistent.Add(record);
                        continue;
                    }

                    AsyncOperationHandle<SceneInstance> handle = record.Handle;

                    // Skip if the handle is invalid
                    if (!handle.IsValid()) continue;

                    try
                    {
                        await Addressables.UnloadSceneAsync(handle).Task;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("Failed to unload scene: " + ex);
                        result.Errors.Add(ex);
                    }
                }

                if (persistent.Count > 0) _loadedGroups[group] = persistent;
                else _loadedGroups.Remove(group);
            }

            _progress.OnNext(new LoadProgress
            {
                NormalizedProgress = 0.25f,
                CurrentRange = "Unload",
                RangeProgress = 1.0f,
                CurrentOperation = "Unloaded Previous Scenes"
            });

            rangeStopwatch.Stop();
            result.RangeTimings["Unload"] = (float)rangeStopwatch.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Asynchronously loads and activates the scenes specified in the provided scene load request, updating progress throughout the operation.
        /// </summary>
        /// <param name="request">The parameters of the scene loading operation, including the scenes to load and load behavior settings.</param>
        /// <param name="result">The result container used to store the state and outcomes of the scene loading operation.</param>
        /// <returns>A task that represents the asynchronous operation of loading and activating the specified scenes.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the operation fails due to invalid or conflicting scene loading conditions.</exception>
        private async Task ExecuteLoadAndActivatePhaseAsync(SceneLoadRequest request, SceneLoadResult result)
        {
            SceneGroup.SceneEntry[] entries = request.SceneGroup.Scenes;
            int sceneCount = entries.Length;

            // Exit case - no scenes to load
            if (sceneCount <= 0)
            {
                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = 0.90f,
                    CurrentRange = "Load",
                    RangeProgress = 1.0f,
                    CurrentOperation = "No Scenes To Load"
                });

                return;
            }

            bool requireOwner = !request.MarkScenesAsPersistent;
            int ownerIndex = requireOwner ? FindOwnerSceneIndex(entries) : -1;

            // Store loading handles
            AsyncOperationHandle<SceneInstance>[] handlesByIndex = new AsyncOperationHandle<SceneInstance>[sceneCount];
            for (int i = 0; i < sceneCount; i++)
            {
                SceneGroup.SceneEntry entry = entries[i];

                // Exit case - the entry has a null reference
                if (entry.SceneReference == null)
                {
                    throw new InvalidOperationException(
                        $"SceneEntry.SceneReference is null at index {i} in SceneGroup '{request.SceneGroup.name}'"
                    );
                }

                handlesByIndex[i] = Addressables.LoadSceneAsync(entry.SceneReference, LoadSceneMode.Additive, false);
            }

            // Calculate loading progress
            while (!AllHandlesDone(handlesByIndex))
            {
                float average = ComputeAverageProgress(handlesByIndex);
                float normalized = 0.25f + (average * 0.50f);

                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = normalized,
                    CurrentRange = "Load",
                    RangeProgress = average,
                    CurrentOperation = "Loading " + sceneCount + " Scene(s)"
                });

                await Task.Yield();
            }

            // Handle all loading operations and record them
            SceneInstance[] instancesByIndex = new SceneInstance[sceneCount];
            List<LoadedSceneHandle> records = new List<LoadedSceneHandle>(sceneCount);
            for (int i = 0; i < sceneCount; i++)
            {
                AsyncOperationHandle<SceneInstance> handle = handlesByIndex[i];

                await handle.Task;

                instancesByIndex[i] = handle.Result;
                records.Add(new LoadedSceneHandle(handle, request.MarkScenesAsPersistent, entries[i].SceneType));
            }

            _loadedGroups[request.SceneGroup] = records;
            List<int> activationOrder = BuildActivationOrder(entries, ownerIndex, requireOwner);
            float activationStart = 0.75f;
            float activationTotal = 0.20f;
            float perScene = activationTotal / activationOrder.Count;

            for (int i = 0; i < activationOrder.Count; i++)
            {
                int index = activationOrder[i];

                _progress.OnNext(new LoadProgress
                {
                    NormalizedProgress = activationStart + (i * perScene),
                    CurrentRange = "Activate",
                    RangeProgress = i / (float)activationOrder.Count,
                    CurrentOperation = $"Activating {sceneCount} Scene(s)"
                });

                AsyncOperation op = instancesByIndex[index].ActivateAsync();

                await AwaitAsyncOperationAsync(op);

                result.LoadedScenes.Add(instancesByIndex[index].Scene.name);
            }

            // Activate the owner scene if required
            if (requireOwner)
            {
                Scene ownerScene = instancesByIndex[ownerIndex].Scene;

                // Exit case - owner scene is invalid or not loaded
                if (!ownerScene.IsValid() || !ownerScene.isLoaded)
                {
                    throw new InvalidOperationException("Owner (SceneType.Gameplay) scene failed to activate.");
                }

                SceneManager.SetActiveScene(ownerScene);
            }

            _progress.OnNext(new LoadProgress
            {
                NormalizedProgress = 0.95f,
                CurrentRange = "Activate",
                RangeProgress = 1.0f,
                CurrentOperation = "Scenes Activated"
            });

            _progress.OnNext(new LoadProgress
            {
                NormalizedProgress = 1.0f,
                CurrentRange = "Complete",
                RangeProgress = 1.0f,
                CurrentOperation = "Scene Load Complete"
            });
        }

        /// <summary>
        /// Executes the post-transition phase of the scene loading process if defined in the provided request
        /// </summary>
        /// <param name="request">The scene load request containing the details of the post-transition phase to execute</param>
        /// <returns>A task that represents the asynchronous operation of executing the post-transition phase</returns>
        private async Task ExecutePostTransitionAsync(SceneLoadRequest request)
        {
            // Exit case - no post-transition is defined
            if (request.PostTransition == null) return;

            await request.PostTransition.PlayAsync(_progress);
        }

        /// <summary>
        /// Awaits the completion of the specified asynchronous Unity operation.
        /// </summary>
        /// <param name="operation">The asynchronous operation to be awaited. If null, the method immediately returns.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task AwaitAsyncOperationAsync(AsyncOperation? operation)
        {
            // Exit case - no operation is provided
            if (operation == null) return;

            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Determines whether all asynchronous operation handles have completed their tasks.
        /// </summary>
        /// <param name="handles">An array of asynchronous operation handles representing scene loading processes.</param>
        /// <returns>True if all handles have completed; otherwise, false.</returns>
        private static bool AllHandlesDone(AsyncOperationHandle<SceneInstance>[] handles)
        {
            for (int i = 0; i < handles.Length; i++)
            {
                // Exit case - the handle operation has not finished
                if (!handles[i].IsDone) return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the average progress of multiple asynchronous scene loading operations.
        /// </summary>
        /// <param name="handles">The array of <see cref="AsyncOperationHandle{SceneInstance}"/> objects representing the loading operations.</param>
        /// <returns>A float value indicating the average progress of the provided loading operations, with a range of 0.0 to 1.0.</returns>
        private static float ComputeAverageProgress(AsyncOperationHandle<SceneInstance>[] handles)
        {
            // Exit case - no handles provided
            if (handles.Length == 0) return 1f;

            float total = 0f;
            for (int i = 0; i < handles.Length; i++)
            {
                total += handles[i].PercentComplete;
            }

            return total / handles.Length;
        }

        /// <summary>
        /// Determines the index of the owner scene within the array of scene entries.
        /// </summary>
        /// <param name="entries">The array of scene entries to evaluate.</param>
        /// <returns>The index of the owner scene, which is the scene marked as <see cref="SceneType.Gameplay"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there is not exactly one scene marked as <see cref="SceneType.Gameplay"/> in the provided entries.
        /// </exception>
        private static int FindOwnerSceneIndex(SceneGroup.SceneEntry[] entries)
        {
            int ownerIndex = -1;
            int ownerCount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                // Exit case - the entry is not a gameplay scene
                if (entries[i].SceneType != SceneType.Gameplay) continue;

                ownerIndex = i;
                ownerCount++;
            }

            // Exit case - more than one owner scene
            if (ownerCount != 1)
            {
                throw new InvalidOperationException(
                    $"SceneGroup must contain exactly one SceneType.Gameplay entry (owner). Found: ownerCount"
                );
            }

            return ownerIndex;
        }

        /// <summary>
        /// Constructs an activation order for scenes based on the specified entries, prioritizing certain scene types and optionally including an owner scene.
        /// </summary>
        /// <param name="entries">The array of scene entries from which the activation order is determined.</param>
        /// <param name="ownerIndex">The index of the owner scene in the entries array.</param>
        /// <param name="includeOwner">A boolean indicating whether the owner scene should be included in the activation order.</param>
        /// <returns>A list of integers representing the indices of scenes in the specified activation order.</returns>
        private static List<int> BuildActivationOrder(
            SceneGroup.SceneEntry[] entries,
            int ownerIndex,
            bool includeOwner
        )
        {
            List<int> order = new List<int>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                // Skip if the entry is not a system scene
                if (entries[i].SceneType != SceneType.Systems) continue;
                
                order.Add(i);
            }

            if (includeOwner) order.Add(ownerIndex);

            for (int i = 0; i < entries.Length; i++)
            {
                // Skip if including the owner and the entry is the owner scene (already recorded)
                if (includeOwner && i == ownerIndex) continue;

                // Skip if the entry is a system scene (already recorded)
                if (entries[i].SceneType == SceneType.Systems) continue;

                order.Add(i);
            }

            return order;
        }
    }
}