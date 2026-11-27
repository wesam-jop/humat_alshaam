using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Identity
{
    /// <summary>
    /// Guest sign-in. Creates a random local profile id on first run and stores it under
    /// persistentDataPath/identity/guest-slotN.json, so it survives restarts. Works fully offline.
    ///
    /// Several copies of the game on one machine (LAN testing) each lock their own slot file, so they
    /// get different but still persistent guest ids instead of fighting over one.
    /// </summary>
    public sealed class GuestIdentityProvider : IIdentityProvider, IDisposable
    {
        [Serializable]
        sealed class GuestRecord
        {
            public string id;
            public string displayName;
            public string createdUtc;
        }

        readonly string _directory;
        readonly string _forcedProfile;
        FileStream _lock;

        public PlayerIdentity Current { get; private set; }
        public int Slot { get; private set; } = -1;
        public string RecordPath { get; private set; }

        /// <param name="directory">Folder for identity files (persistentDataPath/identity in the game).</param>
        /// <param name="forcedProfile">Optional profile name (-profile argument); skips slot locking.</param>
        public GuestIdentityProvider(string directory, string forcedProfile = null)
        {
            _directory = directory;
            _forcedProfile = string.IsNullOrWhiteSpace(forcedProfile) ? null : Sanitize(forcedProfile);
        }

        public Task<PlayerIdentity> SignInAsync() => Task.FromResult(SignIn());

        public PlayerIdentity SignIn()
        {
            if (Current != null) return Current;
            Directory.CreateDirectory(_directory);

            if (_forcedProfile != null)
            {
                RecordPath = Path.Combine(_directory, "guest-" + _forcedProfile + ".json");
            }
            else
            {
                for (int slot = 0; slot < 16; slot++)
                {
                    try
                    {
                        _lock = new FileStream(Path.Combine(_directory, "guest-slot" + slot + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        Slot = slot;
                        break;
                    }
                    catch (IOException)
                    {
                        // Another running copy owns this slot.
                    }
                }
                if (Slot < 0) Slot = 99;
                RecordPath = Path.Combine(_directory, "guest-slot" + Slot + ".json");
            }

            GuestRecord record = null;
            if (File.Exists(RecordPath))
            {
                try { record = JsonUtility.FromJson<GuestRecord>(File.ReadAllText(RecordPath, Encoding.UTF8)); }
                catch (Exception e) { GameLog.Warn("Guest identity file unreadable, recreating: " + e.Message); }
            }
            if (record == null || string.IsNullOrEmpty(record.id))
            {
                var id = Guid.NewGuid().ToString("N");
                record = new GuestRecord { id = id, displayName = "ضيف-" + id.Substring(0, 4).ToUpperInvariant(), createdUtc = DateTime.UtcNow.ToString("o") };
                File.WriteAllText(RecordPath, JsonUtility.ToJson(record, true), new UTF8Encoding(false));
            }

            Current = new PlayerIdentity
            {
                Kind = IdentityKind.Guest,
                LocalProfileId = record.id,
                DisplayName = record.displayName,
                AccountId = null,
                SessionToken = null,
            };
            return Current;
        }

        public void Dispose()
        {
            _lock?.Dispose();
            _lock = null;
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            return sb.Length == 0 ? "default" : sb.ToString();
        }
    }

    /// <summary>Combines the guest provider with the optional account provider.</summary>
    public sealed class IdentityService : IDisposable
    {
        readonly GuestIdentityProvider _guest;
        public IAccountProvider Accounts { get; }
        public PlayerIdentity Current { get; private set; }

        public IdentityService(GuestIdentityProvider guest, IAccountProvider accounts)
        {
            _guest = guest;
            Accounts = accounts ?? new UnavailableAccountProvider();
        }

        /// <summary>Always yields an identity: account if available later, otherwise guest.</summary>
        public PlayerIdentity SignInGuest()
        {
            Current = _guest.SignIn();
            return Current;
        }

        public string GuestRecordPath => _guest.RecordPath;

        public void Dispose() => _guest.Dispose();
    }
}
