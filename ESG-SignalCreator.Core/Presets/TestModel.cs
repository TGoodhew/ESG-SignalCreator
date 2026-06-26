using System;
using EsgSignalCreator.Personalities;

namespace EsgSignalCreator.Presets
{
    /// <summary>
    /// A named "test-model" preset: a human-readable <see cref="Name"/> paired with a
    /// <see cref="Create"/> factory that returns a fully-configured
    /// <see cref="IWaveformPersonality"/> ready to <see cref="IWaveformPersonality.Calculate"/>.
    /// </summary>
    /// <remarks>
    /// Each invocation of <see cref="Create"/> should yield a fresh personality instance with its
    /// configuration already populated (built and applied via
    /// <see cref="IWaveformPersonality.LoadConfig"/>), so callers can generate immediately without
    /// any further setup.
    /// </remarks>
    public sealed class TestModel
    {
        /// <summary>Human-readable preset name, e.g. "CW (1 MHz offset)".</summary>
        public string Name { get; }

        /// <summary>Factory returning a fresh, fully-configured personality for this preset.</summary>
        public Func<IWaveformPersonality> Create { get; }

        public TestModel(string name, Func<IWaveformPersonality> create)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name is required.", nameof(name));
            Name = name;
            Create = create ?? throw new ArgumentNullException(nameof(create));
        }
    }
}
