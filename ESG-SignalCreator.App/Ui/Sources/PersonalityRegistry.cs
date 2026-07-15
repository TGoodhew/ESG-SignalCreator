using System;
using System.Collections.Generic;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.Bluetooth;
using EsgSignalCreator.Personalities.CustomIq;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.GsmEdge;
using EsgSignalCreator.Personalities.Jitter;
using EsgSignalCreator.Personalities.MultiCarrier;
using EsgSignalCreator.Personalities.Multitone;
using EsgSignalCreator.Personalities.MultitoneDistortion;
using EsgSignalCreator.Personalities.Pulse;

namespace EsgSignalCreator.Ui.Sources
{
    /// <summary>Describes a selectable signal personality for the source picker.</summary>
    public sealed class PersonalityDescriptor
    {
        public PersonalityDescriptor(string id, string displayName, Func<IWaveformPersonality> create)
        {
            Id = id;
            DisplayName = displayName;
            Create = create;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public Func<IWaveformPersonality> Create { get; }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// The catalogue of signal personalities the UI offers. Each entry knows how to construct its
    /// Core personality; <see cref="CreatePanel"/> wraps one in an editable source panel.
    /// </summary>
    public static class PersonalityRegistry
    {
        private static readonly List<PersonalityDescriptor> Items = new List<PersonalityDescriptor>
        {
            new PersonalityDescriptor("cw", "CW / Single tone", () => new CwPersonality()),
            new PersonalityDescriptor("multitone", "Multitone", () => new MultitonePersonality()),
            new PersonalityDescriptor("multitone-distortion", "Multitone Distortion", () => new MultitoneDistortionPersonality()),
            new PersonalityDescriptor("multi-carrier", "Multi-Carrier", () => new MultiCarrierPersonality()),
            new PersonalityDescriptor("custom-mod", "Custom Digital Modulation", () => new CustomModPersonality()),
            new PersonalityDescriptor("pulse", "Pulse Building", () => new PulsePersonality()),
            new PersonalityDescriptor("jitter", "Jitter Injection", () => new JitterPersonality()),
            new PersonalityDescriptor("gsm-edge", "GSM/EDGE (GMSK)", () => new GsmEdgePersonality()),
            new PersonalityDescriptor("bluetooth", "Bluetooth (GFSK)", () => new BluetoothPersonality()),
            new PersonalityDescriptor("awgn", "AWGN", () => new AwgnPersonality()),
            new PersonalityDescriptor("import-iq", "Import I/Q", () => new ImportIqPersonality()),
        };

        public static IReadOnlyList<PersonalityDescriptor> All => Items;

        public static PersonalityDescriptor Find(string id) => Items.Find(x => x.Id == id);

        /// <summary>Create an editable source panel for the given personality id.</summary>
        public static ISignalSourcePanel CreatePanel(string id)
        {
            PersonalityDescriptor d = Find(id);
            if (d == null) throw new ArgumentException("Unknown personality: " + id, nameof(id));
            return new GenericSourcePanel(d.Create());
        }
    }
}
