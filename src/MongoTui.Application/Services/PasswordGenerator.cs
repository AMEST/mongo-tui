using System.Security.Cryptography;

namespace MongoTui.Application.Services;

/// <summary>Generates cryptographically secure passwords for MongoDB users.</summary>
public sealed class PasswordGenerator
{
    private const int DefaultLength = 24;
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}:,.?";

    private readonly string _alphabet = Lower + Upper + Digits + Symbols;

    public string Generate()
    {
        return Generate(DefaultLength);
    }

    public string Generate(int length)
    {
        if (length < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be at least 4.");
        }

        var result = new char[length];
        using var rng = RandomNumberGenerator.Create();

        result[0] = GetRandomChar(Lower, rng);
        result[1] = GetRandomChar(Upper, rng);
        result[2] = GetRandomChar(Digits, rng);
        result[3] = GetRandomChar(Symbols, rng);

        for (var i = 4; i < length; i++)
        {
            result[i] = GetRandomChar(_alphabet, rng);
        }

        Shuffle(result, rng);
        return new string(result);
    }

    private static char GetRandomChar(string source, RandomNumberGenerator rng)
    {
        var data = new byte[4];
        rng.GetBytes(data);
        var value = BitConverter.ToUInt32(data, 0);
        return source[(int)(value % (uint)source.Length)];
    }

    private static void Shuffle(char[] array, RandomNumberGenerator rng)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var data = new byte[4];
            rng.GetBytes(data);
            var j = (int)(BitConverter.ToUInt32(data, 0) % (uint)(i + 1));
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
