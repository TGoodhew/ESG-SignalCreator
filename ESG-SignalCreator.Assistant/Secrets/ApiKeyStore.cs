using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EsgSignalCreator.Assistant.Secrets
{
    /// <summary>Encrypts/decrypts secret bytes. Default is Windows DPAPI (per-user).</summary>
    public interface ISecretProtector
    {
        byte[] Protect(byte[] plaintext);
        byte[] Unprotect(byte[] ciphertext);
    }

    /// <summary>Windows DPAPI protector scoped to the current user (#85, §8).</summary>
    public sealed class DpapiSecretProtector : ISecretProtector
    {
        // App-specific optional entropy; not a secret, just domain separation.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ESG-SignalCreator.Assistant.v1");

        public byte[] Protect(byte[] plaintext) =>
            ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

        public byte[] Unprotect(byte[] ciphertext) =>
            ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Stores the Anthropic API key encrypted at rest (#85). Uses Windows DPAPI by default so the key
    /// is bound to the current Windows user and never stored in plaintext — and never written to project
    /// files, logs, or transcripts. The storage path and protector are injectable for testing.
    /// </summary>
    public sealed class ApiKeyStore
    {
        private readonly string _path;
        private readonly ISecretProtector _protector;

        public ApiKeyStore(string filePath = null, ISecretProtector protector = null)
        {
            _path = filePath ?? DefaultPath();
            _protector = protector ?? new DpapiSecretProtector();
        }

        public static string DefaultPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ESG-SignalCreator");
            return Path.Combine(dir, "apikey.dat");
        }

        public bool Exists => File.Exists(_path);

        public void Save(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentException("API key required.", nameof(apiKey));
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            byte[] cipher = _protector.Protect(Encoding.UTF8.GetBytes(apiKey));
            File.WriteAllBytes(_path, cipher);
        }

        public bool TryLoad(out string apiKey)
        {
            apiKey = null;
            if (!File.Exists(_path)) return false;
            try
            {
                byte[] plain = _protector.Unprotect(File.ReadAllBytes(_path));
                apiKey = Encoding.UTF8.GetString(plain);
                return apiKey.Length > 0;
            }
            catch
            {
                return false; // corrupt / wrong user — treat as absent
            }
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
