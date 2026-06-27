using System;
using System.IO;
using EsgSignalCreator.Assistant.Secrets;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class SecretsTests
    {
        /// <summary>Reversible non-encrypting protector so the store can be tested without DPAPI/user binding.</summary>
        private sealed class XorProtector : ISecretProtector
        {
            public byte[] Protect(byte[] p) { var c = (byte[])p.Clone(); for (int i = 0; i < c.Length; i++) c[i] ^= 0x5A; return c; }
            public byte[] Unprotect(byte[] c) { var p = (byte[])c.Clone(); for (int i = 0; i < p.Length; i++) p[i] ^= 0x5A; return p; }
        }

        private static string TempFile() =>
            Path.Combine(Path.GetTempPath(), "esg-assistant-test-" + Guid.NewGuid().ToString("N") + ".dat");

        [Fact]
        public void ApiKeyStore_round_trips_through_the_protector()
        {
            string path = TempFile();
            try
            {
                var store = new ApiKeyStore(path, new XorProtector());
                Assert.False(store.Exists);
                store.Save("sk-ant-secret");
                Assert.True(store.Exists);

                Assert.True(store.TryLoad(out string key));
                Assert.Equal("sk-ant-secret", key);

                // On-disk bytes are not the plaintext.
                byte[] onDisk = File.ReadAllBytes(path);
                Assert.DoesNotContain("sk-ant-secret", System.Text.Encoding.UTF8.GetString(onDisk));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ApiKeyStore_clear_removes_the_key()
        {
            string path = TempFile();
            var store = new ApiKeyStore(path, new XorProtector());
            store.Save("k");
            store.Clear();
            Assert.False(store.Exists);
            Assert.False(store.TryLoad(out _));
        }

        [Fact]
        public void ApiKeyStore_missing_file_loads_false()
        {
            var store = new ApiKeyStore(TempFile(), new XorProtector());
            Assert.False(store.TryLoad(out string key));
            Assert.Null(key);
        }

        [Fact]
        public void ApiKeyStore_round_trips_through_real_dpapi()
        {
            string path = TempFile();
            try
            {
                var store = new ApiKeyStore(path); // real DpapiSecretProtector
                store.Save("dpapi-key-123");
                Assert.True(store.TryLoad(out string key));
                Assert.Equal("dpapi-key-123", key);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void PrivacyGuard_collapses_long_arrays()
        {
            var big = new JArray();
            for (int i = 0; i < 5000; i++) big.Add(i);
            var data = new JObject { ["iq"] = big, ["note"] = "ok" };

            JObject compact = PrivacyGuard.Compact(data, maxArrayLength: 64);

            Assert.True((bool)compact["iq"]["_omitted_array"]);
            Assert.Equal(5000, (int)compact["iq"]["length"]);
            Assert.Equal(8, ((JArray)compact["iq"]["sample"]).Count);
            Assert.Equal("ok", (string)compact["note"]);
        }

        [Fact]
        public void PrivacyGuard_truncates_very_long_strings_and_keeps_small_data()
        {
            var data = new JObject { ["blob"] = new string('x', 50000), ["small"] = "fine" };
            JObject compact = PrivacyGuard.Compact(data, maxStringLength: 100);
            Assert.Contains("truncated", (string)compact["blob"]);
            Assert.Equal("fine", (string)compact["small"]);
        }

        [Fact]
        public void AssistantSettings_default_is_disabled_and_round_trips()
        {
            Assert.False(new AssistantSettings().Enabled); // master off by default

            string path = TempFile();
            try
            {
                var store = new AssistantSettingsStore(path);
                store.Save(new AssistantSettings { Enabled = true, AutoApproveHardware = false, Model = "claude-opus-4-8" });
                AssistantSettings loaded = store.Load();
                Assert.True(loaded.Enabled);
                Assert.Equal("claude-opus-4-8", loaded.Model);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
