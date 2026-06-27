using System;
using System.Collections.Generic;
using EsgSignalCreator.Assistant.Api;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Holds the enabled tools and projects them into the Messages API <c>tools</c> array (#80).
    /// A tool can be disabled (e.g. the gated raw-SCPI passthrough, #88) so it is neither advertised
    /// nor dispatchable until explicitly enabled.
    /// </summary>
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string, IAppTool> _tools = new Dictionary<string, IAppTool>(StringComparer.Ordinal);
        private readonly HashSet<string> _disabled = new HashSet<string>(StringComparer.Ordinal);

        public ToolRegistry Register(IAppTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (string.IsNullOrWhiteSpace(tool.Name)) throw new ArgumentException("Tool name required.");
            _tools[tool.Name] = tool;
            return this;
        }

        public void Register(IEnumerable<IAppTool> tools)
        {
            if (tools == null) return;
            foreach (IAppTool t in tools) Register(t);
        }

        /// <summary>Enable/disable a tool by name (disabled tools are hidden from Claude and rejected).</summary>
        public void SetEnabled(string name, bool enabled)
        {
            if (enabled) _disabled.Remove(name);
            else _disabled.Add(name);
        }

        public bool IsEnabled(string name) => _tools.ContainsKey(name) && !_disabled.Contains(name);

        /// <summary>Look up an enabled tool by name; null if unknown or disabled.</summary>
        public IAppTool ByName(string name)
        {
            if (name == null) return null;
            return _tools.TryGetValue(name, out IAppTool t) && !_disabled.Contains(name) ? t : null;
        }

        public IEnumerable<IAppTool> All()
        {
            foreach (KeyValuePair<string, IAppTool> kv in _tools)
                if (!_disabled.Contains(kv.Key)) yield return kv.Value;
        }

        /// <summary>The enabled tools as Messages API tool definitions.</summary>
        public List<ToolDefinition> ToToolDefinitions()
        {
            var list = new List<ToolDefinition>();
            foreach (IAppTool t in All())
                list.Add(new ToolDefinition { Name = t.Name, Description = t.Description, InputSchema = t.InputSchema });
            return list;
        }
    }
}
