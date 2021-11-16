using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;


namespace KTern_On_Premise_Connector
{
    public static class KTernCipher
    {

        private static String KTERN_CIPHER_TEXT = "HgQ3nTs6XfowcwwDVTWIeT5bn8MvoahLcZMTwjsTgpc=";

        public  static String Encrypt(String TextToEncrypt)
        {
            byte[] clearBytes = Encoding.UTF8.GetBytes(TextToEncrypt);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(KTERN_CIPHER_TEXT, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }, 1000);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                encryptor.Mode = CipherMode.CBC;
                encryptor.Padding = PaddingMode.Zeros;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    TextToEncrypt = Convert.ToBase64String(ms.ToArray());
                }
            }
            return TextToEncrypt;
        }

    }
}
