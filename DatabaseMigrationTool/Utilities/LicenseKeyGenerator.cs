using System;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseMigrationTool.Utilities;

/// <summary>
/// DEVELOPMENT UTILITY ONLY
/// Use this class to generate encrypted license key parts
/// DELETE THIS FILE BEFORE RELEASING TO PRODUCTION
/// </summary>
public static class LicenseKeyGenerator
{
    /// <summary>
    /// Call this method once to generate your encrypted license parts
    /// Copy the output and update App.xaml.cs GetEncryptedPart1/2/3 methods
    /// </summary>
    public static void GenerateLicenseKeyParts(string syncfusionLicenseKey)
    {
        Console.WriteLine("=== License Key Encryption Tool ===");
        Console.WriteLine();
        
        try
        {
            // Step 1: Obfuscate the license key
            var obfuscated = ObfuscateString(syncfusionLicenseKey);
            Console.WriteLine($"Step 1 - Obfuscated: {obfuscated}");
            Console.WriteLine();
            
            // Step 2: Encrypt with DPAPI
            var encrypted = EncryptWithDPAPI(obfuscated);
            Console.WriteLine($"Step 2 - Encrypted (Full): {encrypted}");
            Console.WriteLine();
            
            // Step 3: Split into three parts
            var length = encrypted.Length;
            var part1 = encrypted.Substring(0, length / 3);
            var part2 = encrypted.Substring(length / 3, length / 3);
            var part3 = encrypted.Substring((length / 3) * 2);
            
            Console.WriteLine("=== COPY THESE VALUES TO App.xaml.cs ===");
            Console.WriteLine();
            Console.WriteLine($"GetEncryptedPart1() return: \"{part1}\"");
            Console.WriteLine();
            Console.WriteLine($"GetEncryptedPart2() return: \"{part2}\"");
            Console.WriteLine();
            Console.WriteLine($"GetEncryptedPart3() return: \"{part3}\"");
            Console.WriteLine();
            Console.WriteLine("==========================================");
            Console.WriteLine();
            
            // Verify decryption works
            var combined = part1 + part2 + part3;
            var decrypted = DecryptWithDPAPI(combined);
            var deobfuscated = DeobfuscateString(decrypted);
            
            if (deobfuscated == syncfusionLicenseKey)
            {
                Console.WriteLine("✓ Verification successful! The license key can be decrypted correctly.");
            }
            else
            {
                Console.WriteLine("✗ Verification failed! Something went wrong.");
                Console.WriteLine($"Original: {syncfusionLicenseKey}");
                Console.WriteLine($"Result:   {deobfuscated}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static string EncryptWithDPAPI(string plainText)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            GetEntropy(),
            DataProtectionScope.CurrentUser
        );
        return Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptWithDPAPI(string encryptedData)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
        byte[] decryptedBytes = ProtectedData.Unprotect(
            encryptedBytes,
            GetEntropy(),
            DataProtectionScope.CurrentUser
        );
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private static byte[] GetEntropy()
    {
        var entropy = new List<byte>();
        entropy.AddRange(Encoding.UTF8.GetBytes("DbMigTool"));
        
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
        entropy.AddRange(Encoding.UTF8.GetBytes(assemblyName.Name ?? ""));
        entropy.AddRange(Encoding.UTF8.GetBytes(assemblyName.Version?.ToString() ?? "1.0.4"));
        
        return entropy.ToArray();
    }

    private static string ObfuscateString(string input)
    {
        var key = new byte[] { 0x42, 0x79, 0x65 };
        var bytes = Encoding.UTF8.GetBytes(input);
        
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= key[i % key.Length];
        }
        
        return Encoding.UTF8.GetString(bytes);
    }

    private static string DeobfuscateString(string input)
    {
        var key = new byte[] { 0x42, 0x79, 0x65 };
        var bytes = Encoding.UTF8.GetBytes(input);
        
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= key[i % key.Length];
        }
        
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Example usage - call this from your Main method or a test
    /// </summary>
    public static void Example()
    {
        // Your actual Syncfusion license key
        var licenseKey = "Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH5cdnZUQ2heVEBzXERWYEg=";
        
        GenerateLicenseKeyParts(licenseKey);
    }
}
