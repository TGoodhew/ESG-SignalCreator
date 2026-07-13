using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using EsgSignalCreator;
using EsgSignalCreator.Capability;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Export;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Presets;
using EsgSignalCreator.Project;
using EsgSignalCreator.Seamless;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Ui.Canvas;
using EsgSignalCreator.Ui.Instrument;
using EsgSignalCreator.Ui.Pipeline;
using EsgSignalCreator.Ui.Plots;
using EsgSignalCreator.Ui.Sequencing;
using EsgSignalCreator.Ui.Verify;
using EsgSignalCreator.Ui.Sources;
using EsgSignalCreator.Validation;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Ui
{
    /// <summary>
    /// The Signal Studio shell (UX brief §3): top action bar, a left project tree, a centre with the
    /// signal-flow canvas + active source panel, a right dock of verification plots + results, and a
    /// status bar — driving the deliberate Calculate → Download → Play pipeline (§8).
    /// </summary>
    public sealed partial class StudioForm : Form
    {
        private readonly SignalFlowStrip _canvas = new SignalFlowStrip();
        private readonly ComboBox _sourcePicker = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Panel _sourceHost = new Panel { Dock = DockStyle.Fill };
        private readonly InstrumentSettingsPanel _settings = new InstrumentSettingsPanel { Dock = DockStyle.Fill };
        private readonly ScpiConsolePanel _console = new ScpiConsolePanel { Dock = DockStyle.Fill };
        private readonly NotificationsDock _notifications = new NotificationsDock { Dock = DockStyle.Fill };
        private readonly PlotPane _plotIq = new PlotPane { Dock = DockStyle.Fill };
        private readonly PlotPane _plotSpectrum = new PlotPane { Dock = DockStyle.Fill };
        private readonly PlotPane _plotThird = new PlotPane { Dock = DockStyle.Fill };
        private readonly Label _readout = new Label { Dock = DockStyle.Fill, Text = "No waveform calculated.", Padding = new Padding(6) };
        private readonly ProgressBar _progress = new ProgressBar { Dock = DockStyle.Bottom, Height = 16, Maximum = 100 };
        private readonly PlayStateIndicator _play = new PlayStateIndicator { Dock = DockStyle.Bottom };

        private readonly ToolStripButton _calcBtn;
        private readonly ToolStripButton _downloadBtn;
        private readonly ToolStripButton _playBtn;
        private readonly ToolStripButton _stopBtn;
        private readonly ToolStripButton _allBtn;
        private readonly ToolStripLabel _online = new ToolStripLabel("Offline") { ForeColor = System.Drawing.Color.Firebrick };
        private readonly ToolStripStatusLabel _status = new ToolStripStatusLabel("Ready.");
        private readonly ToolStripStatusLabel _statusModel = new ToolStripStatusLabel("No instrument");

        private readonly Panel _centerCards = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _sourceCard = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _instrumentCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _consoleCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _notificationsCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _verificationCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly VerificationView _verification = new VerificationView { Dock = DockStyle.Fill };
        private readonly Panel _assistantCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _impairmentsCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _sequenceCard = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly SequencePanel _sequence = new SequencePanel { Dock = DockStyle.Fill };

        private readonly CheckBox _iqEnable = new CheckBox { Text = "Apply I/Q impairments", AutoSize = true, Dock = DockStyle.Top };
        private readonly IqImpairmentConfig _iqCfg = new IqImpairmentConfig();
        private readonly CheckBox _awgnEnable = new CheckBox { Text = "Apply AWGN impairment", AutoSize = true, Dock = DockStyle.Top };
        private readonly AwgnImpairmentConfig _awgnCfg = new AwgnImpairmentConfig();
        private readonly CheckBox _cfrEnable = new CheckBox { Text = "Apply CFR", AutoSize = true, Dock = DockStyle.Top };
        private readonly CfrConfig _cfrCfg = new CfrConfig();
        private readonly CheckBox _filterEnable = new CheckBox { Text = "Apply filter", AutoSize = true, Dock = DockStyle.Top };
        private readonly FilterConfig _filterCfg = new FilterConfig();

        private readonly SplitContainer _outerSplit;
        private readonly SplitContainer _rightSplit;
        private readonly InstrumentProfile _profile = InstrumentProfiles.Load("E4438C");
        private EsgInstrument _instrument;
        private EsgController _esg;
        private VsaInstrument _vsa;
        private RfPathSafety _safety = new RfPathSafety();
        private readonly UiSettings _uiSettings = UiSettings.Load();
        private VsaModel _vsaModel;
        private ToolStripDropDownButton _vsaModelToggle;
        private readonly ToolStripLabel _vsaOnline = new ToolStripLabel("VSA: offline") { ForeColor = System.Drawing.Color.Firebrick };
        private ISignalSourcePanel _sourcePanel;
        private WaveformModel _waveform;

        public StudioForm()
        {
            Text = "ESG Signal Studio";
            Width = 1200;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;

            // ---- top action bar ----
            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripLabel("Target: E4438C"));
            bar.Items.Add(new ToolStripSeparator());
            var connect = new ToolStripButton("Connect…"); connect.Click += (s, e) => Connect();
            bar.Items.Add(connect);
            bar.Items.Add(_online);
            var connectVsa = new ToolStripButton("Connect VSA…"); connectVsa.Click += (s, e) => ConnectVsa();
            bar.Items.Add(connectVsa);
            // VSA model toggle (#108): choose which analyzer the Connect VSA… dialog targets.
            _vsaModel = _uiSettings.VsaModel;
            _vsaModelToggle = new ToolStripDropDownButton("VSA model: " + _vsaModel)
            {
                ToolTipText = "Switch the analyzer the app drives (E4406A or N9010A). Affects the next connect."
            };
            foreach (VsaModel m in new[] { VsaModel.E4406A, VsaModel.N9010A })
            {
                VsaModel choice = m;
                var item = new ToolStripMenuItem(VsaModels.DisplayName(m)) { Tag = m, Checked = m == _vsaModel };
                item.Click += (s, e) => SelectVsaModel(choice);
                _vsaModelToggle.DropDownItems.Add(item);
            }
            bar.Items.Add(_vsaModelToggle);
            bar.Items.Add(_vsaOnline);
            UpdateVsaOffline();
            bar.Items.Add(new ToolStripSeparator());
            _calcBtn = Button("Calculate", async (s, e) => await Calculate());
            _downloadBtn = Button("Download", (s, e) => Download());
            _playBtn = Button("Play", (s, e) => Play());
            _stopBtn = Button("Stop", (s, e) => Stop());
            _allBtn = Button("Calc → DL → Play", async (s, e) => { if (await Calculate() && Download()) Play(); });
            bar.Items.Add(_calcBtn); bar.Items.Add(_downloadBtn); bar.Items.Add(_playBtn); bar.Items.Add(_stopBtn);
            var verifyBtn = Button("Verify", (s, e) => Verify());
            bar.Items.Add(verifyBtn);
            var pathCalBtn = Button("Path cal…", (s, e) => PathCalibrate());
            bar.Items.Add(pathCalBtn);
            var refBtn = new ToolStripDropDownButton("Reference");
            refBtn.DropDownItems.Add("Independent (each internal)", null, (s, e) => ApplyReference(ReferenceScheme.Independent));
            refBtn.DropDownItems.Add("Common 10 MHz (external)", null, (s, e) => ApplyReference(ReferenceScheme.CommonExternal));
            bar.Items.Add(refBtn);
            var modeBtn = new ToolStripDropDownButton("VSA Mode");
            modeBtn.DropDownOpening += (s, e) => PopulateVsaModes(modeBtn);
            bar.Items.Add(modeBtn);
            bar.Items.Add(new ToolStripSeparator()); bar.Items.Add(_allBtn);
            bar.Items.Add(new ToolStripSeparator());
            var save = new ToolStripButton("Save…"); save.Click += (s, e) => SaveProject();
            var open = new ToolStripButton("Open…"); open.Click += (s, e) => OpenProject();
            bar.Items.Add(save); bar.Items.Add(open);
            bar.Items.Add(new ToolStripSeparator());

            var presets = new ToolStripDropDownButton("Presets");
            foreach (TestModel m in TestModels.All)
            {
                TestModel model = m;
                presets.DropDownItems.Add(model.Name, null, (s, e) => SetActiveSourcePanel(WrapPersonality(model.Create())));
            }
            bar.Items.Add(presets);

            var export = new ToolStripDropDownButton("Export");
            export.DropDownItems.Add("Raw ARB (int16 BE)…", null, (s, e) => Export("arb"));
            export.DropDownItems.Add("CSV I/Q…", null, (s, e) => Export("csv"));
            export.DropDownItems.Add("SCPI script…", null, (s, e) => Export("scpi"));
            bar.Items.Add(export);

            // ---- status bar ----
            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(_status);
            statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
            statusStrip.Items.Add(_statusModel);

            // ---- left tree ----
            var tree = new TreeView { Dock = DockStyle.Fill };
            tree.Nodes.Add("source", "Source");
            tree.Nodes.Add("impairments", "Impairments");
            tree.Nodes.Add("sequence", "Sequence");
            tree.Nodes.Add("instrument", "Instrument settings");
            tree.Nodes.Add("console", "SCPI console");
            tree.Nodes.Add("notifications", "Notifications");
            tree.Nodes.Add("verification", "Verification");
            tree.Nodes.Add("assistant", "Assistant");
            tree.SelectedNode = tree.Nodes[0];
            tree.AfterSelect += (s, e) => ShowCard(e.Node.Name);

            // ---- centre cards ----
            _sourcePicker.SelectedIndexChanged += (s, e) => SelectPersonality();
            foreach (PersonalityDescriptor d in PersonalityRegistry.All) _sourcePicker.Items.Add(d);
            var sourceInner = new Panel { Dock = DockStyle.Fill };
            sourceInner.Controls.Add(_sourceHost);
            sourceInner.Controls.Add(_sourcePicker);
            _sourceCard.Controls.Add(sourceInner);
            _sourceCard.Controls.Add(_canvas);
            _instrumentCard.Controls.Add(_settings);
            _consoleCard.Controls.Add(_console);
            _notificationsCard.Controls.Add(_notifications);
            _verificationCard.Controls.Add(_verification);
            BuildImpairmentsCard();
            _sequenceCard.Controls.Add(_sequence);
            _centerCards.Controls.Add(_sourceCard);
            _centerCards.Controls.Add(_impairmentsCard);
            _centerCards.Controls.Add(_sequenceCard);
            _centerCards.Controls.Add(_instrumentCard);
            _centerCards.Controls.Add(_consoleCard);
            _centerCards.Controls.Add(_notificationsCard);
            _centerCards.Controls.Add(_verificationCard);
            _centerCards.Controls.Add(_assistantCard);

            // ---- right dock: three plots + readout + progress + play state ----
            _plotSpectrum.SelectedView = PlotPane.ViewType.Spectrum;
            _plotThird.SelectedView = PlotPane.ViewType.Constellation;
            var plots = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            plots.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            plots.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            plots.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            plots.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            plots.Controls.Add(_plotIq, 0, 0);
            plots.Controls.Add(_plotSpectrum, 0, 1);
            plots.Controls.Add(_plotThird, 0, 2);
            var readoutHost = new Panel { Dock = DockStyle.Fill };
            readoutHost.Controls.Add(_readout);
            readoutHost.Controls.Add(_play);
            readoutHost.Controls.Add(_progress);
            plots.Controls.Add(readoutHost, 0, 3);

            // ---- splitters ---- (SplitterDistance is set in OnLoad, once the form has a real size)
            _rightSplit = new SplitContainer { Dock = DockStyle.Fill };
            _rightSplit.Panel1.Controls.Add(_centerCards);
            _rightSplit.Panel2.Controls.Add(plots);
            _outerSplit = new SplitContainer { Dock = DockStyle.Fill };
            _outerSplit.Panel1.Controls.Add(tree);
            _outerSplit.Panel2.Controls.Add(_rightSplit);

            Controls.Add(_outerSplit);
            Controls.Add(bar);
            Controls.Add(statusStrip);

            _notifications.JumpToFieldRequested += (s, field) => _status.Text = "Field: " + field;
            if (_sourcePicker.Items.Count > 0) _sourcePicker.SelectedIndex = 0;
            WireAssistant();
            UpdatePipelineEnabled();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Now the form is sized; set splitter positions safely (clamped to the panels).
            SetSplitter(_outerSplit, 190);
            SetSplitter(_rightSplit, _rightSplit.Width - 360);
        }

        private static void SetSplitter(SplitContainer split, int distance)
        {
            int min = split.Panel1MinSize;
            int max = split.Width - split.Panel2MinSize - split.SplitterWidth;
            if (max < min) return;
            split.SplitterDistance = Math.Min(Math.Max(distance, min), max);
        }

        private static ToolStripButton Button(string text, EventHandler onClick)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            b.Click += onClick;
            return b;
        }

        private void ShowCard(string name)
        {
            _sourceCard.Visible = name == "source";
            _impairmentsCard.Visible = name == "impairments";
            _sequenceCard.Visible = name == "sequence";
            _instrumentCard.Visible = name == "instrument";
            _consoleCard.Visible = name == "console";
            _notificationsCard.Visible = name == "notifications";
            _verificationCard.Visible = name == "verification";
            _assistantCard.Visible = name == "assistant";
        }

        private void BuildImpairmentsCard()
        {
            var iqGroup = new GroupBox { Text = "I/Q impairments", Dock = DockStyle.Top, Height = 220 };
            var iqGrid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = _iqCfg };
            iqGroup.Controls.Add(iqGrid);
            iqGroup.Controls.Add(_iqEnable);

            var awgnGroup = new GroupBox { Text = "AWGN impairment", Dock = DockStyle.Top, Height = 200 };
            var awgnGrid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = _awgnCfg };
            awgnGroup.Controls.Add(awgnGrid);
            awgnGroup.Controls.Add(_awgnEnable);

            var cfrGroup = new GroupBox { Text = "CFR (crest-factor reduction)", Dock = DockStyle.Top, Height = 160 };
            cfrGroup.Controls.Add(new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = _cfrCfg });
            cfrGroup.Controls.Add(_cfrEnable);

            var filterGroup = new GroupBox { Text = "Filter / correction", Dock = DockStyle.Top, Height = 160 };
            filterGroup.Controls.Add(new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = _filterCfg });
            filterGroup.Controls.Add(_filterEnable);

            _awgnEnable.CheckedChanged += (s, e) => _canvas.SetEnabled("awgn", _awgnEnable.Checked);
            _cfrEnable.CheckedChanged += (s, e) => _canvas.SetEnabled("cfr", _cfrEnable.Checked);
            _filterEnable.CheckedChanged += (s, e) => _canvas.SetEnabled("filter", _filterEnable.Checked);

            // Added bottom-first so docking stacks them top-to-bottom in a sensible order.
            _impairmentsCard.Controls.Add(filterGroup);
            _impairmentsCard.Controls.Add(cfrGroup);
            _impairmentsCard.Controls.Add(awgnGroup);
            _impairmentsCard.Controls.Add(iqGroup);
        }

        private void SelectPersonality()
        {
            var d = _sourcePicker.SelectedItem as PersonalityDescriptor;
            if (d == null) return;
            SetActiveSourcePanel(PersonalityRegistry.CreatePanel(d.Id));
        }

        private void SetActiveSourcePanel(ISignalSourcePanel panel)
        {
            _sourceHost.Controls.Clear();
            _sourcePanel = panel;
            Control c = panel.AsControl();
            c.Dock = DockStyle.Fill;
            _sourceHost.Controls.Add(c);
            panel.CalculateRequested += async (s, e) => await Calculate();
        }

        // ---- pipeline ----

        private async Task<bool> Calculate()
        {
            if (_sourcePanel == null) { _status.Text = "Pick a source first."; return false; }
            SetBusy(true);
            _play.State = PlayState.Busy;
            try
            {
                IWaveformPersonality p = _sourcePanel.BuildPersonality();
                var progress = new Progress<int>(v => { if (v >= 0 && v <= 100) _progress.Value = v; });
                WaveformModel wf = await Task.Run(() => p.Calculate(progress));
                if (_iqEnable.Checked) wf = IqImpairments.Apply(wf, _iqCfg);
                if (_awgnEnable.Checked) wf = AwgnImpairment.Apply(wf, _awgnCfg);
                if (_cfrEnable.Checked) wf = Cfr.Apply(wf, _cfrCfg);
                if (_filterEnable.Checked) wf = FilterImpairment.Apply(wf, _filterCfg);
                _waveform = wf;
                _plotIq.Show(wf);
                _plotSpectrum.Show(wf);
                _plotThird.Show(wf);
                RunValidation(wf);
                UpdateReadout(wf);
                _status.Text = "Calculated " + wf.Length.ToString("n0", CultureInfo.InvariantCulture) + " samples.";
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Calculate failed: " + ex.Message));
                _status.Text = "Calculate failed.";
                return false;
            }
            finally
            {
                _progress.Value = 0;
                _play.State = PlayState.Idle;
                SetBusy(false);
                UpdatePipelineEnabled();
            }
        }

        private bool Download()
        {
            if (_waveform == null) { _status.Text = "Calculate a waveform first."; return false; }
            if (_esg == null) { _status.Text = "Connect to an instrument first."; return false; }
            try
            {
                string seg = SegmentName();
                _esg.DownloadWaveform(seg, _waveform);
                _status.Text = "Downloaded '" + seg + "'.";
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Download failed: " + ex.Message));
                _status.Text = "Download failed.";
                return false;
            }
        }

        private void Play()
        {
            if (_esg == null || _waveform == null) { _status.Text = "Nothing to play."; return; }
            try
            {
                _esg.PlayWaveform(SegmentName(), _waveform.SampleRateHz);
                _esg.SetRfOutput(true);
                _play.State = PlayState.Playing;
                _status.Text = "Playing.";
            }
            catch (Exception ex)
            {
                _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Play failed: " + ex.Message));
            }
        }

        private void Stop()
        {
            if (_esg == null) return;
            try { _esg.SetArbState(false); _esg.SetRfOutput(false); }
            catch (Exception ex) { _notifications.Append(new ValidationResult(ValidationSeverity.Warning, "Stop: " + ex.Message)); }
            _play.State = PlayState.Idle;
            _status.Text = "Stopped.";
        }

        private string SegmentName()
        {
            var b = _sourcePanel as SourcePanelBase;
            string s = b != null ? b.SegmentName : "SEG1";
            return string.IsNullOrEmpty(s) ? "SEG1" : s;
        }

        private void RunValidation(WaveformModel wf)
        {
            double carrier = _profile != null ? (_profile.MinFrequencyHz + _profile.MaxFrequencyHz) / 2 : 1e9;
            var results = new System.Collections.Generic.List<ValidationResult>(WaveformValidator.Validate(wf, _profile, wf.SampleRateHz, carrier));
            if (!SeamlessGuard.IsSeamless(wf))
                results.Add(new ValidationResult(ValidationSeverity.Warning,
                    "Loop seam discontinuity — the waveform may not loop seamlessly. Consider an integer-cycle length.", "Length"));
            _notifications.Show(results);
        }

        private void UpdateReadout(WaveformModel wf)
        {
            double durationUs = wf.Length / wf.SampleRateHz * 1e6;
            double[] iD = ToD(wf.I), qD = ToD(wf.Q);
            double papr = EsgSignalCreator.Dsp.Ccdf.PaprDb(iD, qD);
            double peak = wf.PeakMagnitude();

            double sumSq = 0;
            for (int n = 0; n < wf.Length; n++) sumSq += (double)wf.I[n] * wf.I[n] + (double)wf.Q[n] * wf.Q[n];
            double rms = Math.Sqrt(sumSq / wf.Length);
            double obwMHz = OccupiedBandwidthHz(iD, qD, wf.SampleRateHz) / 1e6;

            _readout.Text = string.Format(CultureInfo.InvariantCulture,
                "Samples: {0:n0}   Sample clock: {1:0.###} MHz   Duration: {2:0.###} µs   Download: {3:n0} bytes\r\n" +
                "Peak: {4:0.###}   RMS: {5:0.###}   PAPR: {6:0.##} dB   Occupied BW (99%): {7:0.###} MHz",
                wf.Length, wf.SampleRateHz / 1e6, durationUs, (long)wf.Length * 4, peak, rms, papr, obwMHz);
        }

        /// <summary>Estimate the 99% occupied bandwidth from the centered power spectrum.</summary>
        private static double OccupiedBandwidthHz(double[] i, double[] q, double sampleRateHz)
        {
            Fft.MagnitudeSpectrumDb(i, q, sampleRateHz, out double[] f, out double[] db);
            var p = new double[db.Length];
            double total = 0;
            for (int k = 0; k < db.Length; k++) { p[k] = Math.Pow(10, db[k] / 10); total += p[k]; }
            if (total <= 0) return 0;

            double lo = 0.005 * total, hi = 0.995 * total, cum = 0;
            double fLo = f[0], fHi = f[f.Length - 1];
            bool gotLo = false;
            for (int k = 0; k < p.Length; k++)
            {
                cum += p[k];
                if (!gotLo && cum >= lo) { fLo = f[k]; gotLo = true; }
                if (cum >= hi) { fHi = f[k]; break; }
            }
            return Math.Abs(fHi - fLo);
        }

        // ---- connection ----

        private void Connect()
        {
            EsgInstrument inst = ConnectionManagerForm.Connect(this);
            if (inst == null) return;
            AttachInstrument(inst);
        }

        /// <summary>Wire an opened ESG session into the form (shared by the Connect dialog and the assistant).</summary>
        private void AttachInstrument(EsgInstrument inst)
        {
            _instrument = inst;
            _esg = new EsgController(inst.Transport);
            _settings.Attach(_esg);
            _console.Attach(inst.Transport);
            _online.Text = "Online";
            _online.ForeColor = System.Drawing.Color.ForestGreen;
            try
            {
                InstrumentIdentity id = inst.Identify();
                _statusModel.Text = id.Model + "  " + id.FirmwareRevision;
            }
            catch { _statusModel.Text = inst.ResourceName; }
            UpdatePipelineEnabled();
        }

        private void ConnectVsa()
        {
            VsaConnectionForm.Result r = VsaConnectionForm.Show(this, _vsaModel);
            _safety = r.Safety ?? _safety;
            if (r.ConnectedVsa == null) return;
            _vsa = r.ConnectedVsa;
            _vsaOnline.Text = "VSA: " + _vsaModel + " online";
            _vsaOnline.ForeColor = System.Drawing.Color.ForestGreen;
            try { _status.Text = "VSA connected: " + _vsa.Identify().Model; }
            catch { _status.Text = "VSA connected."; }
        }

        /// <summary>
        /// Switch which analyzer model the app targets (#108). Updates the toggle, persists the choice,
        /// and — while no VSA is connected — the offline status label. Takes effect on the next connect.
        /// </summary>
        private void SelectVsaModel(VsaModel model)
        {
            _vsaModel = model;
            _uiSettings.VsaModel = model;
            _uiSettings.Save();
            _vsaModelToggle.Text = "VSA model: " + model;
            foreach (ToolStripItem dropItem in _vsaModelToggle.DropDownItems)
                if (dropItem is ToolStripMenuItem mi && mi.Tag is VsaModel tagged)
                    mi.Checked = tagged == model;
            if (_vsa == null) UpdateVsaOffline();
        }

        /// <summary>Show the currently selected model in the offline VSA status label.</summary>
        private void UpdateVsaOffline()
        {
            _vsaOnline.Text = "VSA: " + _vsaModel + " offline";
            _vsaOnline.ForeColor = System.Drawing.Color.Firebrick;
        }

        /// <summary>
        /// Manual closed-loop verify (#67/#71): read the ESG's commanded centre frequency + power,
        /// measure the played signal on the E4406A (channel power, PAPR, and — for a single tone — the
        /// spectrum peak) and compare expected vs measured within the verification profile, accounting
        /// for the declared path loss. Results land in the Verification view as an expected-vs-measured table.
        /// </summary>
        private void Verify()
        {
            if (_esg == null) { _status.Text = "Connect the ESG first."; return; }
            if (_vsa == null) { _status.Text = "Connect the VSA first (Connect VSA…)."; return; }
            if (_waveform == null) { _status.Text = "Calculate/play a waveform first."; return; }

            try
            {
                double carrierHz = _esg.GetFrequencyHz();
                double esgPowerDbm = _esg.GetAmplitudeDbm();
                var profile = new VerificationProfile { PathLossDb = _safety.PathLossDb };

                System.Collections.Generic.IReadOnlyList<VerificationResult> results =
                    VerificationHarness.Verify(_vsa, _waveform, carrierHz, esgPowerDbm, profile, CwOffsetHz());
                _verification.Show(results);
                ShowCard("verification");

                bool pass = VerificationHarness.AllPass(results);
                _status.Text = pass ? "Verify: PASS (see Verification)" : "Verify: FAIL (see Verification)";
            }
            catch (Exception ex)
            {
                _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Verify failed: " + ex.Message));
                _status.Text = "Verify failed.";
            }
        }

        /// <summary>
        /// Standard-personality mode picker (#76): populate the dropdown from the analyzer's
        /// <c>:INSTrument:CATalog?</c> so only installed modes (Basic plus any option-gated personalities
        /// such as GSM / W-CDMA / cdma2000) are offered; the current mode is checked.
        /// </summary>
        private void PopulateVsaModes(ToolStripDropDownButton btn)
        {
            btn.DropDownItems.Clear();
            if (_vsa == null)
            {
                btn.DropDownItems.Add(new ToolStripMenuItem("(connect the VSA first)") { Enabled = false });
                return;
            }

            try
            {
                System.Collections.Generic.IReadOnlyList<InstrumentMode> modes =
                    InstrumentModeCatalog.Resolve(_vsa.ModeCatalog());
                string current = null;
                try { current = _vsa.GetMode(); } catch { /* current mode is optional decoration */ }

                if (modes.Count == 0)
                {
                    btn.DropDownItems.Add(new ToolStripMenuItem("(no modes reported)") { Enabled = false });
                    return;
                }

                foreach (InstrumentMode m in modes)
                {
                    string label = m.DisplayName + (m.IsStandardPersonality ? "" : "  (base)");
                    var item = new ToolStripMenuItem(label, null, (s, e) => SelectVsaMode(m.Mnemonic))
                    {
                        ToolTipText = m.Description,
                        Checked = current != null && string.Equals(current, m.Mnemonic, StringComparison.OrdinalIgnoreCase)
                    };
                    btn.DropDownItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                btn.DropDownItems.Add(new ToolStripMenuItem("Catalog error: " + ex.Message) { Enabled = false });
            }
        }

        private void SelectVsaMode(string mnemonic)
        {
            if (_vsa == null) { _status.Text = "Connect the VSA first (Connect VSA…)."; return; }
            try
            {
                _vsa.SelectMode(mnemonic);
                _status.Text = "VSA mode: " + mnemonic;
            }
            catch (Exception ex)
            {
                _status.Text = "Mode select failed: " + ex.Message;
            }
        }

        /// <summary>
        /// Common 10 MHz reference control (#75): lock the ESG and E4406A to a common external timebase
        /// (or return them to independent internal timebases) and report the resulting source of each.
        /// </summary>
        private void ApplyReference(ReferenceScheme scheme)
        {
            if (_esg == null) { _status.Text = "Connect the ESG first."; return; }
            if (_vsa == null) { _status.Text = "Connect the VSA first (Connect VSA…)."; return; }

            try
            {
                ReferenceLock.Apply(_esg, _vsa, scheme);
                ReferenceStatus st = ReferenceLock.Read(_esg, _vsa);
                _status.Text = "Reference: " + st;
            }
            catch (Exception ex)
            {
                _status.Text = "Reference set failed: " + ex.Message;
            }
        }

        /// <summary>
        /// Path-calibration wizard (#72): run a guided CW measurement on the E4406A and store the
        /// commanded−measured delta as the inline path loss, applied to both the safety gate and the
        /// verification harness so subsequent Verify runs are self-consistent.
        /// </summary>
        private void PathCalibrate()
        {
            if (_esg == null) { _status.Text = "Connect the ESG first."; return; }
            if (_vsa == null) { _status.Text = "Connect the VSA first (Connect VSA…)."; return; }

            using (var form = new PathCalibrationForm(_esg, _vsa, _safety))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.AppliedPathLossDb.HasValue)
                {
                    _safety.PathLossDb = form.AppliedPathLossDb.Value;
                    _status.Text = string.Format(CultureInfo.InvariantCulture,
                        "Path loss calibrated: {0:0.###} dB (applied to safety + verify).", _safety.PathLossDb);
                }
            }
        }

        private double CwOffsetHz()
        {
            if (_sourcePanel == null) return 0;
            object cfg = _sourcePanel.GetConfig();
            System.Reflection.PropertyInfo p = cfg?.GetType().GetProperty("FreqOffsetHz");
            if (p == null) return 0;
            object v = p.GetValue(cfg);
            return v == null ? 0 : Convert.ToDouble(v);
        }

        // ---- project I/O ----

        private void SaveProject()
        {
            if (_sourcePanel == null) return;
            using (var dlg = new SaveFileDialog { Filter = "Signal Studio project (*.ssproj)|*.ssproj", DefaultExt = "ssproj" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                object cfg = _sourcePanel.GetConfig();
                var proj = new SsProject
                {
                    PersonalityId = _sourcePanel.PersonalityId,
                    ConfigTypeName = cfg.GetType().AssemblyQualifiedName,
                    ConfigJson = ProjectStore.SerializeConfig(cfg)
                };
                ProjectStore.Save(dlg.FileName, proj);
                _status.Text = "Saved " + dlg.FileName;
            }
        }

        private void OpenProject()
        {
            using (var dlg = new OpenFileDialog { Filter = "Signal Studio project (*.ssproj)|*.ssproj" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SsProject proj = ProjectStore.Load(dlg.FileName);
                    ISignalSourcePanel panel = PersonalityRegistry.CreatePanel(proj.PersonalityId);
                    object cfg = ProjectStore.DeserializeConfig(proj.ConfigJson, proj.ConfigTypeName);
                    panel.LoadConfig(cfg);
                    SetActiveSourcePanel(panel);
                    _status.Text = "Opened " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Open failed: " + ex.Message));
                }
            }
        }

        private ISignalSourcePanel WrapPersonality(IWaveformPersonality p) => new GenericSourcePanel(p);

        private void Export(string kind)
        {
            if (_waveform == null) { _status.Text = "Calculate a waveform first."; return; }
            string filter = kind == "csv" ? "CSV (*.csv)|*.csv"
                : kind == "scpi" ? "SCPI script (*.scpi)|*.scpi"
                : "Raw ARB (*.bin)|*.bin";
            using (var dlg = new SaveFileDialog { Filter = filter })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string seg = SegmentName();
                    if (kind == "csv") WaveformExporter.SaveCsv(dlg.FileName, _waveform);
                    else if (kind == "scpi") WaveformExporter.SaveScpiScript(dlg.FileName, _waveform, seg, 1e9, -10.0);
                    else WaveformExporter.SaveRawArb(dlg.FileName, _waveform);
                    _status.Text = "Exported " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    _notifications.Append(new ValidationResult(ValidationSeverity.Error, "Export failed: " + ex.Message));
                }
            }
        }

        // ---- helpers ----

        private void SetBusy(bool busy)
        {
            _calcBtn.Enabled = !busy;
            _allBtn.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void UpdatePipelineEnabled()
        {
            bool online = _esg != null;
            bool haveWf = _waveform != null;
            _downloadBtn.Enabled = online && haveWf;
            _playBtn.Enabled = online && haveWf;
            _stopBtn.Enabled = online;
        }

        private static double[] ToD(float[] x)
        {
            var d = new double[x.Length];
            for (int n = 0; n < x.Length; n++) d[n] = x[n];
            return d;
        }
    }
}
