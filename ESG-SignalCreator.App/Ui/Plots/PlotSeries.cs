using System;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Ui.Plots
{
    /// <summary>
    /// Builds the chart series for each waveform view. Extracted from <see cref="PlotPane"/> so the same
    /// rendering drives both the interactive pane and the headless image renderer (<see
    /// cref="PlotImageRenderer"/>, issue #150) — one source of truth for what each plot looks like.
    /// </summary>
    internal static class PlotSeries
    {
        /// <summary>Configure a fresh chart area + legend the way the plots expect (grid, zoom off/on-safe).</summary>
        public static void ConfigureChart(Chart chart)
        {
            var area = new ChartArea("main");
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            area.CursorX.IsUserSelectionEnabled = true;
            area.CursorY.IsUserSelectionEnabled = true;
            area.AxisX.ScaleView.Zoomable = true;
            area.AxisY.ScaleView.Zoomable = true;
            chart.ChartAreas.Add(area);
            chart.Legends.Add(new Legend("legend") { Docking = Docking.Top });
        }

        /// <summary>Clear and render <paramref name="view"/> of <paramref name="wf"/> into <paramref name="chart"/>.</summary>
        public static void Render(Chart chart, PlotPane.ViewType view, WaveformModel wf, int samplesPerSymbol)
        {
            chart.Series.Clear();
            chart.Titles.Clear();
            ChartArea a = chart.ChartAreas[0];
            a.AxisY.Minimum = double.NaN;
            a.AxisY.Maximum = double.NaN;
            a.AxisY.IsLogarithmic = false;

            if (wf == null) return;

            switch (view)
            {
                case PlotPane.ViewType.Spectrum: RenderSpectrum(chart, wf); break;
                case PlotPane.ViewType.Constellation: RenderConstellation(chart, wf); break;
                case PlotPane.ViewType.Ccdf: RenderCcdf(chart, wf); break;
                case PlotPane.ViewType.Eye: RenderEye(chart, wf, samplesPerSymbol); break;
                default: RenderIq(chart, wf); break;
            }
        }

        private static void RenderIq(Chart chart, WaveformModel wf)
        {
            chart.Titles.Add("I / Q  (time domain)");
            chart.ChartAreas[0].AxisX.Title = "Sample";
            chart.ChartAreas[0].AxisY.Title = "Amplitude (norm.)";

            var si = new Series("I") { ChartType = SeriesChartType.FastLine, Color = Color.RoyalBlue };
            var sq = new Series("Q") { ChartType = SeriesChartType.FastLine, Color = Color.OrangeRed };
            int step = Math.Max(1, wf.Length / 2000);
            for (int n = 0; n < wf.Length; n += step)
            {
                si.Points.AddXY(n, wf.I[n]);
                sq.Points.AddXY(n, wf.Q[n]);
            }
            chart.Series.Add(si);
            chart.Series.Add(sq);
        }

        private static void RenderSpectrum(Chart chart, WaveformModel wf)
        {
            chart.Titles.Add("Baseband spectrum (centered on carrier)");
            ChartArea a = chart.ChartAreas[0];
            a.AxisX.Title = "Frequency offset (MHz)";
            a.AxisY.Title = "Magnitude (dB)";
            a.AxisY.Minimum = -120;
            a.AxisY.Maximum = 5;

            double[] iD = ToDouble(wf.I);
            double[] qD = ToDouble(wf.Q);
            Fft.MagnitudeSpectrumDb(iD, qD, wf.SampleRateHz, out double[] f, out double[] mag);
            var s = new Series("Spectrum") { ChartType = SeriesChartType.FastLine, Color = Color.SeaGreen };
            for (int k = 0; k < f.Length; k++) s.Points.AddXY(f[k] / 1e6, mag[k]);
            chart.Series.Add(s);
        }

        private static void RenderConstellation(Chart chart, WaveformModel wf)
        {
            chart.Titles.Add("Constellation (I vs Q)");
            ChartArea a = chart.ChartAreas[0];
            a.AxisX.Title = "I";
            a.AxisY.Title = "Q";
            var s = new Series("Constellation") { ChartType = SeriesChartType.Point, Color = Color.MediumVioletRed, MarkerSize = 3 };
            int step = Math.Max(1, wf.Length / 4000);
            for (int n = 0; n < wf.Length; n += step) s.Points.AddXY(wf.I[n], wf.Q[n]);
            chart.Series.Add(s);
        }

        private static void RenderCcdf(Chart chart, WaveformModel wf)
        {
            double[] iD = ToDouble(wf.I), qD = ToDouble(wf.Q);
            double papr = Ccdf.PaprDb(iD, qD);
            chart.Titles.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "CCDF   (PAPR {0:0.##} dB)", papr));
            ChartArea a = chart.ChartAreas[0];
            a.AxisX.Title = "dB above average";
            a.AxisY.Title = "Probability";
            a.AxisY.IsLogarithmic = true;
            Ccdf.Curve(iD, qD, out double[] db, out double[] prob);
            var s = new Series("CCDF") { ChartType = SeriesChartType.FastLine, Color = Color.DarkSlateBlue };
            for (int k = 0; k < db.Length; k++)
                if (prob[k] > 0) s.Points.AddXY(db[k], prob[k]);
            chart.Series.Add(s);
        }

        private static void RenderEye(Chart chart, WaveformModel wf, int samplesPerSymbol)
        {
            chart.Titles.Add("Eye diagram (I)");
            ChartArea a = chart.ChartAreas[0];
            a.AxisX.Title = "Sample in window";
            a.AxisY.Title = "I";

            int sps = samplesPerSymbol > 0 ? samplesPerSymbol : Math.Max(8, Math.Min(128, wf.Length / 200));
            int win = sps * 2;
            if (win < 2 || win > wf.Length) return;

            var color = Color.FromArgb(60, Color.Teal);
            int traces = 0;
            for (int start = 0; start + win <= wf.Length && traces < 200; start += sps, traces++)
            {
                var s = new Series("eye" + traces) { ChartType = SeriesChartType.FastLine, Color = color, IsVisibleInLegend = false };
                for (int k = 0; k < win; k++) s.Points.AddXY(k, wf.I[start + k]);
                chart.Series.Add(s);
            }
        }

        private static double[] ToDouble(float[] x)
        {
            var d = new double[x.Length];
            for (int n = 0; n < x.Length; n++) d[n] = x[n];
            return d;
        }
    }
}
