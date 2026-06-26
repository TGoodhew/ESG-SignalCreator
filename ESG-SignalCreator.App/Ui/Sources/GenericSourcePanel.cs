using System;
using System.Reflection;
using System.Windows.Forms;
using EsgSignalCreator.Personalities;

namespace EsgSignalCreator.Ui.Sources
{
    /// <summary>
    /// A source panel that edits any personality's configuration object through a
    /// <see cref="PropertyGrid"/>. Because every personality's config is a plain serializable
    /// object with public properties, this gives an instant editor for all of them without a
    /// hand-built panel per personality. The shared header's sample rate and solved length are
    /// written back into the config (when it exposes matching properties) on Calculate.
    /// </summary>
    public sealed class GenericSourcePanel : SourcePanelBase
    {
        private readonly IWaveformPersonality _personality;
        private readonly PropertyGrid _grid;
        private object _config;

        public GenericSourcePanel(IWaveformPersonality personality)
        {
            _personality = personality ?? throw new ArgumentNullException(nameof(personality));
            _config = personality.GetConfig();

            _grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = _config, PropertySort = PropertySort.Categorized };
            Body.Controls.Add(_grid);

            // Seed the header from the config's own sample rate / length if present.
            double fs = GetConfigDouble("SampleRateHz", 10e6);
            int len = (int)GetConfigDouble("Length", 4096);
            SetHeader(personality.DisplayName, fs, len);
        }

        public override string PersonalityId => _personality.Id;

        public override object GetConfig() => _config;

        public override void LoadConfig(object cfg)
        {
            _config = cfg;
            _grid.SelectedObject = _config;
            _personality.LoadConfig(cfg);
        }

        public override IWaveformPersonality BuildPersonality()
        {
            // The header is the source of truth for sample rate and length.
            SetConfig("SampleRateHz", SampleRateHz);
            SetConfig("Length", SolveSampleCount());
            _grid.Refresh();
            _personality.LoadConfig(_config);
            return _personality;
        }

        private double GetConfigDouble(string name, double fallback)
        {
            PropertyInfo p = _config.GetType().GetProperty(name);
            if (p == null) return fallback;
            object v = p.GetValue(_config);
            return v == null ? fallback : Convert.ToDouble(v);
        }

        private void SetConfig(string name, object value)
        {
            PropertyInfo p = _config.GetType().GetProperty(name);
            if (p == null || !p.CanWrite) return;
            object converted = Convert.ChangeType(value, p.PropertyType);
            p.SetValue(_config, converted);
        }
    }
}
