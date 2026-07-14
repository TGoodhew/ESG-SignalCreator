using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Ui.Plots
{
    /// <summary>
    /// Renders a waveform plot view to a PNG file <b>off-screen</b> (no visible GUI), so the
    /// tutorial-image harness can regenerate the doc plots deterministically (issue #150). Uses the same
    /// <see cref="PlotSeries"/> rendering as the interactive <see cref="PlotPane"/>, so a doc image always
    /// matches what the app actually draws.
    /// </summary>
    public static class PlotImageRenderer
    {
        /// <summary>Render <paramref name="view"/> of <paramref name="wf"/> to a PNG at <paramref name="path"/>.</summary>
        public static void SaveImage(string path, PlotPane.ViewType view, WaveformModel wf,
            int samplesPerSymbol, Size size)
        {
            using (var chart = new Chart { Width = size.Width, Height = size.Height, BackColor = Color.White })
            {
                PlotSeries.ConfigureChart(chart);
                PlotSeries.Render(chart, view, wf, samplesPerSymbol);
                chart.SaveImage(path, ChartImageFormat.Png);
            }
        }
    }
}
