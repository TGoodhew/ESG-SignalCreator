using System;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// The four fields of a SCPI <c>*IDN?</c> response, parsed and trimmed.
    /// A typical Agilent/Keysight ESG reply is
    /// <c>"Agilent Technologies, E4438C, US44440123, C.05.84"</c>.
    /// </summary>
    public sealed class InstrumentIdentity
    {
        public InstrumentIdentity(string manufacturer, string model, string serial, string firmwareRevision)
        {
            Manufacturer = manufacturer ?? string.Empty;
            Model = model ?? string.Empty;
            Serial = serial ?? string.Empty;
            FirmwareRevision = firmwareRevision ?? string.Empty;
        }

        /// <summary>Field 1 of <c>*IDN?</c> (e.g. "Agilent Technologies").</summary>
        public string Manufacturer { get; }

        /// <summary>Field 2 of <c>*IDN?</c> (e.g. "E4438C").</summary>
        public string Model { get; }

        /// <summary>Field 3 of <c>*IDN?</c> (e.g. "US44440123").</summary>
        public string Serial { get; }

        /// <summary>Field 4 of <c>*IDN?</c> (e.g. "C.05.84").</summary>
        public string FirmwareRevision { get; }

        /// <summary>
        /// Parse a raw <c>*IDN?</c> response by splitting on commas and trimming each field.
        /// Tolerant of missing trailing fields: any field not present becomes an empty string.
        /// </summary>
        public static InstrumentIdentity Parse(string idn)
        {
            if (idn == null) idn = string.Empty;

            string[] parts = idn.Split(',');

            string Field(int index) => index < parts.Length ? parts[index].Trim() : string.Empty;

            return new InstrumentIdentity(Field(0), Field(1), Field(2), Field(3));
        }

        public override string ToString()
        {
            return string.Join(", ", new[] { Manufacturer, Model, Serial, FirmwareRevision });
        }
    }
}
