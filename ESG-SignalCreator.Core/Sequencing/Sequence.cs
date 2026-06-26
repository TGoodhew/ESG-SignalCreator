using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EsgSignalCreator.Sequencing
{
    /// <summary>An ordered list of <see cref="SequenceStep"/>s plus a simple play-order compiler.</summary>
    [DataContract]
    public sealed class Sequence
    {
        [DataMember(Name = "name")] public string Name { get; set; } = "Sequence";

        [DataMember(Name = "steps")] public List<SequenceStep> Steps { get; set; } = new List<SequenceStep>();

        /// <summary>
        /// Resolve the linear order of waveform names that would play, honoring <c>Repeat</c> and
        /// unconditional <c>GoTo</c> jumps, bounded by <paramref name="maxEmitted"/> so an infinite
        /// loop can't run away. Triggered waits and event-jumps are treated as "advance to next"
        /// (they depend on runtime input, which is not modelled here).
        /// </summary>
        public IReadOnlyList<string> CompilePlayOrder(int maxEmitted = 100000)
        {
            var order = new List<string>();
            if (Steps.Count == 0) return order;

            int index = 0;
            int guard = 0;
            while (index >= 0 && index < Steps.Count && order.Count < maxEmitted && guard < maxEmitted)
            {
                guard++;
                SequenceStep step = Steps[index];
                int reps = step.Repeat == SequenceStep.InfiniteRepeat ? 1 : (step.Repeat < 1 ? 1 : step.Repeat);
                for (int r = 0; r < reps && order.Count < maxEmitted; r++)
                    order.Add(string.IsNullOrEmpty(step.SubSequence) ? step.Waveform : "[" + step.SubSequence + "]");

                if (step.Repeat == SequenceStep.InfiniteRepeat) break; // would loop forever on this step
                index = step.GoTo >= 0 ? step.GoTo : index + 1;
            }
            return order;
        }
    }
}
