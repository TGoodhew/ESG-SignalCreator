using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Secrets
{
    /// <summary>
    /// Privacy minimization for outbound data (#85, §8): only compact, structured tool results are sent
    /// to the API — never raw I/Q arrays or huge blobs. <see cref="Compact"/> collapses over-long arrays
    /// into a length + small sample and truncates very long strings, so a tool that accidentally returns
    /// bulk sample data can't leak it into the conversation.
    /// </summary>
    public static class PrivacyGuard
    {
        public const int DefaultMaxArrayLength = 64;
        public const int DefaultMaxStringLength = 20000;

        /// <summary>Return a privacy-minimized copy of <paramref name="data"/> (original untouched).</summary>
        public static JObject Compact(JObject data, int maxArrayLength = DefaultMaxArrayLength, int maxStringLength = DefaultMaxStringLength)
        {
            if (data == null) return null;
            return (JObject)Walk(data.DeepClone(), maxArrayLength, maxStringLength);
        }

        private static JToken Walk(JToken token, int maxArray, int maxString)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    foreach (JProperty p in System.Linq.Enumerable.ToList(obj.Properties()))
                        p.Value = Walk(p.Value, maxArray, maxString);
                    return obj;

                case JTokenType.Array:
                    var arr = (JArray)token;
                    if (arr.Count > maxArray)
                    {
                        var sample = new JArray();
                        for (int i = 0; i < 8 && i < arr.Count; i++) sample.Add(Walk(arr[i], maxArray, maxString));
                        return new JObject
                        {
                            ["_omitted_array"] = true,
                            ["length"] = arr.Count,
                            ["sample"] = sample
                        };
                    }
                    for (int i = 0; i < arr.Count; i++) arr[i] = Walk(arr[i], maxArray, maxString);
                    return arr;

                case JTokenType.String:
                    string s = (string)token;
                    if (s != null && s.Length > maxString)
                        return s.Substring(0, maxString) + "…[" + (s.Length - maxString) + " chars truncated]";
                    return token;

                default:
                    return token;
            }
        }
    }
}
