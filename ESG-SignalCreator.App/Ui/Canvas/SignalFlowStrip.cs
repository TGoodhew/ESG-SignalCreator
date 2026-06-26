using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EsgSignalCreator.Ui.Canvas
{
    /// <summary>
    /// The signal-flow canvas (UX brief §2/§3): a left-to-right block diagram
    /// <c>Source → [AWGN] → [CFR] → [Filter] → Output</c>. Each block is a clickable card; the host
    /// swaps the centre parameter panel in response to <see cref="BlockSelected"/>. Disabled
    /// (optional) blocks render dimmed.
    /// </summary>
    public sealed class SignalFlowStrip : UserControl
    {
        public sealed class Block
        {
            public Block(string id, string title, bool enabled = true)
            {
                Id = id;
                Title = title;
                Enabled = enabled;
            }

            public string Id { get; }
            public string Title { get; set; }
            public bool Enabled { get; set; }
        }

        private readonly FlowLayoutPanel _flow;
        private readonly List<Block> _blocks = new List<Block>();
        private string _selectedId;

        public event EventHandler<string> BlockSelected;

        public SignalFlowStrip()
        {
            Height = 64;
            Dock = DockStyle.Top;
            BackColor = Color.White;
            _flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 10, 8, 8) };
            Controls.Add(_flow);

            SetBlocks(new[]
            {
                new Block("source", "Source"),
                new Block("awgn", "AWGN", enabled: false),
                new Block("cfr", "CFR", enabled: false),
                new Block("filter", "Filter", enabled: false),
                new Block("output", "Output"),
            });
        }

        /// <summary>The id of the currently selected block (defaults to the first).</summary>
        public string SelectedBlockId => _selectedId;

        public void SetBlocks(IEnumerable<Block> blocks)
        {
            _blocks.Clear();
            _blocks.AddRange(blocks);
            Rebuild();
            if (_blocks.Count > 0) Select(_blocks[0].Id, raise: false);
        }

        /// <summary>Enable/disable an optional block (e.g. when an impairment is toggled on).</summary>
        public void SetEnabled(string id, bool enabled)
        {
            Block b = _blocks.FirstOrDefault(x => x.Id == id);
            if (b == null) return;
            b.Enabled = enabled;
            Rebuild();
        }

        private void Rebuild()
        {
            _flow.Controls.Clear();
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (i > 0) _flow.Controls.Add(Arrow());
                _flow.Controls.Add(Card(_blocks[i]));
            }
        }

        private Label Arrow() => new Label
        {
            Text = "▶",
            AutoSize = true,
            ForeColor = Color.Silver,
            Margin = new Padding(2, 12, 2, 0),
            Font = new Font(Font.FontFamily, 11f)
        };

        private Panel Card(Block block)
        {
            bool selected = block.Id == _selectedId;
            var card = new Panel
            {
                Width = 96,
                Height = 40,
                Margin = new Padding(2, 2, 2, 2),
                BackColor = !block.Enabled ? Color.Gainsboro : (selected ? Color.FromArgb(0x33, 0x66, 0xCC) : Color.FromArgb(0xE8, 0xEF, 0xFB)),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = block.Id
            };
            var label = new Label
            {
                Text = block.Title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = !block.Enabled ? Color.Gray : (selected ? Color.White : Color.FromArgb(0x22, 0x33, 0x55)),
                Cursor = Cursors.Hand
            };
            card.Controls.Add(label);

            EventHandler click = (s, e) => Select(block.Id, raise: true);
            card.Click += click;
            label.Click += click;
            return card;
        }

        private void Select(string id, bool raise)
        {
            _selectedId = id;
            Rebuild();
            if (raise) BlockSelected?.Invoke(this, id);
        }
    }
}
