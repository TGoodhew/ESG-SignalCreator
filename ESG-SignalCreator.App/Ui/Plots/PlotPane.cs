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
            Ccdf
        }

        private readonly Chart _chart;
        private readonly ComboBox _view;
        private WaveformModel _waveform;

        public PlotPane()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2) };
            _view = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _view.Items.AddRange(new object[] { "I / Q vs time", "Spectrum (FFT)", "Constellation", "CCDF" });
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
            var area = new ChartArea("main");
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            // Rubber-band zoom (legacy Signal Studio Tools->Zoom).
            area.CursorX.IsUserSelectionEnabled = true;
            area.CursorY.IsUserSelectionEnabled = true;
            area.AxisX.ScaleView.Zoomable = true;
            area.AxisY.ScaleView.Zoomable = true;
            _chart.ChartAreas.Add(area);
            _chart.Legends.Add(new Legend("legend") { Docking = Docking.Top });

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
                    default: return ViewType.IqVsTime;
                }
            }
            set { _view.SelectedIndex = (int)value; }
        }

        /// <summary>Show a waveform (re-renders the current view).</summary>
        public void Show(WaveformModel waveform)
        {
            _waveform = waveform;
            Render();
        }

        private void ResetZoom()
        {
            ChartArea a = _chart.ChartAreas[0];
            a.AxisX.ScaleView.ZoomReset(0);
            a.AxisY.ScaleView.ZoomReset(0);
        }

        private void Render()
        {
            _chart.Series.Clear();
            _chart.Titles.Clear();
            ChartArea a = _chart.ChartAreas[0];
            a.AxisY.Minimum = double.NaN;
            a.AxisY.Maximum = double.NaN;

            if (_waveform == null) return;

            switch (SelectedView)
            {
                case ViewType.Spectrum: RenderSpectrum(); break;
                case ViewType.Constellation: RenderConstellation(); break;
                case ViewType.Ccdf: RenderCcdf(); break;
                default: RenderIq(); break;
            }
        }

        private void RenderIq()
        {
            WaveformModel wf = _waveform;
            _chart.Titles.Add("I / Q  (time domain)");
            _chart.ChartAreas[0].AxisX.Title = "Sample";
            _chart.ChartAreas[0].AxisY.Title = "Amplitude (norm.)";

            var si = new Series("I") { ChartType = SeriesChartType.FastLine, Color = Color.RoyalBlue };
            var sq = new Series("Q") { ChartType = SeriesChartType.FastLine, Color = Color.OrangeRed };
            int step = Math.Max(1, wf.Length / 2000); // decimate for responsiveness
            for (int n = 0; n < wf.Length; n += step)
            {
                si.Points.AddXY(n, wf.I[n]);
                sq.Points.AddXY(n, wf.Q[n]);
            }
            _chart.Series.Add(si);
            _chart.Series.Add(sq);
        }

        private void RenderSpectrum()
        {
            WaveformModel wf = _waveform;
            _chart.Titles.Add("Baseband spectrum (centered on carrier)");
            ChartArea a = _chart.ChartAreas[0];
            a.AxisX.Title = "Frequency offset (MHz)";
            a.AxisY.Title = "Magnitude (dB)";
            a.AxisY.Minimum = -120;
            a.AxisY.Maximum = 5;

            double[] iD = ToDouble(wf.I);
            double[] qD = ToDouble(wf.Q);
            Fft.MagnitudeSpectrumDb(iD, qD, wf.SampleRateHz, out double[] f, out double[] mag);
            var s = new Series("Spectrum") { ChartType = SeriesChartType.FastLine, Color = Color.SeaGreen };
            for (int k = 0; k < f.Length; k++) s.Points.AddXY(f[k] / 1e6, mag[k]);
            _chart.Series.Add(s);
        }

        private void RenderConstellation()
        {
            WaveformModel wf = _waveform;
            _chart.Titles.Add("Constellation (I vs Q)");
            ChartArea a = _chart.ChartAreas[0];
            a.AxisX.Title = "I";
            a.AxisY.Title = "Q";
            var s = new Series("Constellation") { ChartType = SeriesChartType.Point, Color = Color.MediumVioletRed, MarkerSize = 3 };
            int step = Math.Max(1, wf.Length / 4000);
            for (int n = 0; n < wf.Length; n += step) s.Points.AddXY(wf.I[n], wf.Q[n]);
            _chart.Series.Add(s);
        }

        private void RenderCcdf()
        {
            WaveformModel wf = _waveform;
            _chart.Titles.Add("CCDF (instantaneous power above average)");
            ChartArea a = _chart.ChartAreas[0];
            a.AxisX.Title = "dB above average";
            a.AxisY.Title = "Probability";
            a.AxisY.IsLogarithmic = true;
            Ccdf.Curve(ToDouble(wf.I), ToDouble(wf.Q), out double[] db, out double[] prob);
            var s = new Series("CCDF") { ChartType = SeriesChartType.FastLine, Color = Color.DarkSlateBlue };
            for (int k = 0; k < db.Length; k++)
                if (prob[k] > 0) s.Points.AddXY(db[k], prob[k]);
            _chart.Series.Add(s);
        }

        private static double[] ToDouble(float[] x)
        {
            var d = new double[x.Length];
            for (int n = 0; n < x.Length; n++) d[n] = x[n];
            return d;
        }
    }
}
