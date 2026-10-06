namespace Wasla.Identity.Application.Abstractions;

/// <summary>Password hashing abstraction (implementation uses ASP.NET Core Identity hashing).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
