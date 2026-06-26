using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities
{
    /// <summary>
    /// A signal personality: the pluggable part that turns a high-level configuration into a
    /// baseband <see cref="WaveformModel"/>. Implementations are responsible <b>only</b> for
    /// producing normalized I/Q + sample rate; scaling, encoding, transport, and verification
    /// (crest factor / CCDF / spectrum) are all handled centrally.
    /// </summary>
    /// <remarks>
    /// This contract is deliberately UI-free so the Core assembly carries no WinForms dependency.
    /// The matching configuration panel is supplied separately by the UI layer (see the UX brief's
    /// <c>ISignalSourcePanel</c>), keyed off <see cref="Id"/>.
    /// </remarks>
    public interface IWaveformPersonality
    {
        /// <summary>Stable identifier, e.g. "multitone". Used to pair the personality with its UI panel.</summary>
        string Id { get; }

        /// <summary>Human-readable name for menus and the project tree.</summary>
        string DisplayName { get; }

        /// <summary>Return the current settings as a serializable object (for project save).</summary>
        object GetConfig();

        /// <summary>Restore settings previously produced by <see cref="GetConfig"/>.</summary>
        void LoadConfig(object cfg);

        /// <summary>Generate the waveform. Long runs should report 0–100 via <paramref name="progress"/>.</summary>
        WaveformModel Calculate(IProgress<int> progress);

        /// <summary>E4438C option required to <i>play</i> the result, or null when generic ARB suffices.</summary>
        int? RequiredOption { get; }
    }
}
