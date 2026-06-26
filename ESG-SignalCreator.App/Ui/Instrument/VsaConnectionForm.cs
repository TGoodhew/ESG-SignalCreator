using System;
using System.Drawing;
using System.Windows.Forms;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Ui.Instrument
{
    /// <summary>
    /// Modal dialog (GitHub issue #65) — the analyzer counterpart to <see cref="ConnectionManagerForm"/>.
    /// Lets the user pick a transport (NI-VISA or NI-488.2 / GPIB), discover and select a resource,
    /// connect, and confirm the instrument is an Agilent E4406A VSA via <c>*IDN?</c> / <c>*OPT?</c>
    /// before the dialog is accepted.
    ///
    /// The analyzer sits on the ESG's RF output, so the dialog also carries a prominent RF-path safety
    /// group (<see cref="RfPathSafety"/>) describing the analyzer's maximum safe input and the inline
    /// path loss, used downstream to gate ESG power commands.
    ///
    /// On a successful Connect the opened <see cref="VsaInstrument"/> is held in <see cref="ConnectedVsa"/>;
    /// connect is <b>refused</b> if the instrument does not identify as an E4406A so the app never drives
    /// the wrong box. Pressing OK keeps the instrument (closing with <see cref="DialogResult.OK"/>);
    /// Cancel disposes any half-open instrument and leaves <see cref="ConnectedVsa"/> null. The UI is
    /// built entirely in code (no designer/resx).
    /// </summary>
    public sealed class VsaConnectionForm : Form
    {
        private readonly RadioButton _visaRadio;
        private readonly RadioButton _gpibRadio;

        // VISA controls
        private readonly ComboBox _visaResource;
        private readonly Button _visaRefresh;

        // GPIB controls
        private readonly NumericUpDown _gpibBoard;
        private readonly NumericUpDown _gpibAddress;
        private readonly ComboBox _gpibFound;
        private readonly Button _gpibRefresh;

        // RF-path safety controls
        private readonly CheckBox _safetyArmed;
        private readonly NumericUpDown _safetyMaxInput;
        private readonly NumericUpDown _safetyPathLoss;

        private readonly Button _connectButton;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Label _statusLabel;
        private readonly TextBox _detailsBox;

        /// <summary>Default GPIB primary address for the E4406A in this lab's HIL rig.</summary>
        private const byte DefaultGpibAddress = 17;

        /// <summary>
        /// The E4406A opened by a successful Connect, or null if nothing connected (or the dialog was
        /// cancelled). Ownership transfers to the caller when the dialog closes with OK.
        /// </summary>
        public VsaInstrument ConnectedVsa { get; private set; }

        /// <summary>
        /// The RF-path safety configuration as edited in the dialog. Always non-null; live-bound to the
        /// safety controls so its values reflect the UI when the dialog closes.
        /// </summary>
        public RfPathSafety Safety { get; } = new RfPathSafety();

        public VsaConnectionForm()
        {
            Text = "Connect VSA (E4406A)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 540);

            // --- Interface selection ---------------------------------------------------------
            var interfaceGroup = new GroupBox
            {
                Text = "Interface",
                Location = new Point(12, 12),
                Size = new Size(416, 50)
            };
            // Default to GPIB — the E4406A in the rig is on GPIB0::17::INSTR.
            _visaRadio = new RadioButton { Text = "VISA", Location = new Point(16, 20), AutoSize = true, Checked = false };
            _gpibRadio = new RadioButton { Text = "GPIB (488.2)", Location = new Point(120, 20), AutoSize = true, Checked = true };
            _visaRadio.CheckedChanged += (s, e) => UpdateInterfaceEnabled();
            _gpibRadio.CheckedChanged += (s, e) => UpdateInterfaceEnabled();
            interfaceGroup.Controls.Add(_visaRadio);
            interfaceGroup.Controls.Add(_gpibRadio);

            // --- VISA group ------------------------------------------------------------------
            var visaGroup = new GroupBox
            {
                Text = "VISA resource",
                Location = new Point(12, 70),
                Size = new Size(416, 80)
            };
            _visaResource = new ComboBox
            {
                Location = new Point(16, 28),
                Size = new Size(290, 23),
                DropDownStyle = ComboBoxStyle.DropDown // free-text entry allowed
            };
            _visaRefresh = new Button { Text = "Refresh", Location = new Point(316, 27), Size = new Size(84, 25) };
            _visaRefresh.Click += (s, e) => RefreshVisa();
            var visaHint = new Label
            {
                Text = "e.g. GPIB0::17::INSTR  or  TCPIP0::192.168.0.20::INSTR",
                Location = new Point(16, 55),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };
            visaGroup.Controls.Add(_visaResource);
            visaGroup.Controls.Add(_visaRefresh);
            visaGroup.Controls.Add(visaHint);

            // --- GPIB group ------------------------------------------------------------------
            var gpibGroup = new GroupBox
            {
                Text = "GPIB (NI-488.2)",
                Location = new Point(12, 158),
                Size = new Size(416, 110)
            };
            var boardLabel = new Label { Text = "Board:", Location = new Point(16, 30), AutoSize = true };
            _gpibBoard = new NumericUpDown { Location = new Point(70, 27), Size = new Size(60, 23), Minimum = 0, Maximum = 31, Value = 0 };
            var addrLabel = new Label { Text = "Address:", Location = new Point(150, 30), AutoSize = true };
            _gpibAddress = new NumericUpDown { Location = new Point(210, 27), Size = new Size(60, 23), Minimum = 0, Maximum = 30, Value = DefaultGpibAddress };

            var foundLabel = new Label { Text = "Listeners:", Location = new Point(16, 66), AutoSize = true };
            _gpibFound = new ComboBox
            {
                Location = new Point(80, 63),
                Size = new Size(226, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _gpibFound.SelectedIndexChanged += (s, e) => ApplyFoundListener();
            _gpibRefresh = new Button { Text = "Refresh", Location = new Point(316, 62), Size = new Size(84, 25) };
            _gpibRefresh.Click += (s, e) => RefreshGpib();

            gpibGroup.Controls.Add(boardLabel);
            gpibGroup.Controls.Add(_gpibBoard);
            gpibGroup.Controls.Add(addrLabel);
            gpibGroup.Controls.Add(_gpibAddress);
            gpibGroup.Controls.Add(foundLabel);
            gpibGroup.Controls.Add(_gpibFound);
            gpibGroup.Controls.Add(_gpibRefresh);

            // --- RF-path safety group --------------------------------------------------------
            var safetyGroup = new GroupBox
            {
                Text = "RF-path safety (analyzer input protection)",
                Location = new Point(12, 276),
                Size = new Size(416, 120),
                ForeColor = Color.DarkRed,
                Font = new Font(Font, FontStyle.Bold)
            };
            _safetyArmed = new CheckBox
            {
                Text = "Armed — protect analyzer input on the ESG RF output",
                Location = new Point(16, 24),
                AutoSize = true,
                Checked = Safety.Armed,
                ForeColor = SystemColors.ControlText,
                Font = new Font(Font, FontStyle.Regular)
            };
            _safetyArmed.CheckedChanged += (s, e) => Safety.Armed = _safetyArmed.Checked;

            var maxInputLabel = new Label
            {
                Text = "Analyzer max safe input (dBm):",
                Location = new Point(16, 56),
                AutoSize = true,
                ForeColor = SystemColors.ControlText,
                Font = new Font(Font, FontStyle.Regular)
            };
            _safetyMaxInput = new NumericUpDown
            {
                Location = new Point(286, 54),
                Size = new Size(110, 23),
                DecimalPlaces = 2,
                Increment = 0.5m,
                Minimum = -100m,
                Maximum = 50m,
                Value = (decimal)Safety.AnalyzerMaxSafeInputDbm
            };
            _safetyMaxInput.ValueChanged += (s, e) => Safety.AnalyzerMaxSafeInputDbm = (double)_safetyMaxInput.Value;

            var pathLossLabel = new Label
            {
                Text = "Path loss (dB):",
                Location = new Point(16, 88),
                AutoSize = true,
                ForeColor = SystemColors.ControlText,
                Font = new Font(Font, FontStyle.Regular)
            };
            _safetyPathLoss = new NumericUpDown
            {
                Location = new Point(286, 86),
                Size = new Size(110, 23),
                DecimalPlaces = 2,
                Increment = 0.5m,
                Minimum = 0m,
                Maximum = 200m,
                Value = (decimal)Safety.PathLossDb
            };
            _safetyPathLoss.ValueChanged += (s, e) => Safety.PathLossDb = (double)_safetyPathLoss.Value;

            safetyGroup.Controls.Add(_safetyArmed);
            safetyGroup.Controls.Add(maxInputLabel);
            safetyGroup.Controls.Add(_safetyMaxInput);
            safetyGroup.Controls.Add(pathLossLabel);
            safetyGroup.Controls.Add(_safetyPathLoss);

            // --- Connect + status ------------------------------------------------------------
            _connectButton = new Button { Text = "Connect", Location = new Point(12, 404), Size = new Size(100, 28) };
            _connectButton.Click += (s, e) => DoConnect();

            _statusLabel = new Label
            {
                Text = "Not connected",
                Location = new Point(124, 410),
                AutoSize = true,
                ForeColor = Color.DarkRed,
                Font = new Font(Font, FontStyle.Bold)
            };

            _detailsBox = new TextBox
            {
                Location = new Point(12, 440),
                Size = new Size(416, 60),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window
            };

            // --- OK / Cancel -----------------------------------------------------------------
            _okButton = new Button { Text = "OK", Location = new Point(268, 506), Size = new Size(75, 26), Enabled = false };
            _okButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _cancelButton = new Button { Text = "Cancel", Location = new Point(353, 506), Size = new Size(75, 26) };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.Add(interfaceGroup);
            Controls.Add(visaGroup);
            Controls.Add(gpibGroup);
            Controls.Add(safetyGroup);
            Controls.Add(_connectButton);
            Controls.Add(_statusLabel);
            Controls.Add(_detailsBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            UpdateInterfaceEnabled();
        }

        /// <summary>
        /// Result of showing the dialog: the connected E4406A (null if cancelled / never connected) and
        /// the edited RF-path safety configuration.
        /// </summary>
        public sealed class Result
        {
            public Result(VsaInstrument vsa, RfPathSafety safety)
            {
                ConnectedVsa = vsa;
                Safety = safety;
            }

            /// <summary>The connected analyzer, or null if the user cancelled or never connected.</summary>
            public VsaInstrument ConnectedVsa { get; }

            /// <summary>The RF-path safety configuration as edited in the dialog.</summary>
            public RfPathSafety Safety { get; }
        }

        /// <summary>
        /// Show the dialog modally and return a <see cref="Result"/> holding the connected E4406A (null if
        /// the user cancelled or never connected) and the edited <see cref="RfPathSafety"/>. Convenience
        /// wrapper around constructing the form and reading <see cref="ConnectedVsa"/> / <see cref="Safety"/>.
        /// </summary>
        public static new Result Show(IWin32Window owner)
        {
            using (var form = new VsaConnectionForm())
            {
                bool ok = form.ShowDialog(owner) == DialogResult.OK;
                return new Result(ok ? form.ConnectedVsa : null, form.Safety);
            }
        }

        private bool UseVisa => _visaRadio.Checked;

        private void UpdateInterfaceEnabled()
        {
            bool visa = UseVisa;

            _visaResource.Enabled = visa;
            _visaRefresh.Enabled = visa;

            _gpibBoard.Enabled = !visa;
            _gpibAddress.Enabled = !visa;
            _gpibFound.Enabled = !visa;
            _gpibRefresh.Enabled = !visa;
        }

        private void RefreshVisa()
        {
            try
            {
                _visaResource.Items.Clear();
                foreach (string resource in VisaInstrument.FindResources())
                    _visaResource.Items.Add(resource);

                if (_visaResource.Items.Count > 0)
                {
                    _visaResource.SelectedIndex = 0;
                    SetStatus(string.Format("Found {0} VISA resource(s).", _visaResource.Items.Count), Color.Black);
                }
                else
                {
                    SetStatus("No VISA resources found.", Color.DarkRed);
                }
            }
            catch (Exception ex)
            {
                SetStatus("VISA scan failed: " + ex.Message, Color.DarkRed);
            }
        }

        private void RefreshGpib()
        {
            try
            {
                _gpibFound.Items.Clear();
                int board = (int)_gpibBoard.Value;
                foreach (string listener in Gpib488Instrument.FindListeners(board))
                    _gpibFound.Items.Add(listener);

                if (_gpibFound.Items.Count > 0)
                {
                    _gpibFound.SelectedIndex = 0; // also applies the address via ApplyFoundListener
                    SetStatus(string.Format("Found {0} GPIB listener(s).", _gpibFound.Items.Count), Color.Black);
                }
                else
                {
                    SetStatus("No GPIB listeners found.", Color.DarkRed);
                }
            }
            catch (Exception ex)
            {
                SetStatus("GPIB scan failed: " + ex.Message, Color.DarkRed);
            }
        }

        /// <summary>Parse the selected "GPIBb::addr::INSTR" listener and copy its address into the numeric input.</summary>
        private void ApplyFoundListener()
        {
            string selected = _gpibFound.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;

            // Format: GPIB{board}::{address}::INSTR
            string[] parts = selected.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length >= 2 && byte.TryParse(parts[1], out byte addr) && addr <= _gpibAddress.Maximum)
                _gpibAddress.Value = addr;
        }

        private void DoConnect()
        {
            // Drop any previous connection before opening a new one.
            DisposeConnected();
            _okButton.Enabled = false;
            _detailsBox.Clear();

            IInstrument transport = null;
            VsaInstrument vsa = null;
            try
            {
                if (UseVisa)
                {
                    string resource = (_visaResource.Text ?? string.Empty).Trim();
                    if (resource.Length == 0)
                    {
                        SetStatus("Enter or select a VISA resource.", Color.DarkRed);
                        return;
                    }
                    transport = new VisaInstrument(resource);
                }
                else
                {
                    transport = new Gpib488Instrument((int)_gpibBoard.Value, (byte)_gpibAddress.Value);
                }

                vsa = new VsaInstrument(transport);
                transport = null; // ownership transferred to the VsaInstrument

                InstrumentIdentity idn = vsa.Identify();
                string[] options = vsa.Options();

                if (!vsa.IsE4406A())
                {
                    SetStatus("Refused: connected instrument is not an E4406A.", Color.DarkRed);
                    _detailsBox.Text =
                        "This dialog only drives an Agilent E4406A VSA." + Environment.NewLine +
                        "Resource: " + vsa.ResourceName + Environment.NewLine +
                        "*IDN?: " + idn + Environment.NewLine +
                        "*OPT?: " + (options.Length > 0 ? string.Join(", ", options) : "(none)");
                    vsa.Dispose();
                    vsa = null;
                    return;
                }

                ConnectedVsa = vsa;
                vsa = null; // ownership transferred to ConnectedVsa; don't dispose in finally

                _detailsBox.Text =
                    "Resource: " + ConnectedVsa.ResourceName + Environment.NewLine +
                    "*IDN?: " + idn + Environment.NewLine +
                    "*OPT?: " + (options.Length > 0 ? string.Join(", ", options) : "(none)");

                SetStatus("Connected", Color.Green);
                _okButton.Enabled = true;
            }
            catch (Exception ex)
            {
                DisposeConnected();
                SetStatus("Connect failed: " + ex.Message, Color.DarkRed);
                _detailsBox.Text = ex.ToString();
            }
            finally
            {
                // Dispose anything half-open that did not transfer ownership: a wrapped VSA (Identify/
                // Options/IsE4406A threw), or a bare transport (wrapping threw before transport = null).
                vsa?.Dispose();
                transport?.Dispose();
            }
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        private void DisposeConnected()
        {
            if (ConnectedVsa != null)
            {
                try { ConnectedVsa.Dispose(); } catch { /* best effort */ }
                ConnectedVsa = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // If the dialog is closing for any reason other than OK, the caller does not receive the
            // instrument, so dispose it here to avoid leaking the open transport.
            if (DialogResult != DialogResult.OK)
                DisposeConnected();

            base.OnFormClosing(e);
        }
    }
}
