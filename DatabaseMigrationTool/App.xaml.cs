using Newtonsoft.Json;
using Syncfusion.Licensing;
using System.Configuration;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace DatabaseMigrationTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        try
        {
           // GenerateEncryptedLicenseParts();
            var licenseKey = GetLicenseKey();
            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"License initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Gets the license key using multiple obfuscation layers
    /// </summary>
    private string GetLicenseKey()
    {
        // Multi-layer obfuscation approach
        // Layer 1: Split encrypted data across multiple parts
        var part1 = GetEncryptedPart1();
        var part2 = GetEncryptedPart2();
        var part3 = GetEncryptedPart3();

        // Layer 2: Combine and decrypt using DPAPI
        var combined = part1 + part2 + part3;
        var decrypted = DecryptWithDPAPI(combined);

        // Layer 3: Deobfuscate the result
        return DeobfuscateString(decrypted);
    }

    /// <summary>
    /// First part of encrypted license (split to make harder to find)
    /// </summary>
    private string GetEncryptedPart1()
    {
        return "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAwiGE0PdxmEiLtWI7PVKdUQAAAAACAAAAAAAQZgAAAAEAACAAAABWxtmkEUZpCHq9ZOPDMzhkc0rb9HKT5Fh8RwIfGIkg7gAAAAAOgAAAAA";
    }

    /// <summary>
    /// Second part of encrypted license
    /// </summary>
    private string GetEncryptedPart2()
    {
        return "IAACAAAAAkD6X0ckkDOlqHH2c1FDLezLOIGakybECEFLBotjM/F2AAAADxatQxF+oFLmwJ+B2quDW4qN/BprHf0GQ61EDm5J554rWpLVUzA4GV1tRyM7EIOZd7ynagqFJKSZIm6IK7";
    }

    /// <summary>
    /// Third part of encrypted license
    /// </summary>
    private string GetEncryptedPart3()
    {
        return "Gk6grNmA32hl50c8ts3SoKhLVCXDEu8Si5JUU6qZL0TEIEhAAAAAMxkIoph3FzEwYayQ2aE3o02GlVW0PrDLfdVSSo6+I86stgCBBNCzwP6b0yd1AL/S0xsy/EHcvrDU1K7L6EW5cQ==";
    }

    /// <summary>
    /// Decrypts data using DPAPI (Windows Data Protection API)
    /// This makes the encryption machine-specific
    /// </summary>
    private string DecryptWithDPAPI(string encryptedData)
    {
        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
            byte[] decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                GetEntropy(), // Additional entropy for extra security
                DataProtectionScope.CurrentUser // Machine + User specific
            );
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            // Fallback: Try with LocalMachine scope
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    GetEntropy(),
                    DataProtectionScope.LocalMachine
                );
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt license key. This application may only work on the authorized machine.", ex);
            }
        }
    }

    /// <summary>
    /// Encrypts data using DPAPI - USE THIS TO GENERATE YOUR ENCRYPTED PARTS
    /// Run this method once on your development machine to get the encrypted values
    /// </summary>
    private string EncryptWithDPAPI(string plainText)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            GetEntropy(),
            DataProtectionScope.CurrentUser // Use CurrentUser for development machine
        );
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Additional entropy (salt) for DPAPI encryption
    /// This adds an extra layer of security
    /// </summary>
    private byte[] GetEntropy()
    {
        // Use a combination of machine-specific and application-specific data
        var entropy = new List<byte>();

        // Part 1: Application-specific constant (obfuscated)
        entropy.AddRange(Encoding.UTF8.GetBytes("DbMigTool"));

        // Part 2: Derived from assembly name (harder to replicate)
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
        entropy.AddRange(Encoding.UTF8.GetBytes(assemblyName.Name ?? ""));

        // Part 3: Version-specific
        entropy.AddRange(Encoding.UTF8.GetBytes(assemblyName.Version?.ToString() ?? "2.0"));

        return entropy.ToArray();
    }

    /// <summary>
    /// Simple obfuscation to add another layer
    /// </summary>
    private string DeobfuscateString(string input)
    {
        // Simple XOR obfuscation with a pattern
        var key = new byte[] { 0x42, 0x79, 0x65 }; // "Bye" in ASCII
        var bytes = Encoding.UTF8.GetBytes(input);

        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= key[i % key.Length];
        }

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Helper method to obfuscate strings - use during development
    /// </summary>
    private string ObfuscateString(string input)
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
    /// UTILITY METHOD: Use this to generate your encrypted license parts
    /// Run this once on your development machine, then copy the output
    /// and update GetEncryptedPart1/2/3 methods
    /// </summary>
    private void GenerateEncryptedLicenseParts()
    {
        // Your actual Syncfusion license key
        var actualLicenseKey = "Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH5cdnZUQ2heVEBzXERWYEg=";

        // Step 1: Obfuscate
        var obfuscated = ObfuscateString(actualLicenseKey);

        // Step 2: Encrypt with DPAPI
        var encrypted = EncryptWithDPAPI(obfuscated);

        // Step 3: Split into parts (makes it harder to find in decompiled code)
        var length = encrypted.Length;
        var part1 = encrypted.Substring(0, length / 3);
        var part2 = encrypted.Substring(length / 3, length / 3);
        var part3 = encrypted.Substring((length / 3) * 2);

        // Output these values - you'll need to manually update the GetEncryptedPartX methods
        //Console.WriteLine($"Part 1: {part1}");
        //Console.WriteLine($"Part 2: {part2}");
        //Console.WriteLine($"Part 3: {part3}");
        //Console.WriteLine($"Full encrypted: {encrypted}");
        var obj = new
        {
            name = part1,
            age = part2,
            extra = part3,
            full = encrypted
        };
        File.WriteAllText("EncryptedLicenseParts.json", JsonConvert.SerializeObject(obj));
    }
}