#nullable enable
using System;
using DutyCalls.Simulation.Core;

namespace DutyCalls.Adapters.Bootstrapping
{
    /// <summary>
    /// Provides services required during the application runtime, including configuration settings
    /// such as the simulation tick rate and seed values.
    /// </summary>
    public sealed class AppServices : IAppServices
    {
        private const int DefaultTickRateHz = 60;
        private readonly int _runSeed;
        private readonly SimulationTickRate _tickRate;

        /// <summary>
        /// Gets the seed value for initializing simulations or other application-lifetime operations.
        /// </summary>
        public int RunSeed => _runSeed;
        
        /// <summary>
        /// Gets the fixed simulation tick rate, expressed in hertz (Hz), used to control the pacing of simulation updates.
        /// </summary>
        public SimulationTickRate TickRate => _tickRate;
        
        public AppServices(int runSeed, SimulationTickRate tickRate)
        {
            _runSeed = runSeed;
            _tickRate = tickRate;
        }

        /// <summary>
        /// Retrieves the seed associated with the specified scene key.
        /// </summary>
        public int GetSceneSeed(string sceneKey)
        {
            // Exit case - null key
            if (sceneKey == null) throw new ArgumentNullException(nameof(sceneKey));

            int sceneHash = ComputeFnv1a32(sceneKey);
            uint combined = unchecked((uint)_runSeed);
            combined ^= unchecked((uint)sceneHash) + 0x9e3779b9u + (combined << 6) + (combined >> 2);
            
            return unchecked((int)combined);
        }

        /// <summary>
        /// Creates an instance of the <see cref="AppServices"/> class based on command-line arguments.
        /// </summary>
        /// <returns>A new instance of <see cref="AppServices"/> initialized with the parsed command-line data.</returns>
        public static AppServices CreateFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            
            // Exit case - could not parse a runseed from the command line
            if (!TryParseIntFlag(args, "-runSeed", out int runSeed))
                runSeed = unchecked((int)DateTime.UtcNow.Ticks);
            
            // Ext case - could not parse the tick rate from the command line
            if (!TryParseIntFlag(args, "-tickRateHz", out int tickRateHz) && !TryParseIntFlag(args, "-tickRate", out tickRateHz))
                tickRateHz = DefaultTickRateHz;

            // Set the tick rate
            SimulationTickRate tickRate = new SimulationTickRate(tickRateHz);
            
            return new AppServices(runSeed, tickRate);
        }

        /// <summary>
        /// Attempts to parse an integer value from the command-line arguments associated with the specified flag.
        /// </summary>
        /// <param name="args">The array of command-line arguments.</param>
        /// <param name="flag">The flag for which the associated integer value is to be parsed.</param>
        /// <param name="value">When this method returns, contains the parsed integer value if the operation succeeded, or 0 if it failed.</param>
        /// <returns>true if the flag is found and its associated value is successfully parsed; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown if the flag is found but the value is missing or invalid.</exception>
        private static bool TryParseIntFlag(string[] args, string flag, out int value)
        {
            value = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                // Skip if the flag and the arg mismatch
                if (!string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                    continue;

                int valueIndex = i + 1;
                
                // Exit case - index out of bounds
                if (valueIndex >= args.Length)
                    throw new ArgumentException("Missing value for command line flag: " + flag);

                string rawValue = args[valueIndex];
                
                // Exit case - could not parse the raw value
                if (!int.TryParse(rawValue, out value))
                    throw new ArgumentException("Invalid value for command line flag: " + flag + " " + rawValue);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Computes the FNV-1a 32-bit hash for the given text.
        /// </summary>
        /// <param name="text">The input text to compute the hash for.</param>
        /// <returns>The 32-bit integer hash value computed using the FNV-1a algorithm.</returns>
        private static int ComputeFnv1a32(string text)
        {
            const uint OffsetBasis = 2166136261u;
            const uint Prime = 16777619u;
            uint hash = OffsetBasis;
            
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= Prime;
            }

            return unchecked((int)hash);
        }
    }
}