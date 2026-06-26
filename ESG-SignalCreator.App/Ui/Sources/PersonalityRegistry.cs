using System;
using System.Collections.Generic;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.CustomIq;
using EsgSignalCreator.Personalities.Multitone;

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
