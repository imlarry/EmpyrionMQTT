using System.Numerics;
using System.Text;

namespace EmpyrionMQTT.ShapeBaker;

internal sealed class BakedStamp
{
    public string Name { get; init; } = "";
    // Packed occupancy: bit i where i = x*res*res + y*res + z (LSB-first per byte).
    public byte[] Packed { get; init; } = Array.Empty<byte>();
}

internal sealed class BakeFile
{
    public int Resolution { get; init; }
    public AABB Frame { get; init; }
    public IReadOnlyList<BakedStamp> Stamps { get; init; } = Array.Empty<BakedStamp>();
}

internal static class BakeReader
{
    private const uint ExpectedMagic = 0x53424d45; // little-endian "EMBS"

    public static BakeFile Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        uint magic = br.ReadUInt32();
        if (magic != ExpectedMagic)
            throw new InvalidDataException(
                $"bad magic 0x{magic:X8} (expected EMBS 0x{ExpectedMagic:X8})");

        uint version = br.ReadUInt32();
        if (version != 1)
            throw new InvalidDataException($"unsupported version {version}");

        int resolution = (int)br.ReadUInt32();
        int count = (int)br.ReadUInt32();

        var min = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        var max = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        int totalBits = resolution * resolution * resolution;
        int byteCount = (totalBits + 7) / 8;

        var stamps = new List<BakedStamp>(count);
        for (int i = 0; i < count; i++)
        {
            ushort nameLen = br.ReadUInt16();
            var nameBytes = br.ReadBytes(nameLen);
            var name = Encoding.ASCII.GetString(nameBytes);
            var packed = br.ReadBytes(byteCount);
            stamps.Add(new BakedStamp { Name = name, Packed = packed });
        }

        return new BakeFile
        {
            Resolution = resolution,
            Frame = new AABB(min, max),
            Stamps = stamps,
        };
    }

    public static bool IsSet(BakedStamp stamp, int resolution, int x, int y, int z)
    {
        int i = x * resolution * resolution + y * resolution + z;
        return (stamp.Packed[i >> 3] & (1 << (i & 7))) != 0;
    }

    public static int CountFilled(BakedStamp stamp)
    {
        int n = 0;
        foreach (var b in stamp.Packed) n += System.Numerics.BitOperations.PopCount((uint)b);
        return n;
    }
}
