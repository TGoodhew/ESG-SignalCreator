using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EsgSignalCreator.Assistant.Agent;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Guardrails;
using EsgSignalCreator.Assistant.Secrets;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json;

namespace EsgSignalCreator.Ui.Assistant
{
    /// <summary>Everything the pane needs to run a conversation. Built once by the host (StudioForm).</summary>
    public sealed class AssistantPaneDeps
    {
        public ConversationStore Store;
        public ToolRegistry Registry;
        public ToolDispatcher Dispatcher;
        public AssistantSettings Settings;
        public AssistantSettingsStore SettingsStore;
        public ApiKeyStore KeyStore;
        public EffectPolicyOptions PolicyOptions;                 // shared with the policy (live auto-approve toggle)
        public Func<ClaudeClientOptions, IClaudeClient> ClientFactory;
        public string SystemPrompt;
        public Action<string> Log;                                // optional: echo notes to the app log
        public Func<string, bool> ReadOnlyClassifier;             // reads run concurrently (#89)
        public int MaxHistoryMessages;                            // conversation compaction (#89); 0 = unlimited
    }

    /// <summary>
    /// The in-app Claude assistant pane (#84). A transcript of message bubbles, an input box with
    /// Send/Stop, a settings strip (enable + API key + auto-approve), and inline Approve/Decline
    /// confirmation cards — the pane itself is the <see cref="IHardwareConfirmer"/> the guardrail
    /// policy calls. The conversation runs OFF the UI thread; streamed text and confirmation cards
    /// marshal back here. Nothing reaches hardware without a card the user approves.
    /// </summary>
    public sealed class AssistantPane : UserControl, IHardwareConfirmer
    {
        private AssistantPaneDeps _deps;

        private readonly FlowLayoutPanel _transcript;
        private readonly TextBox _input;
        private readonly Button _send;
        private readonly Button _stop;
        private readonly CheckBox _enabled;
        private readonly CheckBox _autoApprove;
        private readonly CheckBox _rawScpi;
        private readonly Button _keyButton;
        private readonly Label _modelLabel;

        private CancellationTokenSource _cts;
        private Label _streamingLabel;       // the assistant bubble being streamed this turn
        private bool _busy;

        public AssistantPane()
        {
            // ---- settings strip (top) ----
            var settingsBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(4, 4, 4, 0), WrapContents = false, AutoScroll = false };
            _enabled = new CheckBox { Text = "Enable assistant", AutoSize = true, Margin = new Padding(2, 4, 12, 0) };
            _autoApprove = new CheckBox { Text = "Auto-approve hardware", AutoSize = true, Margin = new Padding(2, 4, 12, 0) };
            _rawScpi = new CheckBox { Text = "Allow raw SCPI", AutoSize = true, Margin = new Padding(2, 4, 12, 0) };
            _keyButton = new Button { Text = "Set API key…", AutoSize = true, Margin = new Padding(2, 1, 12, 0) };
            _modelLabel = new Label { Text = "", AutoSize = true, Margin = new Padding(2, 6, 0, 0), ForeColor = Color.DimGray };
            _enabled.CheckedChanged += (s, e) => OnEnabledChanged();
            _autoApprove.CheckedChanged += (s, e) => OnAutoApproveChanged();
            _rawScpi.CheckedChanged += (s, e) => OnRawScpiChanged();
            _keyButton.Click += (s, e) => PromptAndStoreKey();
            settingsBar.Controls.Add(_enabled);
            settingsBar.Controls.Add(_autoApprove);
            settingsBar.Controls.Add(_rawScpi);
            settingsBar.Controls.Add(_keyButton);
            settingsBar.Controls.Add(_modelLabel);

            // ---- transcript (centre) ----
            _transcript = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(6),
                BackColor = Color.White
            };
            _transcript.Resize += (s, e) => ReflowWidths();

