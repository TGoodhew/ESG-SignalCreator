using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Ui.Plots
{
    /// <summary>
    /// A reusable verification plot: a chart with a view-type dropdown and rubber-band zoom
    /// (UX brief §7). One pane can show any supported view of the current waveform; the host docks
    /// up to three of them. P1 ships I/Q-vs-time and Spectrum; further views (constellation, eye,
    /// CCDF) plug in via <see cref="ViewType"/>.
    /// </summary>
    public sealed class PlotPane : UserControl
    {
        public enum ViewType
        {
            IqVsTime,
            Spectrum,
            Constellation,
            Ccdf,
            Eye
        }

        private readonly Chart _chart;
        private readonly ComboBox _view;
        private WaveformModel _waveform;
        private int _samplesPerSymbol;

        public PlotPane()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2) };
            _view = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _view.Items.AddRange(new object[] { "I / Q vs time", "Spectrum (FFT)", "Constellation", "CCDF", "Eye diagram" });
            _view.SelectedIndex = 0;
            _view.SelectedIndexChanged += (s, e) => Render();
            top.Controls.Add(new Label { Text = "View:", AutoSize = true, Margin = new Padding(2, 6, 2, 0) });
            top.Controls.Add(_view);
            var reset = new Button { Text = "Reset zoom", AutoSize = true };
            reset.Click += (s, e) => ResetZoom();
            top.Controls.Add(reset);

            // The MS Chart throws "Height must be greater than 0px" if it is ever laid out at zero
            // size (which happens transiently during form construction); a small minimum avoids it.
            _chart = new Chart { Dock = DockStyle.Fill, MinimumSize = new Size(10, 10) };
            PlotSeries.ConfigureChart(_chart); // chart area + legend + rubber-band zoom (shared with the image renderer)

            Controls.Add(_chart);
            Controls.Add(top);
        }

        /// <summary>The currently selected view.</summary>
        public ViewType SelectedView
        {
            get
            {
                switch (_view.SelectedIndex)
                {
                    case 1: return ViewType.Spectrum;
                    case 2: return ViewType.Constellation;
                    case 3: return ViewType.Ccdf;
                    case 4: return ViewType.Eye;
                    default: return ViewType.IqVsTime;
                }
            }
            set { _view.SelectedIndex = (int)value; }
        }

        /// <summary>Show a waveform (re-renders the current view).</summary>
        public void Show(WaveformModel waveform)
        {
            Show(waveform, 0);
        }

        /// <summary>Show a waveform, hinting samples-per-symbol so the eye diagram folds correctly.</summary>
        public void Show(WaveformModel waveform, int samplesPerSymbol)
        {
            _waveform = waveform;
            _samplesPerSymbol = samplesPerSymbol;
            Render();
        }

        private void ResetZoom()
        {
            ChartArea a = _chart.ChartAreas[0];
            a.AxisX.ScaleView.ZoomReset(0);
            a.AxisY.ScaleView.ZoomReset(0);
        }

        // Rendering lives in PlotSeries so the interactive pane and the headless image renderer draw
        // identically (issue #150).
        private void Render() => PlotSeries.Render(_chart, SelectedView, _waveform, _samplesPerSymbol);
    }
}
