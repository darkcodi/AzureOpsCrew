using System.Security.Cryptography;
using Murmur;

namespace AzureOpsCrew.Domain.Utils;

public static class HashUtils
{
    public static Guid HashStringToGuid(string str)
    {
        if (string.IsNullOrEmpty(str)) return Guid.Empty;
        using HashAlgorithm murmur128 = MurmurHash.Create128(managed: true);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(str);
        byte[] hash = murmur128.ComputeHash(data);
        return new Guid(hash);
    }

    public static int HashStringToInt(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        using HashAlgorithm murmur32 = MurmurHash.Create32(managed: true);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(str);
        byte[] hash = murmur32.ComputeHash(data);
        return BitConverter.ToInt32(hash, 0);
    }
}
