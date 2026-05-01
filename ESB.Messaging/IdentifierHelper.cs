using System.Security.Cryptography;
using System.Text;

namespace ESB.Messaging
{
    public static class IdentifierHelper
    {
        public static string GenerateIdentifier(string source, int width)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? ""));
                return IdEncoder.ToBase36(bytes, width);
            }
        }
    }
}
