using System.Security.Cryptography;

namespace Basera.Shared.Domain;

/// <summary>
/// Version 7 GUIDs (RFC 9562): 48 bits of Unix time in milliseconds, then random bits. New ids sort
/// after older ones, which keeps PostgreSQL's b-tree primary key indexes compact. .NET 9 adds
/// <c>Guid.CreateVersion7()</c> with the same layout; switch to it when we move to .NET 9.
/// </summary>
public static class GuidV7
{
    public static Guid New() => New(DateTimeOffset.UtcNow);

    public static Guid New(DateTimeOffset timestamp)
    {
        var milliseconds = timestamp.ToUnixTimeMilliseconds();
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < 6; i++)
        {
            bytes[i] = (byte)(milliseconds >> (8 * (5 - i)));
        }

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant 10xx

        // Big-endian, so the string form (and PostgreSQL's uuid ordering) starts with the timestamp.
        return new Guid(bytes, bigEndian: true);
    }
}
