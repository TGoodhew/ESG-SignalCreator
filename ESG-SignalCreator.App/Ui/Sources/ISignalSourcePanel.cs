using System;
using System.Windows.Forms;
using EsgSignalCreator.Personalities;

namespace EsgSignalCreator.Ui.Sources
{
    /// <summary>
    /// UI contract for a signal personality's configuration panel — the WinForms-side counterpart to
    /// <see cref="IWaveformPersonality"/>. Kept out of Core so the Core library stays UI-free
    /// (UX brief §12).
    /// </summary>
    public interface ISignalSourcePanel
    {
        /// <summary>Id of the personality this panel configures (matches <see cref="IWaveformPersonality.Id"/>).</summary>
        string PersonalityId { get; }

        /// <summary>Build a personality configured from the panel's current values, ready to Calculate.</summary>
        IWaveformPersonality BuildPersonality();

        /// <summary>The current configuration object (for project save).</summary>
        object GetConfig();

        /// <summary>Load a previously-saved configuration object.</summary>
        void LoadConfig(object cfg);

        /// <summary>Raised when the user clicks Calculate.</summary>
        event EventHandler CalculateRequested;

        /// <summary>This panel as a WinForms control for hosting.</summary>
        Control AsControl();
    }
}
