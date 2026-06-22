using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SearchLego.DataSeeder.Common
{
    public class Encryption
    {

        /// <summary>
        /// Defines the SaltSize
        /// </summary>
        private const int SaltSize = 16;

        /// <summary>
        /// Defines the ByteSize
        /// </summary>
        private const int ByteSize = 8;

        /// <summary>
        /// Defines the BlockSize
        /// </summary>
        private const int BlockSize = 128;

        /// <summary>
        /// Defines the Iterations
        /// </summary>
        private const int Iterations = 10000;

        /// <summary>
        /// Defines the KeySize
        /// </summary>
        private const int KeySize = 256;

        /// <summary>
        /// The Encrypt
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="pwd">The pwd<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string Encrypt(string text, string pwd)
        {
            byte[] originalBytes = Encoding.UTF8.GetBytes(text);
            byte[] encryptedBytes = null;
            byte[] passwordBytes = Encoding.UTF8.GetBytes(pwd);

            // Hash the password with SHA256
            passwordBytes = System.Security.Cryptography.SHA256.Create().ComputeHash(passwordBytes);

            // Generating salt bytes
            byte[] saltBytes = GetRandomBytes();

            // Appending salt bytes to original bytes
            byte[] bytesToBeEncrypted = new byte[saltBytes.Length + originalBytes.Length];
            for (int i = 0; i < saltBytes.Length; i++)
            {
                bytesToBeEncrypted[i] = saltBytes[i];
            }
            for (int i = 0; i < originalBytes.Length; i++)
            {
                bytesToBeEncrypted[i + saltBytes.Length] = originalBytes[i];
            }

            encryptedBytes = AES_Encrypt(bytesToBeEncrypted, passwordBytes);

            return Convert.ToBase64String(encryptedBytes);
        }

        #region Decrypt
        /*
        /// <summary>
        /// The Decrypt
        /// </summary>
        /// <param name="decryptedText">The decryptedText<see cref="string"/></param>
        /// <param name="pwd">The pwd<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string Decrypt(string decryptedText, string pwd)
        {
            byte[] bytesToBeDecrypted = Convert.FromBase64String(decryptedText);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(pwd);

            // Hash the password with SHA256
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

            byte[] decryptedBytes = AES_Decrypt(bytesToBeDecrypted, passwordBytes);

            // Removing salt bytes, retrieving original bytes
            byte[] originalBytes = new byte[decryptedBytes.Length - SaltSize];
            for (int i = SaltSize; i < decryptedBytes.Length; i++)
            {
                originalBytes[i - SaltSize] = decryptedBytes[i];
            }

            return Encoding.UTF8.GetString(originalBytes);
        }

        /// <summary>
        /// This method will decrypt the target bytes with the password bytes provided
        /// </summary>
        /// <param name="bytesToBeDecrypted"></param>
        /// <param name="passwordBytes"></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private static byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged aes = new RijndaelManaged())
                {
                    aes.KeySize = KeySize;
                    aes.BlockSize = BlockSize;
                    byte[] saltBytes = new byte[SaltSize];
                    byte[] decryptBytes = new byte[(bytesToBeDecrypted.Length - SaltSize)];

                    Array.Copy(bytesToBeDecrypted, saltBytes, SaltSize);
                    Array.Copy(bytesToBeDecrypted, SaltSize, decryptBytes, 0, decryptBytes.Length);

                    using (Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, Iterations))
                    {
                        aes.Key = key.GetBytes(aes.KeySize / ByteSize);
                        aes.IV = key.GetBytes(aes.BlockSize / ByteSize);

                        aes.Mode = CipherMode.CBC;

                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(decryptBytes, 0, decryptBytes.Length);
                            cs.Close();
                        }
                        decryptedBytes = ms.ToArray();
                    }
                }
            }

            return decryptedBytes;
        }
        */
        #endregion
        /// <summary>
        /// This method will encrypt the target bytes with the password bytes provided
        /// </summary>
        /// <param name="bytesToBeEncrypted"></param>
        /// <param name="passwordBytes"></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private static byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        {
            byte[] encryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged aes = new RijndaelManaged())
                {
                    aes.KeySize = KeySize;
                    aes.BlockSize = BlockSize;
                    byte[] saltBytes = GetRandomBytes();
                    using (Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, Iterations))
                    {
                        aes.Key = key.GetBytes(aes.KeySize / ByteSize);
                        aes.IV = key.GetBytes(aes.BlockSize / ByteSize);

                        aes.Mode = CipherMode.CBC;

                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                            cs.Close();
                        }
                        encryptedBytes = Combine(saltBytes, ms.ToArray());
                    }
                }
            }

            return encryptedBytes;
        }

        /// <summary>
        /// Combining multiple bytes
        /// </summary>
        /// <param name="arrays"></param>
        /// <returns></returns>
        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] ret = new byte[arrays.Sum(x => x.Length)];
            int offset = 0;
            foreach (byte[] data in arrays)
            {
                Buffer.BlockCopy(data, 0, ret, offset, data.Length);
                offset += data.Length;
            }
            return ret;
        }

        /// <summary>
        /// The GetRandomBytes
        /// </summary>
        /// <returns>The <see cref="byte[]"/></returns>
        private static byte[] GetRandomBytes()
        {
            byte[] ba = new byte[SaltSize];
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            RandomNumberGenerator.Create().GetBytes(ba);
            return ba;
        }

    }
}
