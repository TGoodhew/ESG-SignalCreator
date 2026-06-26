using System.Linq;
using EsgSignalCreator.Sequencing;
using Xunit;

namespace EsgSignalCreator.Tests.Sequencing
{
    public class SequencingTests
    {
        private static Sequence Sample()
        {
            var seq = new Sequence { Name = "S" };
            seq.Steps.Add(new SequenceStep { Waveform = "SEG1", Repeat = 3, PowerDb = -2.5, Wait = WaitMode.TrigA, MarkerEnabled = true, Flags = new[] { true, false, true, false }, IdleSamples = 100 });
            seq.Steps.Add(new SequenceStep { Waveform = "SEG2", Repeat = 1, GoTo = 0, Wait = WaitMode.Off });
            return seq;
        }

        [Fact]
        public void Script_round_trips_through_format_and_parse()
        {
            Sequence original = Sample();
            string text = SequenceScript.Format(original);
            Sequence parsed = SequenceScript.Parse(text);

            Assert.Equal(original.Steps.Count, parsed.Steps.Count);
            for (int i = 0; i < original.Steps.Count; i++)
            {
                SequenceStep a = original.Steps[i], b = parsed.Steps[i];
                Assert.Equal(a.Waveform, b.Waveform);
                Assert.Equal(a.Repeat, b.Repeat);
                Assert.Equal(a.PowerDb, b.PowerDb, 3);
                Assert.Equal(a.Wait, b.Wait);
                Assert.Equal(a.GoTo, b.GoTo);
                Assert.Equal(a.MarkerEnabled, b.MarkerEnabled);
                Assert.Equal(a.IdleSamples, b.IdleSamples);
                Assert.Equal(a.Flags, b.Flags);
            }
        }

        [Fact]
        public void CompilePlayOrder_honors_repeat_counts()
        {
            var seq = new Sequence();
            seq.Steps.Add(new SequenceStep { Waveform = "A", Repeat = 2 });
            seq.Steps.Add(new SequenceStep { Waveform = "B", Repeat = 1 });

            Assert.Equal(new[] { "A", "A", "B" }, seq.CompilePlayOrder().ToArray());
        }

        [Fact]
        public void CompilePlayOrder_stops_on_an_infinite_repeat()
        {
            var seq = new Sequence();
            seq.Steps.Add(new SequenceStep { Waveform = "A", Repeat = SequenceStep.InfiniteRepeat });
            seq.Steps.Add(new SequenceStep { Waveform = "B" });

            // Infinite step emits once and halts (no runaway, B not reached).
            Assert.Equal(new[] { "A" }, seq.CompilePlayOrder(maxEmitted: 50).ToArray());
        }

        [Fact]
        public void Batch_expand_produces_the_sweep_points()
        {
            var points = BatchCompiler.Expand("cn", 10, 30, 10);
            Assert.Equal(3, points.Count);
            Assert.Equal(new[] { 10.0, 20.0, 30.0 }, points.Select(p => p.Value).ToArray());
            Assert.Equal("cn_10", points[0].Name);
        }

        [Fact]
        public void Batch_expand_with_no_step_yields_a_single_point()
        {
            Assert.Single(BatchCompiler.Expand("x", 5, 5, 0));
        }
    }
}
