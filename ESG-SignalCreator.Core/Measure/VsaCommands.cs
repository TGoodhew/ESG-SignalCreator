using System.Globalization;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// Builds the E4406A's MEASure / CONFigure / READ / FETCh command strings (Programmer's Guide
    /// "four command verbs"). The optional <c>[n]</c> result index is appended to the mnemonic
    /// (e.g. <c>:FETCh:SPECtrum4?</c>); <c>n &lt;= 1</c> selects the scalar result set and omits it.
    /// </summary>
    public static class VsaCommands
    {
        /// <summary>One-shot with factory defaults: configure + initiate + block + return.</summary>
        public static string Measure(string root, int n = 1) => ":MEASure:" + root + Index(n) + "?";

        /// <summary>Initiate (INIT + FETCh) honoring persistent SENSe/CALCulate settings, return results.</summary>
        public static string Read(string root, int n = 1) => ":READ:" + root + Index(n) + "?";

        /// <summary>Return data from the most recent measurement without re-measuring.</summary>
        public static string Fetch(string root, int n = 1) => ":FETCh:" + root + Index(n) + "?";

        /// <summary>Set up the measurement to defaults and go to single mode without initiating.</summary>
        public static string Configure(string root) => ":CONFigure:" + root;

        private static string Index(int n) => n <= 1 ? string.Empty : n.ToString(CultureInfo.InvariantCulture);
    }
}
