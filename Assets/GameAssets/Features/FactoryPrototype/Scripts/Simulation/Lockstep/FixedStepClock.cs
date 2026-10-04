using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Converts frame time into fixed simulation steps. Pacing only: never part of the simulation state.
    /// </summary>
    public sealed class FixedStepClock
    {
        private readonly double _stepSeconds;
        private readonly int _maxStepsPerUpdate;
        private double _accumulator;

        public FixedStepClock(int ticksPerSecond, int maxStepsPerUpdate)
        {
            _stepSeconds = 1.0 / ticksPerSecond;
            _maxStepsPerUpdate = Math.Max(1, maxStepsPerUpdate);
        }

        public double StepSeconds => _stepSeconds;

        /// <summary>Elapsed fraction of the next step, for interpolation. 0-1.</summary>
        public float Alpha => (float)Math.Min(1.0, _accumulator / _stepSeconds);

        /// <summary>Adds frame time and returns the steps due. Time beyond maxStepsPerUpdate is dropped.</summary>
        public int Accumulate(double deltaSeconds)
        {
            if (deltaSeconds > 0)
                _accumulator += deltaSeconds;

            int steps = (int)(_accumulator / _stepSeconds);
            if (steps > _maxStepsPerUpdate)
            {
                steps = _maxStepsPerUpdate;
                _accumulator = 0;
            }
            else
            {
                _accumulator -= steps * _stepSeconds;
            }
            return steps;
        }

        public void Reset() => _accumulator = 0;

        /// <summary>Keeps less than one step accumulated, so a stall does not turn into a burst later.</summary>
        public void Clamp() => _accumulator = Math.Min(_accumulator, _stepSeconds * 0.999);
    }
}
