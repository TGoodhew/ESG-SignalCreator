using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using EsgSignalCreator.Verify;

namespace EsgSignalCreator.Ui.Verify
{
    /// <summary>
    /// Troubleshooting dialog (issue #130) shown when the install self-test fails: for each failed check
    /// it lists a likely cause and ordered suggestions, from <see cref="VerificationGuidanceBook"/>.
    /// </summary>
    public sealed class VerificationGuidanceForm : Form
    {
        public VerificationGuidanceForm(IReadOnlyList<VerificationGuidance> guidance)
        {
            Text = "Install verification — troubleshooting";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(660, 460);
            MinimizeBox = false;

            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window,
                Font = new Font(FontFamily.GenericSansSerif, 9.5f),
                Text = Build(guidance)
            };

            var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 34 };
            close.Click += (s, e) => Close();

            Controls.Add(box);   // fills the remaining area
            Controls.Add(close); // docked to the bottom (added last -> laid out first)
            AcceptButton = close;
            CancelButton = close;
        }

        private static string Build(IReadOnlyList<VerificationGuidance> guidance)
        {
            if (guidance == null || guidance.Count == 0)
                return "All checks passed — nothing to troubleshoot.";

            var sb = new StringBuilder();
            sb.AppendLine(guidance.Count + " check(s) failed. Suggested troubleshooting:").AppendLine();
            foreach (VerificationGuidance g in guidance)
            {
                sb.AppendLine("■ " + g.Check);
                sb.AppendLine("    Likely cause: " + g.Cause);
                foreach (string s in g.Suggestions) sb.AppendLine("    • " + s);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Show the guidance modally over <paramref name="owner"/>.</summary>
        public static void ShowFor(IWin32Window owner, IReadOnlyList<VerificationGuidance> guidance)
        {
            using (var form = new VerificationGuidanceForm(guidance))
                form.ShowDialog(owner);
        }
    }
}
