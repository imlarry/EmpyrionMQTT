using System.IO;
using System.Windows;
using EDNAClient.Core;
using Newtonsoft.Json;

namespace EDNAClient.Workspace
{
    internal class WorkspaceState
    {
        // Window position/size stored as individual fields (no Rect dependency on serializer).
        public double Left   { get; set; } = double.NaN;
        public double Top    { get; set; } = double.NaN;
        public double Width  { get; set; } = double.NaN;
        public double Height { get; set; } = double.NaN;

        public string?      LayoutXml   { get; set; }
        public List<string> ExpandedNav { get; set; } = new List<string>();

        // Maps skill ID -> list of open document contentIds, persisted across sessions.
        public Dictionary<string, List<string>> OpenDocuments { get; set; } = new Dictionary<string, List<string>>();

        [JsonIgnore]
        public bool HasBounds => !double.IsNaN(Left) && Width > 0 && Height > 0;

        public void ApplyBounds(Window w)
        {
            if (!HasBounds) return;
            w.Left   = Left;
            w.Top    = Top;
            w.Width  = Width;
            w.Height = Height;
        }

        public void CaptureBounds(Window w)
        {
            Left   = w.Left;
            Top    = w.Top;
            Width  = w.Width;
            Height = w.Height;
        }

        public static WorkspaceState Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    EdnaLogger.Log($"[Workspace] no state file at {path} -- using defaults");
                    return new WorkspaceState();
                }
                var state = JsonConvert.DeserializeObject<WorkspaceState>(File.ReadAllText(path));
                if (state == null)
                {
                    EdnaLogger.Warn("[Workspace] state file deserialized to null -- using defaults");
                    return new WorkspaceState();
                }
                EdnaLogger.Log($"[Workspace] loaded state: bounds={state.Left},{state.Top},{state.Width},{state.Height} expandedNav={state.ExpandedNav.Count} openDocs={state.OpenDocuments.Values.Sum(v => v.Count)} hasLayout={state.LayoutXml != null}");
                return state;
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"[Workspace] failed to load state from {path}: {ex.Message}");
                return new WorkspaceState();
            }
        }

        public void Save(string path)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
                EdnaLogger.Log($"[Workspace] state saved to {path}");
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"[Workspace] failed to save state to {path}: {ex.Message}");
            }
        }
    }
}
