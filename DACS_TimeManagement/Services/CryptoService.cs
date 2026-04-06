using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace DACS_TimeManagement.Services
{
    public class CryptoService : ICryptoService
    {
        private readonly byte[] _key;

        public CryptoService(IConfiguration configuration)
        {
            var keyString = configuration["EncryptionKey"] ?? "b14ca5898a4e4133bbce2ea2315a1916"; // Mặc định 32 bytes (256 bits) nếu không config
            if (keyString.Length > 32) keyString = keyString.Substring(0, 32);
            else if (keyString.Length < 32) keyString = keyString.PadRight(32, '0');
            
            _key = Encoding.UTF8.GetBytes(keyString);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _key;
                aesAlg.GenerateIV(); // Tạo IV ngẫu nhiên

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                byte[] encryptedBytes;

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encryptedBytes = msEncrypt.ToArray();
                    }
                }

                // Ghép IV và data mã hóa để khi giải mã lấy lại được IV
                var result = new byte[aesAlg.IV.Length + encryptedBytes.Length];
                Buffer.BlockCopy(aesAlg.IV, 0, result, 0, aesAlg.IV.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, aesAlg.IV.Length, encryptedBytes.Length);

                return Convert.ToBase64String(result);
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                var fullCipherContent = Convert.FromBase64String(cipherText);

                using (Aes aesAlg = Aes.Create())
                {
                    // Tách IV ra từ 16 bytes đầu tiên
                    var iv = new byte[16];
                    var cipher = new byte[fullCipherContent.Length - 16];

                    Buffer.BlockCopy(fullCipherContent, 0, iv, 0, iv.Length);
                    Buffer.BlockCopy(fullCipherContent, 16, cipher, 0, cipher.Length);

                    aesAlg.Key = _key;
                    aesAlg.IV = iv;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipher))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Nếu lỗi giải mã (do đổi key hoặc data ko được mã hóa từ trước), trả về nguyên bản
                return cipherText;
            }
        }
    }
}
