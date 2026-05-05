using System.Security.Cryptography;
using System.Text;

namespace WhatAmIDoing.Services;

/// <summary>
/// Stores a parent / family PIN as a PBKDF2-SHA256 hash in the settings table. The hash
/// format is <c>v1$iterations$saltBase64$hashBase64</c>. The cleartext PIN never touches disk.
/// </summary>
/// <remarks>
/// Future: extend PIN to additional destructive flows (in-app reset, optional uninstall confirm)
/// and pair with parent-verified recovery (e.g. email) — see <c>docs/family-pin-roadmap.md</c>.
/// </remarks>
public static class PinManager
{
    private const string SettingKey = "pin_hash";
    private const int Iterations = 200_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static bool IsSet(Data.AppDatabase db) =>
        !string.IsNullOrEmpty(db.GetSetting(SettingKey));

    public static void Set(Data.AppDatabase db, string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            db.SetSetting(SettingKey, "");
            return;
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Hash(pin, salt, Iterations);
        db.SetSetting(SettingKey, $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public static bool Verify(Data.AppDatabase db, string pin)
    {
        var stored = db.GetSetting(SettingKey);
        if (string.IsNullOrEmpty(stored))
            return true; // no PIN set => always allowed

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "v1")
            return false;
        if (!int.TryParse(parts[1], out var iters))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Hash(pin, salt, iters);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    public static void Clear(Data.AppDatabase db) => db.SetSetting(SettingKey, "");

    private static byte[] Hash(string pin, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(HashBytes);
    }
}
