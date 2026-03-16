#nullable enable

using System;
using System.Collections.Generic;

namespace DutyCalls.Adapters.Utilities
{
    /// <summary>
    /// Defines a provider for push-based notifications, allowing subscribers to
    /// observe and react to a stream of data or events.
    /// </summary>
    /// <typeparam name="T">The type of data being observed.</typeparam>
    public interface IObservable<out T>
    {
        /// <summary>
        /// Subscribes a callback to be invoked whenever the observable emits a new item or data.
        /// </summary>
        /// <param name="onNext">The action to execute when a new item is published by the observable.</param>
        /// <returns>An <see cref="IDisposable"/> object to manage the subscription and allow for unsubscription.</returns>
        IDisposable Subscribe(Action<T> onNext);
    }

    /// <summary>
    /// Represents an observable object that allows subscribers to receive notifications
    /// about asynchronous data streams or events; this class provides support for
    /// the observer design pattern by allowing subscribers to register, receive
    /// notifications, and dispose of their subscriptions.
    /// </summary>
    /// <typeparam name="T">The type of data being observed.</typeparam>
    public sealed class Observable<T> : IObservable<T>
    {
        /// <summary>
        /// Represents an active subscription to an observable sequence. This class allows
        /// managing the lifecycle of the subscription, including disposing of it to stop
        /// receiving notifications and to release associated resources.
        /// </summary>
        private sealed class Subscription<TValue> : IDisposable
        {
            private Observable<TValue>? _observable;
            private Action<TValue>? _callback;

            public Subscription(Observable<TValue> observable, Action<TValue> callback)
            {
                _observable = observable;
                _callback = callback;
            }

            /// <summary>
            /// Disposes the subscription by unregistering the associated callback from the observable
            /// and releasing references to avoid memory leaks
            /// </summary>
            public void Dispose()
            {
                // Exit case - neither the observable nor the callback are valid
                if (_observable == null || _callback == null) return;

                // Remove the callback and clean up
                _observable._subscribers.Remove(_callback);
                _observable = null;
                _callback = null;
            }
        }

        /// <summary>
        /// Represents a disposable object that performs no operation
        /// when disposed, typically used as a placeholder or default implementation.
        /// </summary>
        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private readonly List<Action<T>> _subscribers = new List<Action<T>>();
        private bool _isCompleted;

        /// <summary>
        /// Notifies all subscribers of a new value by invoking their associated callbacks.
        /// </summary>
        /// <param name="value">The new value to be delivered to each subscriber.</param>
        public void OnNext(T value)
        {
            // Exit case - if completed already
            if (_isCompleted) return;

            // Notify subscribers
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                _subscribers[i]?.Invoke(value);
            }
        }

        /// <summary>
        /// Completes the observable by marking it as finished and clearing all registered subscribers,
        /// ensuring no further notifications are sent to observers.
        /// </summary>
        public void Complete()
        {
            _isCompleted = true;
            _subscribers.Clear();
        }

        /// <summary>
        /// Resets the observable to allow reuse after completion.
        /// </summary>
        public void Reset() => _isCompleted = false;

        /// <summary>
        /// Subscribes a callback to the observable, allowing the subscriber to receive notifications
        /// whenever new data is published to the observable.
        /// </summary>
        /// <param name="onNext">The action to invoke with the published data when the observable is updated.</param>
        /// <returns>An <see cref="IDisposable"/> object that can be used to unsubscribe and release resources.</returns>
        public IDisposable Subscribe(Action<T> onNext)
        {
            // Exit case - if completed already
            if (_isCompleted) return new EmptyDisposable();

            _subscribers.Add(onNext);
            return new Subscription<T>(this, onNext);
        }
    }
}