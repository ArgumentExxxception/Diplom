namespace Core;

public class SecurityService
{
    public static string GenerateSalt()
    {
        byte[] salt = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        return Convert.ToBase64String(salt);
    }

    public static string HashPassword(string password, string salt)
    {
        using (var hasher = new System.Security.Cryptography.HMACSHA512(Convert.FromBase64String(salt)))
        {
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hashedBytes = hasher.ComputeHash(passwordBytes);
            return Convert.ToBase64String(hashedBytes);
        }
    }

    public static bool VerifyPassword(string password, string salt, string storedHash)
    {
        string computedHash = HashPassword(password, salt);
        return computedHash == storedHash;
    }
}