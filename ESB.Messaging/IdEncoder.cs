using System;

namespace ESB.Messaging
{
    // Encodes a byte array as a base-36 (0-9, a-z) string, zero-padded to a fixed width.
    // Output is suitable for use as an identifier; encoding is one-way (not reversible by design).
    public static class IdEncoder
    {
        private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const int Base = 36;

        // ToBase36 encodes bytes as a base-36 string of exactly `width` characters.
        public static string ToBase36(byte[] bytes, int width)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (width <= 0) throw new ArgumentOutOfRangeException("width");

            // Work on a mutable copy treated as a big-endian unsigned integer.
            byte[] digits = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, digits, 0, bytes.Length);

            char[] result = new char[width];
            for (int i = width - 1; i >= 0; i--)
            {
                // Divide the big-endian byte array by 36 in place; capture remainder.
                int rem = 0;
                for (int j = 0; j < digits.Length; j++)
                {
                    int cur = rem * 256 + digits[j];
                    digits[j] = (byte)(cur / Base);
                    rem = cur % Base;
                }
                result[i] = Alphabet[rem];
            }
            return new string(result);
        }
    }
}
