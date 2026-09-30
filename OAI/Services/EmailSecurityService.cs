using System;
using System.Security.Cryptography;
using System.Text;

namespace OAI.Services
{
    public static class EmailSecurityService
    {
        // Entropy byte array to add extra layer of encryption security
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OAI_DesktopApp_Secure_Entropy_v1");

        public static string ProtectPassword(string plainTextPassword)
        {
            if (string.IsNullOrEmpty(plainTextPassword)) return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainTextPassword);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes, 
                Entropy, 
                DataProtectionScope.CurrentUser
            );

            return Convert.ToBase64String(encryptedBytes);
        }

        public static string UnprotectPassword(string encryptedBase64Password)
        {
            if (string.IsNullOrEmpty(encryptedBase64Password)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64Password);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes, 
                    Entropy, 
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI Decryption failed: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
