using Microsoft.AspNetCore.Identity;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Security;

/// <summary>ASP.NET Core Identity password hashing (PBKDF2-SHA256 with per-password salt).</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(user: null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
