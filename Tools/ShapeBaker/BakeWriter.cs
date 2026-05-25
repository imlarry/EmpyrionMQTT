using System.Text;

namespace EmpyrionMQTT.ShapeBaker;

// Binary stamp file format:
//
//   Header (40 bytes):
//     byte[4] magic     = "EMBS"
//     uint32  version   = 1
//     uint32  resolution
//     uint32  stampCount
//     float3  frameMin            // mesh-space min of reference AABB
//     float3  frameMax            // mesh-space max of reference AABB
//
//   Per stamp:
//     uint16  nameLength
//     byte[]  nameBytes  (ASCII, length = nameLength)
//     byte[]  occupancy  (length = ceil(resolution^3 / 8))
//
//   Occupancy bit layout: linear index i = x*res*res + y*res + z. Bit i
//   lives in byte (i/8), at position (i%8), LSB-first within each byte.
internal static class BakeWriter
{
    private const uint Version = 1;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("EMBS");

    public static void Write(
        string outputPath,
        int resolution,
        AABB frame,
        IReadOnlyList<(string Name, bool[,,] Stamp)> stamps)
    {
        using var fs = File.Create(outputPath);
        using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);

        bw.Write(Magic);
        bw.Write(Version);
        bw.Write((uint)resolution);
        bw.Write((uint)stamps.Count);

        bw.Write(frame.Min.X); bw.Write(frame.Min.Y); bw.Write(frame.Min.Z);
        bw.Write(frame.Max.X); bw.Write(frame.Max.Y); bw.Write(frame.Max.Z);

        int totalBits = resolution * resolution * resolution;
        int byteCount = (totalBits + 7) / 8;
        var packed = new byte[byteCount];

        foreach (var (name, stamp) in stamps)
        {
            var nameBytes = Encoding.ASCII.GetBytes(name);
            if (nameBytes.Length > ushort.MaxValue)
                throw new InvalidOperationException($"shape name too long: {name}");

            bw.Write((ushort)nameBytes.Length);
            bw.Write(nameBytes);

            Array.Clear(packed, 0, packed.Length);
            int i = 0;
            for (int x = 0; x < resolution; x++)
            for (int y = 0; y < resolution; y++)
            for (int z = 0; z < resolution; z++, i++)
            {
                if (stamp[x, y, z]) packed[i >> 3] |= (byte)(1 << (i & 7));
            }
            bw.Write(packed);
        }
    }
}
