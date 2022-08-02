using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Pathfinding_Project
{
    public static class PublicExtensions
    {
        public static string returnHashedValueOfPath2(this List<int> lstPath)
        {
            return ComputeSha256Hash(string.Join(" ", lstPath));
        }

        public static int returnHashedValueOfPath(this List<int> lstPath)
        {
            return string.Join(" ", lstPath).GetHashCode();
        }

        static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
