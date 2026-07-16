using System.Collections.Generic;
using System.Linq;
using EsgSignalCreator.Model;
using EsgSignalCreator.Verify;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    /// <summary>
    /// Guards the closed-loop verification battery (#227): it must cover every source personality and
    /// every entry must build a valid waveform offline. Freezing the id set turns a silently-uncovered
    /// new personality into a loud failure — the exact fail-silent case the #225 harness review found.
    /// </summary>
    public class VerificationBatteryTests
    {
        // Mirrors the app's PersonalityRegistry. If a personality is added/removed, this list — and the
        // battery — must be updated together; that is the point of the guard.
        private static readonly string[] ExpectedPersonalities =
        {
            "cw", "multitone", "multitone-distortion", "multi-carrier", "custom-mod", "pulse", "jitter",
            "gsm-edge", "bluetooth", "wcdma-fdd", "wcdma-hspa", "cdma2000", "td-scdma", "lte-fdd", "lte-tdd",
            "wlan-80211", "wimax-fixed", "wimax-mobile", "t-dmb", "digital-video", "broadcast-radio", "awgn",
            "import-iq",
        };

        [Fact]
        public void Battery_covers_exactly_the_expected_personalities()
        {
            Assert.Equal(
                ExpectedPersonalities.OrderBy(s => s),
                VerificationBattery.PersonalityIds.OrderBy(s => s));
        }

        [Fact]
        public void Battery_entries_match_the_declared_id_list_in_order()
        {
            string[] built = VerificationBattery.All(1e6).Select(e => e.Id).ToArray();
            Assert.Equal(VerificationBattery.PersonalityIds, built);
        }

        public static IEnumerable<object[]> Ids =>
            VerificationBattery.PersonalityIds.Select(id => new object[] { id });

        [Theory]
        [MemberData(nameof(Ids))]
        public void Every_battery_signal_builds_a_valid_waveform_offline(string id)
        {
            BatteryEntry entry = VerificationBattery.Get(id, 1e6);
            Assert.NotNull(entry);

            WaveformModel wf = entry.Build(null);
            Assert.NotNull(wf);
            Assert.True(wf.Length > 0, $"{id} produced an empty waveform.");
            Assert.True(wf.SampleRateHz > 0, $"{id} has a non-positive sample rate.");

            for (int k = 0; k < wf.Length; k++)
            {
                Assert.True(IsFinite(wf.I[k]), $"{id} I[{k}] is not finite.");
                Assert.True(IsFinite(wf.Q[k]), $"{id} Q[{k}] is not finite.");
            }
        }

        [Fact]
        public void Get_returns_null_for_an_unknown_id()
        {
            Assert.Null(VerificationBattery.Get("does-not-exist", 1e6));
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
