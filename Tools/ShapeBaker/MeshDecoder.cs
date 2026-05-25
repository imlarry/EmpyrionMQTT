using System.Numerics;
using AssetsTools.NET;

namespace EmpyrionMQTT.ShapeBaker;

internal sealed class DecodedMesh
{
    public string Name { get; init; } = "";        // shape key, e.g. "Cube"
    public string OwnerName { get; init; } = "";   // sub-object GameObject m_Name (empty after merge)
    public Vector3[] Positions { get; init; } = Array.Empty<Vector3>();
    public int[] Triangles { get; init; } = Array.Empty<int>();
}

// Decodes a Unity Mesh asset (post-5.6 packed VertexData layout) into a
// position array and a flat triangle index list. Empyrion shape meshes are
// simple (single stream, float3 position at channel 0), so the decoder only
// supports that subset and skips anything else.
//
// Coordinate convention: positions are preserved as authored in Unity --
// left-handed, Y-up. Downstream voxelization treats this as the canonical
// pose; per-block Rotation from a structure scan is applied at render time
// against this same reference frame.
internal static class MeshDecoder
{
    public static DecodedMesh? Decode(RawMesh raw)
    {
        var field = raw.Field;

        var vertexData = field["m_VertexData"];
        int vertexCount = (int)vertexData["m_VertexCount"].AsUInt;
        if (vertexCount <= 0) return null;

        var dataBytes = TryReadByteArray(vertexData, "m_DataSize");
        if (dataBytes == null || dataBytes.Length == 0) return null;

        var channelsArray = vertexData["m_Channels"]["Array"];
        int channelCount = channelsArray.Children.Count;
        if (channelCount == 0) return null;

        var channels = new ChannelInfo[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            var c = channelsArray[i];
            channels[i] = new ChannelInfo(
                Stream:    c["stream"].AsByte,
                Offset:    c["offset"].AsByte,
                Format:    c["format"].AsByte,
                Dimension: c["dimension"].AsByte);
        }

        // Position lives at the semantic slot index 0. Some meshes may have
        // dimension 0 for unused channels; require Float3 at slot 0.
        var posCh = channels[0];
        if (posCh.Dimension < 3 || posCh.Format != 0) return null;

        // Stream layout: each stream is a contiguous (stride * vertexCount)
        // block in m_DataSize. Unity aligns each subsequent stream start to
        // 16 bytes.
        Span<int> streamStride = stackalloc int[8];
        foreach (var c in channels)
        {
            if (c.Dimension == 0 || c.Stream >= streamStride.Length) continue;
            streamStride[c.Stream] += c.Dimension * FormatSize(c.Format);
        }

        Span<int> streamStart = stackalloc int[8];
        int running = 0;
        for (int s = 0; s < streamStride.Length; s++)
        {
            streamStart[s] = running;
            running += streamStride[s] * vertexCount;
            if (running % 16 != 0) running += 16 - (running % 16);
        }

        int posBase   = streamStart[posCh.Stream] + posCh.Offset;
        int posStride = streamStride[posCh.Stream];
        if (posStride <= 0) return null;

        var positions = new Vector3[vertexCount];
        var world = raw.WorldMatrix;
        for (int i = 0; i < vertexCount; i++)
        {
            int off = posBase + i * posStride;
            if (off + 12 > dataBytes.Length) return null;
            var local = new Vector3(
                BitConverter.ToSingle(dataBytes, off),
                BitConverter.ToSingle(dataBytes, off + 4),
                BitConverter.ToSingle(dataBytes, off + 8));
            positions[i] = Vector3.Transform(local, world);
        }

        // Indices.
        var indexBuffer = TryReadByteArray(field, "m_IndexBuffer");
        if (indexBuffer == null || indexBuffer.Length == 0) return null;

        int indexFormat = field["m_IndexFormat"].AsInt;
        int indexSize = indexFormat == 0 ? 2 : 4;

        var subMeshes = field["m_SubMeshes"]["Array"];
        var tris = new List<int>(subMeshes.Children.Count * 32);

        foreach (var sm in subMeshes.Children)
        {
            int topology = sm["topology"].AsInt;
            if (topology != 0) continue;

            uint firstByte = sm["firstByte"].AsUInt;
            uint indexCount = sm["indexCount"].AsUInt;
            int baseVertex = (int)sm["baseVertex"].AsUInt;

            for (uint i = 0; i < indexCount; i++)
            {
                int off = (int)(firstByte + i * indexSize);
                if (off + indexSize > indexBuffer.Length) break;
                int idx = indexFormat == 0
                    ? BitConverter.ToUInt16(indexBuffer, off)
                    : (int)BitConverter.ToUInt32(indexBuffer, off);
                tris.Add(idx + baseVertex);
            }
        }

        if (tris.Count < 3) return null;

        return new DecodedMesh
        {
            Name = raw.Name,
            OwnerName = raw.OwnerName,
            Positions = positions,
            Triangles = tris.ToArray()
        };
    }

    // Reads a byte payload that may be serialized as either TypelessData
    // (direct byte[]) or vector<UInt8> (wrapped under ["Array"]). Returns
    // null on any access failure rather than crashing the decoder.
    private static byte[]? TryReadByteArray(AssetTypeValueField parent, string fieldName)
    {
        AssetTypeValueField? field;
        try { field = parent[fieldName]; }
        catch { return null; }
        if (field == null) return null;

        try
        {
            var bytes = field.AsByteArray;
            if (bytes != null && bytes.Length > 0) return bytes;
        }
        catch { }

        try
        {
            var inner = field["Array"];
            if (inner != null) return inner.AsByteArray;
        }
        catch { }

        return null;
    }

    private static int FormatSize(int format) => format switch
    {
        0 => 4,                          // Float
        1 => 2,                          // Float16
        2 or 3 or 4 or 5 => 1,           // U/SNorm8, U/SInt8
        6 or 7 => 2,                     // U/SInt16
        8 or 9 => 4,                     // U/SInt32
        _ => 0
    };

    private readonly record struct ChannelInfo(int Stream, int Offset, int Format, int Dimension);
}
