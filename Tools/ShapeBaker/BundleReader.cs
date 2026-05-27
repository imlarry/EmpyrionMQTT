using System.Numerics;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace EmpyrionMQTT.ShapeBaker;

// Loads an Empyrion Unity AssetBundle and yields (shape-name, mesh,
// worldMatrix) tuples by walking every GameObject in the bundle. The
// FBX root GameObject's m_Name matches the shape KEY used by
// BlockShapesWindow.ecf (e.g. "CutCornerE").
//
// Empyrion shape FBXs are not single combined meshes; each shape is a
// small GameObject hierarchy where individual faces live on child
// GameObjects with their own MeshFilter + Transform. We therefore
// breadth-first traverse from each root, accumulate local-to-world
// matrices down the tree, and yield ONE RawMesh per MeshFilter found.
// The caller decodes each submesh against its world matrix and merges
// them into a single mesh per shape name.
//
// If the bundle was built without serialized type-tree info, GetBaseField
// returns null. In that case the caller should drop a classdata.tpk file
// (from the AssetsTools.NET releases page) next to the executable and
// pass its path to the constructor.
internal sealed class BundleReader : IDisposable
{
    private readonly AssetsManager _manager = new();
    private readonly BundleFileInstance _bundle;
    private readonly AssetsFileInstance _assets;

    public BundleReader(string bundlePath, string? classPackagePath = null)
    {
        if (!string.IsNullOrEmpty(classPackagePath) && File.Exists(classPackagePath))
            _manager.LoadClassPackage(classPackagePath);

        _bundle = _manager.LoadBundleFile(bundlePath, true)
            ?? throw new InvalidOperationException($"failed to load bundle: {bundlePath}");

        _assets = _manager.LoadAssetsFileFromBundle(_bundle, 0)
            ?? throw new InvalidOperationException("bundle contained no assets file at index 0");

        if (_manager.ClassPackage != null)
            _manager.LoadClassDatabaseFromPackage(_assets.file.Metadata.UnityVersion);
    }

    // Yield every GameObject root name in the bundle. Diagnostic helper for
    // --list-bundle-roots so we can compare BlocksConfig prefab names against
    // what's actually present (door / walkway investigation, etc.). Skips
    // GameObjects whose m_Name fails to read or is empty.
    public IEnumerable<string> EnumerateRootNames()
    {
        foreach (var rootInfo in _assets.file.GetAssetsOfType(AssetClassID.GameObject))
        {
            AssetTypeValueField? rootField;
            try { rootField = _manager.GetBaseField(_assets, rootInfo); }
            catch { continue; }
            if (rootField == null) continue;

            string name;
            try { name = rootField["m_Name"].AsString ?? ""; }
            catch { continue; }
            if (name.Length == 0) continue;
            yield return name;
        }
    }