            // ---- input (bottom) ----
            var inputBar = new Panel { Dock = DockStyle.Bottom, Height = 92, Padding = new Padding(4) };
            _input = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = false, ScrollBars = ScrollBars.Vertical };
            _input.KeyDown += OnInputKeyDown;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, Width = 84, WrapContents = false };
            _send = new Button { Text = "Send", Width = 76, Height = 34, Margin = new Padding(2) };
            _stop = new Button { Text = "Stop", Width = 76, Height = 34, Margin = new Padding(2), Enabled = false };
            _send.Click += async (s, e) => await SendAsync();
            _stop.Click += (s, e) => _cts?.Cancel();
            buttons.Controls.Add(_send);
            buttons.Controls.Add(_stop);
            inputBar.Controls.Add(_input);
            inputBar.Controls.Add(buttons);

            Controls.Add(_transcript);
            Controls.Add(inputBar);
            Controls.Add(settingsBar);
        }

        /// <summary>Inject dependencies and reflect persisted settings into the controls.</summary>
        public void Initialize(AssistantPaneDeps deps)
        {
            _deps = deps ?? throw new ArgumentNullException(nameof(deps));
            _enabled.Checked = deps.Settings.Enabled;
            _autoApprove.Checked = deps.Settings.AutoApproveHardware;
            _rawScpi.Checked = deps.Settings.AllowRawScpi;
            _modelLabel.Text = "Model: " + deps.Settings.Model;
            _keyButton.Text = deps.KeyStore.Exists ? "Change API key…" : "Set API key…";
            AddNote(deps.Settings.Enabled
                ? "Assistant ready. Ask me to configure a signal, calculate it, or check the app state."
                : "Assistant is disabled. Tick “Enable assistant” to use it.");
        }

        // ---- settings handlers ----

        private void OnEnabledChanged()
        {
            if (_deps == null) return;
            _deps.Settings.Enabled = _enabled.Checked;
            _deps.SettingsStore.Save(_deps.Settings);
        }

        private void OnAutoApproveChanged()
        {
            if (_deps == null) return;
            _deps.Settings.AutoApproveHardware = _autoApprove.Checked;
            _deps.PolicyOptions.AutoApproveHardware = _autoApprove.Checked; // live effect on the policy
            _deps.SettingsStore.Save(_deps.Settings);
            if (_autoApprove.Checked)
                AddNote("Auto-approve hardware is ON. play_rf and connect_instrument still ask every time.");
        }

        private void OnRawScpiChanged()
        {
            if (_deps == null) return;
            _deps.Settings.AllowRawScpi = _rawScpi.Checked;
            _deps.Registry.SetEnabled(EsgSignalCreator.Assistant.Tools.GatedTools.SendRawScpiName, _rawScpi.Checked);
            _deps.SettingsStore.Save(_deps.Settings);
            AddNote(_rawScpi.Checked
                ? "Raw SCPI passthrough ENABLED — every command shows the literal text and asks for approval."
                : "Raw SCPI passthrough disabled.");
        }

        private void PromptAndStoreKey()
        {
            if (_deps == null) return;
            string key = PromptText(this, "Anthropic API key", "Paste your API key (stored encrypted with Windows DPAPI):");
            if (string.IsNullOrWhiteSpace(key)) return;
            _deps.KeyStore.Save(key.Trim());
            _keyButton.Text = "Change API key…";
            AddNote("API key saved (encrypted, per-user).");
        }

        // ---- send / run loop ----

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            // Enter sends; Shift+Enter inserts a newline.
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                if (!_busy) _ = SendAsync();
            }
        }

        private async Task SendAsync()
        {
            if (_deps == null || _busy) return;

            string text = _input.Text.Trim();
            if (text.Length == 0) return;

            if (!_deps.Settings.Enabled)
            {
                AddNote("Assistant is disabled. Tick “Enable assistant” first.");
                return;
            }

            if (!_deps.KeyStore.TryLoad(out string apiKey))
            {
                PromptAndStoreKey();
                if (!_deps.KeyStore.TryLoad(out apiKey)) { AddNote("No API key set."); return; }
            }

            _input.Clear();
            AddBubble("You", text, Color.FromArgb(230, 240, 255), Color.Black);

            SetBusy(true);
            _streamingLabel = null;
            _cts = new CancellationTokenSource();

            IClaudeClient client = _deps.ClientFactory(new ClaudeClientOptions
            {
                ApiKey = apiKey,
                Model = _deps.Settings.Model,
                FastModel = _deps.Settings.FastModel
            });

            var loop = new AgentLoop(client, _deps.Store, _deps.Dispatcher, new AgentLoopOptions
            {
                Tools = _deps.Registry.ToToolDefinitions(),
                Streaming = true,
                Model = _deps.Settings.Model,
                ReadOnlyClassifier = _deps.ReadOnlyClassifier,
                MaxHistoryMessages = _deps.MaxHistoryMessages
            });
            loop.TextDelta += OnTextDelta;
            loop.ToolRoundCompleted += OnToolRoundCompleted;

            try
            {
                ClaudeResponse response = await Task.Run(() => loop.RunTurnAsync(text, _cts.Token));
                // If nothing streamed (e.g. a tool-only turn ending in text not captured), show the text.
                if (_streamingLabel == null && !string.IsNullOrEmpty(response.Text()))
                    AddBubble("Claude", response.Text(), Color.FromArgb(245, 245, 245), Color.Black);
            }
            catch (OperationCanceledException)
            {
                AddNote("Stopped.");
            }
            catch (ClaudeApiException ex)
            {
                AddNote("API error: " + ex.Message);
                _deps.Log?.Invoke("Assistant API error: " + ex.Message);
            }
            catch (Exception ex)
            {
                AddNote("Error: " + ex.Message);
                _deps.Log?.Invoke("Assistant error: " + ex.Message);
            }
            finally
            {
                loop.TextDelta -= OnTextDelta;
                loop.ToolRoundCompleted -= OnToolRoundCompleted;
                (client as IDisposable)?.Dispose();
                _cts.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        private void OnTextDelta(string delta)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => AppendStreaming(delta))); return; }
            AppendStreaming(delta);
        }

        private void OnToolRoundCompleted(int round)
        {
            // AddNote already marshals to the UI thread.
            AddNote("Ran tool round " + round + ".");
        }

        private void AppendStreaming(string delta)
        {
            if (_streamingLabel == null)
                _streamingLabel = AddBubble("Claude", delta, Color.FromArgb(245, 245, 245), Color.Black);
            else
                _streamingLabel.Text += delta;
            ScrollToBottom();
        }

        // ---- IHardwareConfirmer: inline Approve/Decline card ----

        public Task<bool> ConfirmAsync(ToolConfirmationRequest request, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();

            void AddCard()
            {
                Panel card = BuildConfirmationCard(request, tcs);
                _transcript.Controls.Add(card);
                ReflowWidths();
                ScrollToBottom();
            }

            if (InvokeRequired) BeginInvoke((Action)AddCard); else AddCard();
            if (ct.CanBeCanceled) ct.Register(() => tcs.TrySetCanceled(ct));
            return tcs.Task;
        }

        private Panel BuildConfirmationCard(ToolConfirmationRequest request, TaskCompletionSource<bool> tcs)
        {
            string argsText = request.Args != null ? request.Args.ToString(Formatting.None) : "{}";
            if (argsText.Length > 400) argsText = argsText.Substring(0, 400) + "…";

            var card = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(255, 250, 225),
                Margin = new Padding(2, 4, 2, 4),
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var heading = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Approval required: " + request.ToolName + "  (" + request.Effect + ")"
            };
            var detail = new Label { AutoSize = true, Text = "Arguments: " + argsText, MaximumSize = new Size(CardWidth(), 0), Margin = new Padding(0, 4, 0, 6) };

            var approve = new Button { Text = "Approve", Width = 90, Height = 28, Margin = new Padding(0, 0, 8, 0) };
            var decline = new Button { Text = "Decline", Width = 90, Height = 28 };
            var result = new Label { AutoSize = true, Visible = false, Margin = new Padding(0, 6, 0, 0) };

            void Finish(bool approved)
            {
                approve.Enabled = decline.Enabled = false;
                result.Text = approved ? "✓ Approved" : "✗ Declined";
                result.ForeColor = approved ? Color.ForestGreen : Color.Firebrick;
                result.Visible = true;
                tcs.TrySetResult(approved);
            }
            approve.Click += (s, e) => Finish(true);
            decline.Click += (s, e) => Finish(false);

            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            buttons.Controls.Add(approve);
            buttons.Controls.Add(decline);

            var stack = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
            stack.Controls.Add(heading);
            stack.Controls.Add(detail);
            stack.Controls.Add(buttons);
            stack.Controls.Add(result);
            card.Controls.Add(stack);
            return card;
        }

        // ---- transcript helpers ----

        private Label AddBubble(string role, string text, Color back, Color fore)
        {
            var label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(CardWidth(), 0),
                Text = role + ":  " + text,
                BackColor = back,
                ForeColor = fore,
                Padding = new Padding(8),
                Margin = new Padding(2, 3, 2, 3)
            };
            _transcript.Controls.Add(label);
            ScrollToBottom();
            return label;
        }

        private void AddNote(string text)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => AddNote(text))); return; }
            var label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(CardWidth(), 0),
                Text = text,
                ForeColor = Color.DimGray,
                Font = new Font(Font, FontStyle.Italic),
                Margin = new Padding(2, 3, 2, 3)
            };
            _transcript.Controls.Add(label);
            ScrollToBottom();
        }

        // ---- demo hooks (#150): inject a canned transcript into the real pane for tutorial screenshots ----

        /// <summary>Add a mock "You" message bubble (tutorial-image generation only).</summary>
        public void AddDemoUserMessage(string text) => AddBubble("You", text, Color.FromArgb(230, 240, 255), Color.Black);

        /// <summary>Add a mock "Claude" message bubble (tutorial-image generation only).</summary>
        public void AddDemoAssistantMessage(string text) => AddBubble("Claude", text, Color.FromArgb(245, 245, 245), Color.Black);

        /// <summary>Add a mock italic note line (tutorial-image generation only).</summary>
        public void AddDemoNote(string text) => AddNote(text);

        /// <summary>Add a mock inline Approve/Decline confirmation card (tutorial-image generation only).</summary>
        public void AddDemoCard(string toolName, string argsText)
        {
            var card = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(255, 250, 225),
                Margin = new Padding(2, 4, 2, 4),
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            var stack = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            stack.Controls.Add(new Label { AutoSize = true, Text = "Approve hardware action:  " + toolName, Font = new Font(Font, FontStyle.Bold), MaximumSize = new Size(CardWidth(), 0) });
            stack.Controls.Add(new Label { AutoSize = true, Text = "Arguments: " + argsText, MaximumSize = new Size(CardWidth(), 0), Margin = new Padding(0, 4, 0, 6) });
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            buttons.Controls.Add(new Button { Text = "Approve", AutoSize = true });
            buttons.Controls.Add(new Button { Text = "Decline", AutoSize = true });
            stack.Controls.Add(buttons);
            card.Controls.Add(stack);
            _transcript.Controls.Add(card);
            ReflowWidths();
        }

        private int CardWidth() => Math.Max(120, _transcript.ClientSize.Width - 36);

        private void ReflowWidths()
        {
            int w = CardWidth();
            foreach (Control c in _transcript.Controls)
                if (c is Label lbl) lbl.MaximumSize = new Size(w, 0);
        }

        private void ScrollToBottom()
        {
            if (_transcript.Controls.Count > 0)
                _transcript.ScrollControlIntoView(_transcript.Controls[_transcript.Controls.Count - 1]);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _send.Enabled = !busy;
            _stop.Enabled = busy;
            _input.Enabled = !busy;
        }

        // ---- a tiny modal text prompt (used for the API key) ----

        private static string PromptText(IWin32Window owner, string title, string prompt)
        {
            using (var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 120)
            })
            {
                var label = new Label { Text = prompt, Location = new Point(12, 12), Size = new Size(396, 32) };
                var box = new TextBox { Location = new Point(12, 50), Size = new Size(396, 22), UseSystemPasswordChar = true };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(252, 84), Size = new Size(75, 26) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(333, 84), Size = new Size(75, 26) };
                form.Controls.Add(label);
                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }
    }
}
