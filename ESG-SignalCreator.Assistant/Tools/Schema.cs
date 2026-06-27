using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Small JSON-Schema builders for tool input schemas (#80, §4.1). Produces plain
    /// <see cref="JObject"/>s (Newtonsoft-native) emitted into the Messages API <c>tools</c> array.
    /// </summary>
    public static class Schema
    {
        /// <summary>A property spec for <see cref="Object"/>.</summary>
        public sealed class Prop
        {
            public string Name;
            public JObject Def;
            public bool Required;
            public Prop(string name, JObject def, bool required) { Name = name; Def = def; Required = required; }
        }

        public static Prop P(string name, JObject def, bool required = false) => new Prop(name, def, required);

        public static JObject Object(params Prop[] props)
        {
            var properties = new JObject();
            var required = new JArray();
            if (props != null)
            {
                foreach (Prop p in props)
                {
                    properties[p.Name] = p.Def;
                    if (p.Required) required.Add(p.Name);
                }
            }
            var o = new JObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
            if (required.Count > 0) o["required"] = required;
            return o;
        }

        public static JObject Str(string description = null, IEnumerable<string> @enum = null)
        {
            var o = new JObject { ["type"] = "string" };
            if (description != null) o["description"] = description;
            if (@enum != null) { var a = new JArray(); foreach (string e in @enum) a.Add(e); o["enum"] = a; }
            return o;
        }

        public static JObject Number(string description = null) => Typed("number", description);
        public static JObject Integer(string description = null) => Typed("integer", description);
        public static JObject Bool(string description = null) => Typed("boolean", description);

        public static JObject Array(JObject items, string description = null)
        {
            var o = new JObject { ["type"] = "array", ["items"] = items };
            if (description != null) o["description"] = description;
            return o;
        }

        private static JObject Typed(string type, string description)
        {
            var o = new JObject { ["type"] = type };
            if (description != null) o["description"] = description;
            return o;
        }
    }

    /// <summary>
    /// Lightweight validator for tool arguments against a tool's input schema (#80): checks that all
    /// <c>required</c> properties are present and that declared properties have the right JSON type.
    /// Not a full JSON-Schema engine — just enough to reject obviously bad tool calls with a clear
    /// message before execution. Returns null when valid, else a human-readable reason.
    /// </summary>
    public static class SchemaValidator
    {
        public static string Validate(JObject schema, JObject args)
        {
            if (schema == null) return null;
            args = args ?? new JObject();

            var required = schema["required"] as JArray;
            if (required != null)
            {
                foreach (JToken r in required)
                {
                    string name = (string)r;
                    if (args[name] == null || args[name].Type == JTokenType.Null)
                        return "missing required property '" + name + "'";
                }
            }

            var properties = schema["properties"] as JObject;
            if (properties != null)
            {
                foreach (JProperty prop in args.Properties())
                {
                    JToken def = properties[prop.Name];
                    if (def == null) continue; // additional props tolerated by the validator
                    string expected = (string)def["type"];
                    if (expected == null) continue;
                    string typeError = CheckType(prop.Name, expected, prop.Value);
                    if (typeError != null) return typeError;

                    var en = def["enum"] as JArray;
                    if (en != null && prop.Value.Type == JTokenType.String)
                    {
                        bool found = false;
                        foreach (JToken e in en) if ((string)e == (string)prop.Value) { found = true; break; }
                        if (!found) return "property '" + prop.Name + "' must be one of the allowed values";
                    }
                }
            }
            return null;
        }

        private static string CheckType(string name, string expected, JToken value)
        {
            switch (expected)
            {
                case "string": return value.Type == JTokenType.String ? null : Bad(name, "string");
                case "boolean": return value.Type == JTokenType.Boolean ? null : Bad(name, "boolean");
                case "integer": return value.Type == JTokenType.Integer ? null : Bad(name, "integer");
                case "number": return (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) ? null : Bad(name, "number");
                case "array": return value.Type == JTokenType.Array ? null : Bad(name, "array");
                case "object": return value.Type == JTokenType.Object ? null : Bad(name, "object");
                default: return null;
            }
        }

        private static string Bad(string name, string type) => "property '" + name + "' must be a " + type;
    }
}