    public IEnumerable<RawMesh> EnumerateMeshes(HashSet<string>? nameFilter = null)
    {
        int goCount = 0;
        int rootsInFilter = 0;
        int submeshes = 0;
        var perShape = new Dictionary<string, int>(StringComparer.Ordinal);
        var unresolved = new List<string>();

        foreach (var rootInfo in _assets.file.GetAssetsOfType(AssetClassID.GameObject))
        {
            goCount++;

            AssetTypeValueField? rootField;
            try { rootField = _manager.GetBaseField(_assets, rootInfo); }
            catch { continue; }
            if (rootField == null) continue;

            string name;
            try { name = rootField["m_Name"].AsString ?? ""; }
            catch { continue; }
            if (name.Length == 0) continue;
            if (nameFilter != null && !nameFilter.Contains(name)) continue;
            rootsInFilter++;

            int countBefore = submeshes;
            var visited = new HashSet<long>();
            var queue = new Queue<(AssetsFileInstance file, AssetFileInfo info, AssetTypeValueField field, Matrix4x4 parentWorld)>();
            queue.Enqueue((_assets, rootInfo, rootField, Matrix4x4.Identity));

            while (queue.Count > 0)
            {
                var (file, info, goField, parentWorld) = queue.Dequeue();
                if (!visited.Add(info.PathId)) continue;

                AssetTypeValueField? components;
                try { components = goField["m_Component"]["Array"]; }
                catch { continue; }
                if (components == null) continue;

                AssetTypeValueField? transformPtr = null;
                var meshFilters = new List<AssetExternal>();

                foreach (var comp in components.Children)
                {
                    var compPtr = TryReadComponentPPtr(comp);
                    if (compPtr == null) continue;

                    AssetExternal compAsset;
                    try { compAsset = _manager.GetExtAsset(file, compPtr); }
                    catch { continue; }
                    if (compAsset.info == null) continue;

                    int cls = compAsset.info.TypeId;
                    // MeshFilter is the static-mesh case; SkinnedMeshRenderer
                    // is the animated-mesh case (doors, etc.) and stores its
                    // mesh under the same m_Mesh field, so the downstream
                    // ResolveMeshFromMeshFilter path works for both.
                    if (cls == (int)AssetClassID.MeshFilter ||
                        cls == (int)AssetClassID.SkinnedMeshRenderer)
                        meshFilters.Add(compAsset);
                    else if (cls == (int)AssetClassID.Transform || cls == (int)AssetClassID.RectTransform)
                        transformPtr = compPtr;
                }

                if (transformPtr == null) continue;

                AssetExternal trAsset;
                try { trAsset = _manager.GetExtAsset(file, transformPtr); }
                catch { continue; }
                if (trAsset.info == null) continue;

                AssetTypeValueField? trField;
                try { trField = _manager.GetBaseField(trAsset.file, trAsset.info); }
                catch { continue; }
                if (trField == null) continue;

                var world = ReadLocalMatrix(trField) * parentWorld;

                string ownerName;
                try { ownerName = goField["m_Name"].AsString ?? ""; }
                catch { ownerName = ""; }

                foreach (var mfAsset in meshFilters)
                {
                    var meshField = ResolveMeshFromMeshFilter(mfAsset);
                    if (meshField != null)
                    {
                        submeshes++;
                        yield return new RawMesh(name, meshField, world, ownerName);
                    }
                }

                AssetTypeValueField? children;
                try { children = trField["m_Children"]["Array"]; }
                catch { continue; }
                if (children == null) continue;

                foreach (var childTrPtr in children.Children)
                {
                    AssetExternal childTr;
                    try { childTr = _manager.GetExtAsset(trAsset.file, childTrPtr); }
                    catch { continue; }
                    if (childTr.info == null) continue;

                    AssetTypeValueField? childTrField;
                    try { childTrField = _manager.GetBaseField(childTr.file, childTr.info); }
                    catch { continue; }
                    if (childTrField == null) continue;

                    AssetTypeValueField? childGoPtr;
                    try { childGoPtr = childTrField["m_GameObject"]; }
                    catch { continue; }
                    if (childGoPtr == null) continue;

                    AssetExternal childGo;
                    try { childGo = _manager.GetExtAsset(childTr.file, childGoPtr); }
                    catch { continue; }
                    if (childGo.info == null) continue;

                    AssetTypeValueField? childGoField;
                    try { childGoField = _manager.GetBaseField(childGo.file, childGo.info); }
                    catch { continue; }
                    if (childGoField == null) continue;

                    queue.Enqueue((childGo.file!, childGo.info, childGoField, world));
                }
            }

            int got = submeshes - countBefore;
            if (got > 0) perShape[name] = got;
            else if (unresolved.Count < 10) unresolved.Add(name);
        }

        double avg = perShape.Count > 0 ? (double)submeshes / perShape.Count : 0;
        Console.Error.WriteLine(
            $"BundleReader: scanned {goCount} GameObjects, {rootsInFilter} in filter, "
            + $"{submeshes} submeshes across {perShape.Count} shapes (avg {avg:F1}/shape)");
        if (unresolved.Count > 0)
            Console.Error.WriteLine("first in-filter shapes with no submeshes: " + string.Join(", ", unresolved));
    }

    // Row-vector convention: v_world = v_local * (S * R * T).
    // Defaults to identity if any field access fails.
    private static Matrix4x4 ReadLocalMatrix(AssetTypeValueField transformField)
    {
        try
        {
            var lpos = transformField["m_LocalPosition"];
            var lrot = transformField["m_LocalRotation"];
            var lscl = transformField["m_LocalScale"];

            var pos = new Vector3(lpos["x"].AsFloat, lpos["y"].AsFloat, lpos["z"].AsFloat);
            var rot = new Quaternion(lrot["x"].AsFloat, lrot["y"].AsFloat, lrot["z"].AsFloat, lrot["w"].AsFloat);
            var scl = new Vector3(lscl["x"].AsFloat, lscl["y"].AsFloat, lscl["z"].AsFloat);

            return Matrix4x4.CreateScale(scl)
                 * Matrix4x4.CreateFromQuaternion(rot)
                 * Matrix4x4.CreateTranslation(pos);
        }
        catch { return Matrix4x4.Identity; }
    }

    private AssetTypeValueField? ResolveMeshFromMeshFilter(AssetExternal mfAsset)
    {
        AssetTypeValueField? mfField;
        try { mfField = _manager.GetBaseField(mfAsset.file, mfAsset.info!); }
        catch { return null; }
        if (mfField == null) return null;

        AssetTypeValueField? meshPtr;
        try { meshPtr = mfField["m_Mesh"]; }
        catch { return null; }
        if (meshPtr == null) return null;

        AssetExternal meshAsset;
        try { meshAsset = _manager.GetExtAsset(mfAsset.file, meshPtr); }
        catch { return null; }
        if (meshAsset.info == null || meshAsset.info.TypeId != (int)AssetClassID.Mesh) return null;

        try { return _manager.GetBaseField(meshAsset.file, meshAsset.info); }
        catch { return null; }
    }

    private static AssetTypeValueField? TryReadComponentPPtr(AssetTypeValueField entry)
    {
        // Modern Unity wraps each component slot as { component: PPtr<Component> }.
        try { return entry["component"]; }
        catch { return null; }
    }

    public void Dispose() => _manager.UnloadAll(true);
}

// Name: the FBX root GameObject's name (the shape KEY, e.g. "Cube").
// OwnerName: the m_Name of the GameObject whose MeshFilter we collected
// this mesh from (often a face/sub-object like "Top" or "Cube_LOD1").
internal sealed record RawMesh(string Name, AssetTypeValueField Field, Matrix4x4 WorldMatrix, string OwnerName);
