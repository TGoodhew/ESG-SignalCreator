using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EsgSignalCreator.Project
{
    /// <summary>
    /// Reads and writes <see cref="SsProject"/> artifacts (<c>*.ssproj</c>, UTF-8 JSON) and provides
    /// helpers for round-tripping an opaque personality config object to/from JSON without the Core
    /// library knowing the concrete config type at compile time.
    /// </summary>
    public static class ProjectStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Serializes <paramref name="project"/> to <paramref name="path"/> as UTF-8 JSON.</summary>
        public static void Save(string path, SsProject project)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (project == null) throw new ArgumentNullException(nameof(project));

            var serializer = new DataContractJsonSerializer(typeof(SsProject));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.WriteObject(stream, project);
            }
        }

        /// <summary>Deserializes an <see cref="SsProject"/> from the UTF-8 JSON file at <paramref name="path"/>.</summary>
        public static SsProject Load(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var serializer = new DataContractJsonSerializer(typeof(SsProject));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return (SsProject)serializer.ReadObject(stream);
            }
        }

        /// <summary>Serializes an arbitrary <c>[DataContract]</c> personality config object to JSON.</summary>
        public static string SerializeConfig(object config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var serializer = new DataContractJsonSerializer(config.GetType());
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, config);
                return Utf8NoBom.GetString(stream.ToArray());
            }
        }

        /// <summary>Deserializes a personality config from <paramref name="json"/> into the given <paramref name="type"/>.</summary>
        public static object DeserializeConfig(string json, Type type)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (type == null) throw new ArgumentNullException(nameof(type));

            var serializer = new DataContractJsonSerializer(type);
            using (var stream = new MemoryStream(Utf8NoBom.GetBytes(json)))
            {
                return serializer.ReadObject(stream);
            }
        }

        /// <summary>
        /// Deserializes a personality config from <paramref name="json"/>, resolving the target type
        /// from <paramref name="typeName"/>. Falls back to scanning loaded assemblies when
        /// <see cref="Type.GetType(string)"/> cannot resolve the name directly.
        /// </summary>
        public static object DeserializeConfig(string json, string typeName)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));

            var type = ResolveType(typeName);
            if (type == null)
                throw new TypeLoadException("Could not resolve config type '" + typeName + "'.");

            return DeserializeConfig(json, type);
        }

        /// <summary>
        /// Resolves a type by (assembly-qualified or full) name, searching all assemblies loaded into
        /// the current <see cref="AppDomain"/> when a direct lookup fails. Returns null if not found.
        /// </summary>
        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
