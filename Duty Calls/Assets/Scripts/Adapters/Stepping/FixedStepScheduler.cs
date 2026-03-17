#nullable enable

using System;
using DutyCalls.Simulation.Core;

namespace DutyCalls.Adapters.Stepping
{
    /// <summary>
    /// Converts frame delta time into a bounded number of fixed simulation steps.
    /// Hitch policy: cap steps per frame and drop the backlog.
    /// </summary>
    public sealed class FixedStepScheduler
    {
        private readonly double _stepSeconds;
        private readonly int _maxStepsPerFrame;
        private double _accumulatorSeconds;

        public FixedStepScheduler(SimulationTickRate tickRate, int maxStepsPerFrame)
        {
            if (maxStepsPerFrame <= 0) throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame));

            _stepSeconds = 1.0d / tickRate.TickRateHz;
            _maxStepsPerFrame = maxStepsPerFrame;
            _accumulatorSeconds = 0.0d;
        }

        /// <summary>
        /// Adds a specified amount of time in seconds to the internal time accumulator
        /// and computes the number of fixed simulation steps that should be taken
        /// based on the accumulated time and the fixed step duration. Excessive accumulated
        /// time is capped by the maximum steps allowed per frame, and any backlog beyond
        /// this cap is discarded.
        /// </summary>
        /// <param name="deltaTimeSeconds">The amount of time, in seconds, to add to the accumulator. Must be a positive value.</param>
        /// <returns>The number of fixed simulation steps to execute, based on the added time and capped by the maximum steps allowed per frame.</returns>
        public int AddTime(float deltaTimeSeconds)
        {
            // Exit case - no time to add
            if (deltaTimeSeconds <= 0f) return 0;

            _accumulatorSeconds += deltaTimeSeconds;
            int steps = (int)(_accumulatorSeconds / _stepSeconds);
            
            // Exit case - no steps to take
            if (steps <= 0) return 0;

            if (steps > _maxStepsPerFrame)
            {
                _accumulatorSeconds = 0.0d;
                return _maxStepsPerFrame;
            }

            _accumulatorSeconds -= steps * _stepSeconds;
            return steps;
        }

        /// <summary>
        /// Resets the internal time accumulator to zero. This effectively clears any accumulated time
        /// and ensures that the scheduler starts fresh when adding new delta time.
        /// </summary>
        public void Reset() => _accumulatorSeconds = 0.0d;
    }
}