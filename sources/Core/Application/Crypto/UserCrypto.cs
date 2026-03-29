using System.Security.Cryptography;
using NSec.Cryptography;

namespace Core.Application.Crypto;

public static class UserCrypto
{
    public static (byte[] privateEd25519, byte[] publicEd25519, byte[] privateX25519, byte[] publicX25519) GenerateKeys()
    {
        var edAlg = SignatureAlgorithm.Ed25519;
        using var edKey = Key.Create(edAlg, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var pubEd = edKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var privEd = edKey.Export(KeyBlobFormat.RawPrivateKey);

        var xAlg = KeyAgreementAlgorithm.X25519;
        using var xKey = Key.Create(xAlg, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var pubX = xKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var privX = xKey.Export(KeyBlobFormat.RawPrivateKey);

        return (privEd, pubEd, privX, pubX);
    }

    public static byte[] Sign(byte[] privateKeyEd25519, byte[] data)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        using var key = Key.Import(algorithm, privateKeyEd25519, KeyBlobFormat.RawPrivateKey);
        return algorithm.Sign(key, data);
    }

    public static string ComputeFingerprint(byte[] publicKeyEd25519)
    {
        var hash = SHA256.HashData(publicKeyEd25519);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
