using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EDNAClient.Core.ShapeBake
{
    // Reader for the shapes.bake binary format produced by Tools/ShapeBaker.
    //
    // Header (40 bytes):
    //   byte[4] magic     = "EMBS"
    //   uint32  version   = 1
    //   uint32  resolution
    //   uint32  stampCount
    //   float3  frameMin
    //   float3  frameMax
    //
    // Per stamp:
    //   uint16 nameLength
    //   byte[] nameBytes  (ASCII, length = nameLength)
    //   byte[] occupancy  (length = ceil(resolution^3 / 8))
    //
    // Bit layout: linear index i = x*res*res + y*res + z. Bit i lives in
    // byte (i/8) at position (i%8), LSB-first within each byte.
    public sealed class BakeFile
    {
        public int Resolution { get; }
        public float FrameMinX { get; }
        public float FrameMinY { get; }
        public float FrameMinZ { get; }
        public float FrameMaxX { get; }
        public float FrameMaxY { get; }
        public float FrameMaxZ { get; }
        public IReadOnlyList<BakedStamp> Stamps { get; }

        public BakeFile(int resolution,
            float fxMin, float fyMin, float fzMin,
            float fxMax, float fyMax, float fzMax,
            IReadOnlyList<BakedStamp> stamps)
        {
            Resolution = resolution;
            FrameMinX = fxMin; FrameMinY = fyMin; FrameMinZ = fzMin;
            FrameMaxX = fxMax; FrameMaxY = fyMax; FrameMaxZ = fzMax;
            Stamps = stamps;
        }
    }

    public static class BakeReader
    {
        // little-endian "EMBS"
        private const uint ExpectedMagic = 0x53424d45;

        public static BakeFile Read(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false))
            {
                uint magic = br.ReadUInt32();
                if (magic != ExpectedMagic)
                    throw new InvalidDataException(
                        $"bad magic 0x{magic:X8} (expected EMBS 0x{ExpectedMagic:X8})");

                uint version = br.ReadUInt32();
                if (version != 1)
                    throw new InvalidDataException($"unsupported version {version}");

                int resolution = (int)br.ReadUInt32();
                int count = (int)br.ReadUInt32();

                float minX = br.ReadSingle(), minY = br.ReadSingle(), minZ = br.ReadSingle();
                float maxX = br.ReadSingle(), maxY = br.ReadSingle(), maxZ = br.ReadSingle();

                int totalBits = resolution * resolution * resolution;
                int byteCount = (totalBits + 7) / 8;

                var stamps = new List<BakedStamp>(count);
                for (int i = 0; i < count; i++)
                {
                    ushort nameLen = br.ReadUInt16();
                    var nameBytes = br.ReadBytes(nameLen);
                    var name = Encoding.ASCII.GetString(nameBytes);
                    var packed = br.ReadBytes(byteCount);

                    var occ = new bool[totalBits];
                    int filled = 0;
                    for (int bit = 0; bit < totalBits; bit++)
                    {
                        if ((packed[bit >> 3] & (1 << (bit & 7))) != 0)
                        {
                            occ[bit] = true;
                            filled++;
                        }
                    }

                    stamps.Add(new BakedStamp(name, resolution, occ, filled));
                }

                return new BakeFile(resolution, minX, minY, minZ, maxX, maxY, maxZ, stamps);
            }
        }
    }
}
