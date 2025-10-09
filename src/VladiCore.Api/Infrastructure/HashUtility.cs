using System.Security.Cryptography;
using System.Text;

namespace VladiCore.Api.Infrastructure
{
    public static class HashUtility
    {
        public static string Compute(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                var hash = md5.ComputeHash(bytes);
                return System.BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
