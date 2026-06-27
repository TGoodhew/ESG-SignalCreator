using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>
    /// Contract acceptance tests over the full v1 tool set (read + configure + hardware): every tool
    /// must expose an API-valid JSON Schema (object schema with a properties object) and a well-formed
    /// contract (snake_case unique name, useful description, consistent required list), and the registry
    /// must project the whole set faithfully into Messages API tool definitions.
    /// </summary>
    public class AcceptanceSchemaTests
    {
        private static readonly Regex SnakeCase = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>The complete v1 tool surface advertised to Claude.</summary>
        private static IReadOnlyList<IAppTool> FullToolSet()
        {
            return ReadTools.All()
                .Concat(ConfigureTools.All())
                .Concat(HardwareTools.All())
                .ToList();
        }

        [Fact]
        public void All_tool_names_are_snake_case_and_unique()
        {
            IReadOnlyList<IAppTool> tools = FullToolSet();

            foreach (IAppTool tool in tools)
            {
                Assert.False(string.IsNullOrEmpty(tool.Name), "A tool has a null/empty Name.");
                Assert.True(
                    SnakeCase.IsMatch(tool.Name),
                    "Tool name is not snake_case: '" + tool.Name + "'.");
            }

            List<string> names = tools.Select(t => t.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void All_descriptions_are_nonempty_for_claude()
        {
            foreach (IAppTool tool in FullToolSet())
            {
                Assert.False(
                    string.IsNullOrEmpty(tool.Description),
                    "Tool '" + tool.Name + "' has a null/empty Description.");
                Assert.True(
                    tool.Description.Length >= 20,
                    "Tool '" + tool.Name + "' has a too-short Description (length " +
                    tool.Description.Length + ").");
            }
        }

        [Fact]
        public void All_input_schemas_are_object_schemas()
        {
            foreach (IAppTool tool in FullToolSet())
            {
                JObject schema = tool.InputSchema;
                Assert.True(schema != null, "Tool '" + tool.Name + "' has a null InputSchema.");

                Assert.Equal("object", (string)schema["type"]);

                JToken props = schema["properties"];
                Assert.True(props != null, "Tool '" + tool.Name + "' schema has no 'properties'.");
                Assert.Equal(JTokenType.Object, props.Type);
            }
        }

        [Fact]
        public void Required_properties_exist_in_properties()
        {
            foreach (IAppTool tool in FullToolSet())
            {
                JObject schema = tool.InputSchema;
                var properties = (JObject)schema["properties"];

                if (schema["required"] is JArray required)
                {
                    foreach (JToken entry in required)
                    {
                        var key = (string)entry;
                        Assert.True(
                            key != null && properties[key] != null,
                            "Tool '" + tool.Name + "' lists required property '" + key +
                            "' that is not declared in 'properties'.");
                    }
                }
            }
        }

        [Fact]
        public void Registry_definitions_match_the_tools()
        {
            IReadOnlyList<IAppTool> tools = FullToolSet();

            var registry = new ToolRegistry();
            registry.Register(tools);

            List<ToolDefinition> definitions = registry.ToToolDefinitions();

            Assert.Equal(tools.Count, definitions.Count);

            foreach (ToolDefinition def in definitions)
            {
                Assert.False(string.IsNullOrEmpty(def.Name), "A tool definition has a null/empty Name.");
                Assert.True(def.InputSchema != null, "Tool definition '" + def.Name + "' has a null InputSchema.");
            }

            var toolNames = new HashSet<string>(tools.Select(t => t.Name));
            var defNames = new HashSet<string>(definitions.Select(d => d.Name));
            Assert.True(toolNames.SetEquals(defNames), "Definition names do not match tool names.");
        }
    }
}
