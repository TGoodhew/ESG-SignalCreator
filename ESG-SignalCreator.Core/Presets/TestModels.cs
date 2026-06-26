using System;
using System.Collections.Generic;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.Multitone;

namespace EsgSignalCreator.Presets
{
    /// <summary>
    /// The built-in catalogue of test-model presets (GitHub issue #57). Each entry pairs a name
    /// with a factory that constructs a personality, builds its configuration, applies it via
    /// <see cref="IWaveformPersonality.LoadConfig"/>, and returns it ready to generate.
    /// </summary>
    public static class TestModels
    {
        /// <summary>All built-in presets. Names are unique within the list.</summary>
        public static IReadOnlyList<TestModel> All { get; } = new List<TestModel>
        {
            new TestModel("CW (1 MHz offset)", CreateCw),
            new TestModel("Multitone IMD (two-tone)", CreateMultitone),
            new TestModel("Single-carrier 16-QAM", CreateSingleCarrier16Qam),
            new TestModel("AWGN only", CreateAwgn),
        };

        /// <summary>
        /// Find a preset by exact <see cref="TestModel.Name"/> (ordinal comparison).
        /// </summary>
        /// <returns>The matching <see cref="TestModel"/>, or <c>null</c> if none matches.</returns>
        public static TestModel Find(string name)
        {
            if (name == null) return null;
            foreach (TestModel m in All)
            {
                if (string.Equals(m.Name, name, StringComparison.Ordinal))
                    return m;
            }
            return null;
        }

        /// <summary>A clean CW carrier offset 1 MHz from baseband centre.</summary>
        private static IWaveformPersonality CreateCw()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 10e6,
                Length = 4000,
                FreqOffsetHz = 1e6,
                AmplitudeScale = 1.0,
                PhaseDeg = 0.0,
            };
            var p = new CwPersonality();
            p.LoadConfig(cfg);
            return p;
        }

        /// <summary>
        /// A classic two-tone IMD stimulus: two equal-power tones symmetric about centre,
        /// spaced 1 MHz apart (so ±500 kHz).
        /// </summary>
        private static IWaveformPersonality CreateMultitone()
        {
            var cfg = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 16384,
                Phase = PhaseStrategy.Equal,
                Tones = MultitonePersonality.AutoSpacing(
                    toneCount: 2,
                    spacingHz: 1e6,
                    centerOffsetHz: 0.0,
                    powerDbPerTone: 0.0),
            };
            var p = new MultitonePersonality();
            p.LoadConfig(cfg);
            return p;
        }

        /// <summary>A single-carrier 16-QAM signal with RRC shaping.</summary>
        private static IWaveformPersonality CreateSingleCarrier16Qam()
        {
            var cfg = new CustomModConfig
            {
                Modulation = Modulation.QAM16,
                SymbolRateHz = 1e6,
                SamplesPerSymbol = 8,
                Shape = PulseShape.RootRaisedCosine,
                Alpha = 0.35,
                FilterSpanSymbols = 8,
                SymbolCount = 512,
                Data = DataSource.PN9,
            };
            var p = new CustomModPersonality();
            p.LoadConfig(cfg);
            return p;
        }

        /// <summary>Band-limited additive white Gaussian noise on its own.</summary>
        private static IWaveformPersonality CreateAwgn()
        {
            var cfg = new AwgnConfig
            {
                SampleRateHz = 10e6,
                Length = 32768,
                NoiseBandwidthHz = 5e6,
                CrestFactorDb = 12.0,
                RandomSeed = 12345,
            };
            var p = new AwgnPersonality();
            p.LoadConfig(cfg);
            return p;
        }
    }
}
