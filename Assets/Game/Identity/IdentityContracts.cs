using System.Threading.Tasks;

namespace Adventure3D.Identity
{
    public enum IdentityKind { Guest, Account }

    /// <summary>
    /// Who the local player is. Three different identifiers exist and must not be mixed:
    ///  - LocalProfileId: persistent id of the local profile on this device (guest), survives restarts.
    ///  - AccountId: id issued by a future account service; null for guests.
    ///  - The network player id (ushort) is assigned by the server per connection and lives only in the
    ///    session (see GameSession.LocalPlayerId); it is never stored here.
    /// </summary>
    public sealed class PlayerIdentity
    {
        public IdentityKind Kind;
        public string LocalProfileId;
        public string DisplayName;
        public string AccountId;
        /// <summary>Opaque token a future account provider issues; sent in the session handshake.</summary>
        public string SessionToken;

        public bool IsGuest => Kind == IdentityKind.Guest;
    }

    /// <summary>Signs the player in locally. Must always succeed offline.</summary>
    public interface IIdentityProvider
    {
        PlayerIdentity Current { get; }
        Task<PlayerIdentity> SignInAsync();
    }

    public enum AuthError
    {
        None,
        NotConfigured,
        NetworkUnavailable,
        InvalidCredentials,
        AccountExists,
        SessionExpired,
        Unknown,
    }

    public sealed class AuthResult
    {
        public bool Success;
        public AuthError Error;
        public string AccountId;
        public string DisplayName;
        public string SessionToken;

        public static AuthResult Fail(AuthError e) => new AuthResult { Success = false, Error = e };
    }

    public struct AccountCredentials
    {
        public string Login;
        public string Secret;
    }

    /// <summary>
    /// Future account service integration point (create account, sign in, sign out, restore session).
    /// Only <see cref="UnavailableAccountProvider"/> exists in this phase: nothing pretends to
    /// authenticate. Offline play never depends on this provider.
    /// </summary>
    public interface IAccountProvider
    {
        bool IsAvailable { get; }
        Task<AuthResult> CreateAccountAsync(AccountCredentials credentials, string displayName);
        Task<AuthResult> SignInAsync(AccountCredentials credentials);
        Task SignOutAsync();
        /// <summary>Restores a previous session (stored refresh token) without user input.</summary>
        Task<AuthResult> TryRestoreSessionAsync();
    }

    public sealed class UnavailableAccountProvider : IAccountProvider
    {
        public bool IsAvailable => false;
        public Task<AuthResult> CreateAccountAsync(AccountCredentials c, string n) => Task.FromResult(AuthResult.Fail(AuthError.NotConfigured));
        public Task<AuthResult> SignInAsync(AccountCredentials c) => Task.FromResult(AuthResult.Fail(AuthError.NotConfigured));
        public Task SignOutAsync() => Task.CompletedTask;
        public Task<AuthResult> TryRestoreSessionAsync() => Task.FromResult(AuthResult.Fail(AuthError.NotConfigured));
    }
}
