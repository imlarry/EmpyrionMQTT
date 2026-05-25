using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using UserControl     = System.Windows.Controls.UserControl;
using MouseButton     = System.Windows.Input.MouseButton;
using MouseEventArgs  = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs  = System.Windows.Input.MouseWheelEventArgs;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace EDNAClient.Skills.Tomography
{
    public partial class TomographyPanel : UserControl
    {
        private readonly TomographyDocument _document;

        // Camera state: position in world space + yaw/pitch orientation.
        // LookDirection is computed each frame from yaw/pitch.
        private Point3D _cameraPos;
        private double  _yaw;     // radians, right-positive (drag right -> turn right)
        private double  _pitch;   // radians, up-positive    (drag up    -> look up)

        // Mouse drag state.
        private Point _lastMouse;
        private bool  _looking;
        private bool  _panning;

        // Held-keys + frame timer for smooth WASDC+Space movement.
        private readonly HashSet<Key>  _heldKeys  = new HashSet<Key>();
        private readonly DispatcherTimer _moveTimer;
        private readonly Stopwatch    _moveClock = new Stopwatch();
        private TimeSpan              _lastTick;

        // True once we've framed the initial mesh; subsequent rescans do not jolt
        // the camera away from wherever the user has flown to.
        private bool _initialPoseSet;

        public TomographyPanel(TomographyDocument document)
        {
            InitializeComponent();
            _document = document;
            DataContext = document;

            Focusable = true;
            IsTabStop = true;

            _moveTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _moveTimer.Tick += OnMoveTick;

            RebuildVisual();

            document.PropertyChanged += OnDocumentPropertyChanged;

            MouseDown    += OnMouseDown;
            MouseUp      += OnMouseUp;
            MouseMove    += OnMouseMove;
            MouseWheel   += OnMouseWheel;
            MouseEnter   += OnMouseEnter;
            KeyDown      += OnKeyDown;
            KeyUp        += OnKeyUp;
            Loaded       += OnLoaded;
            Unloaded     += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Focus();
            _moveClock.Restart();
            _lastTick = _moveClock.Elapsed;
            _moveTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _moveTimer.Stop();
            _heldKeys.Clear();
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Grab keyboard focus when the cursor enters the panel so WASD/Space/C
            // work without requiring the user to click first.
            if (!IsKeyboardFocusWithin) Focus();
        }

        private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TomographyDocument.HullMesh) ||
                e.PropertyName == nameof(TomographyDocument.WindowMesh) ||
                e.PropertyName == nameof(TomographyDocument.RedTipMesh) ||
                e.PropertyName == nameof(TomographyDocument.GreenTipMesh) ||
                e.PropertyName == nameof(TomographyDocument.BlueTipMesh))
                RebuildVisual();
        }

        // ── 3D content ────────────────────────────────────────────────────────

        private void RebuildVisual()
        {
            var hullMesh   = _document.HullMesh;
            var windowMesh = _document.WindowMesh;
            var redMesh    = _document.RedTipMesh;
            var greenMesh  = _document.GreenTipMesh;
            var blueMesh   = _document.BlueTipMesh;
            bool haveHull   = hullMesh   != null && hullMesh.Positions   != null && hullMesh.TriangleIndices.Count   > 0;
            bool haveWindow = windowMesh != null && windowMesh.Positions != null && windowMesh.TriangleIndices.Count > 0;
            bool haveRed    = redMesh    != null && redMesh.Positions    != null && redMesh.TriangleIndices.Count    > 0;
            bool haveGreen  = greenMesh  != null && greenMesh.Positions  != null && greenMesh.TriangleIndices.Count  > 0;
            bool haveBlue   = blueMesh   != null && blueMesh.Positions   != null && blueMesh.TriangleIndices.Count   > 0;

            if (!haveHull && !haveWindow && !haveRed && !haveGreen && !haveBlue)
            {
                MeshHost.Content = null;
                return;
            }

            var group = new Model3DGroup();

            if (haveHull)
            {
                // Soft teal "scan" material -- diffuse plus a subtle specular highlight.
                var hullDiffuse  = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x60, 0xB8, 0xC8)));
                var hullSpecular = new SpecularMaterial(new SolidColorBrush(Color.FromRgb(0x80, 0xC0, 0xE0)), 24);
                var hullMaterial = new MaterialGroup();
                hullMaterial.Children.Add(hullDiffuse);
                hullMaterial.Children.Add(hullSpecular);

                group.Children.Add(new GeometryModel3D
                {
                    Geometry     = hullMesh,
                    Material     = hullMaterial,
                    BackMaterial = hullDiffuse,
                });
            }

            if (haveWindow)
            {
                // Translucent ice-blue with a subtle emissive glow -- visibly
                // distinct from the hull while still reading as a continuous
                // part of the structure surface.
                var winDiffuse  = new DiffuseMaterial(
                    new SolidColorBrush(Color.FromArgb(0xC0, 0x70, 0xC0, 0xFF)));
                var winSpecular = new SpecularMaterial(
                    new SolidColorBrush(Color.FromRgb(0xC0, 0xE0, 0xFF)), 48);
                var winEmissive = new EmissiveMaterial(
                    new SolidColorBrush(Color.FromArgb(0x60, 0x80, 0xB0, 0xE0)));
                var winMaterial = new MaterialGroup();
                winMaterial.Children.Add(winDiffuse);
                winMaterial.Children.Add(winSpecular);
                winMaterial.Children.Add(winEmissive);

                group.Children.Add(new GeometryModel3D
                {
                    Geometry     = windowMesh,
                    Material     = winMaterial,
                    BackMaterial = winDiffuse,
                });
            }

            if (haveRed)   AddTipModel(group, redMesh!,   Color.FromRgb(0xE0, 0x20, 0x20));
            if (haveGreen) AddTipModel(group, greenMesh!, Color.FromRgb(0x20, 0xC0, 0x20));
            if (haveBlue)  AddTipModel(group, blueMesh!,  Color.FromRgb(0x30, 0x60, 0xF0));

            MeshHost.Content = group;

            if (!_initialPoseSet)
            {
                Recenter();
                _initialPoseSet = true;
            }
        }

        // Tip-color material: bright diffuse plus a matching emissive so the
        // face reads its color even when the camera angle dims direct lighting.
        // Used for the RotationAtlas / Sharp calibration marker only.
        private static void AddTipModel(Model3DGroup group, MeshGeometry3D mesh, Color color)
        {
            var diffuse  = new DiffuseMaterial(new SolidColorBrush(color));
            var emissive = new EmissiveMaterial(new SolidColorBrush(
                Color.FromRgb((byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2))));
            var mat = new MaterialGroup();
            mat.Children.Add(diffuse);
            mat.Children.Add(emissive);
            group.Children.Add(new GeometryModel3D
            {
                Geometry     = mesh,
                Material     = mat,
                BackMaterial = diffuse,
            });
        }

        // ── Camera ────────────────────────────────────────────────────────────

        // Place the camera 3/4-view back from the origin (mesh center) and frame the
        // bounding diagonal. Invoked on first mesh load and on the R key.
        // After the left-to-right-handed Z flip in TomographyDocument.RebuildMesh,
        // data +Z is render -Z, so the camera sits at render +Z to look toward
        // data +Z (where the rotation strip extends from the core).
        private void Recenter()
        {
            double D = Math.Max(20.0, _document.Diagonal);
            _cameraPos = new Point3D(-D * 0.7, D * 0.4, D * 0.7);
            var look = new Vector3D(-_cameraPos.X, -_cameraPos.Y, -_cameraPos.Z);
            look.Normalize();
            _pitch = Math.Asin(look.Y);
            _yaw   = Math.Atan2(look.X, -look.Z);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            Camera.Position      = _cameraPos;
            Camera.LookDirection = Forward();
            Camera.UpDirection   = new Vector3D(0, 1, 0);
        }

        private Vector3D Forward()
        {
            double cy = Math.Cos(_yaw),   sy = Math.Sin(_yaw);
            double cp = Math.Cos(_pitch), sp = Math.Sin(_pitch);
            return new Vector3D(cp * sy, sp, -cp * cy);
        }

        // Cross(forward, worldUp) normalized -- independent of pitch.
        private Vector3D Right()
        {
            double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw);
            return new Vector3D(cy, 0, sy);
        }

        // Cross(right, forward): the "screen up" direction. Used for right-drag pan.
        private Vector3D ScreenUp()
        {
            var f = Forward();
            var r = Right();
            return new Vector3D(
                r.Y * f.Z - r.Z * f.Y,
                r.Z * f.X - r.X * f.Z,
                r.X * f.Y - r.Y * f.X);
        }

        // ── Mouse input ───────────────────────────────────────────────────────

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            _lastMouse = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Left)
            {
                _looking = true;
                CaptureMouse();
            }
            else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
            {
                _panning = true;
                CaptureMouse();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)  _looking = false;
            if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) _panning = false;
            if (!_looking && !_panning) ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_looking && !_panning)
            {
                UpdateHoverLabel(e.GetPosition(Viewport));
                return;
            }
            var p = e.GetPosition(this);
            double dx = p.X - _lastMouse.X;
            double dy = p.Y - _lastMouse.Y;
            _lastMouse = p;

            if (_looking)
            {
                _yaw   += dx * 0.005;       // drag right -> turn right
                _pitch -= dy * 0.005;       // drag up    -> look up
                const double limit = Math.PI / 2 - 0.05;
                if (_pitch >  limit) _pitch =  limit;
                if (_pitch < -limit) _pitch = -limit;
            }
            else if (_panning)
            {
                // Pan: drag right -> scene moves right -> camera moves left.
                double panScale = Math.Max(_document.Diagonal, 20.0) * 0.0015;
                _cameraPos -= Right()    * (dx * panScale);
                _cameraPos += ScreenUp() * (dy * panScale);
            }

            UpdateCamera();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Wheel = nudge forward / back along the look direction. Scaled to scan size
            // so a small scan and a large scan both feel responsive.
            double step = Math.Max(_document.Diagonal, 20.0) * 0.05;
            if (e.Delta < 0) step = -step;
            _cameraPos += Forward() * step;
            UpdateCamera();
        }

        // ── Keyboard input ────────────────────────────────────────────────────

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // R = recenter. Single-shot, not repeat-driven.
            if (e.Key == Key.R && !e.IsRepeat)
            {
                Recenter();
                e.Handled = true;
                return;
            }

            if (IsMoveKey(e.Key))
            {
                _heldKeys.Add(e.Key);
                e.Handled = true;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (_heldKeys.Remove(e.Key)) e.Handled = true;
        }

        private static bool IsMoveKey(Key k) =>
            k == Key.W || k == Key.A || k == Key.S || k == Key.D ||
            k == Key.C || k == Key.Space ||
            k == Key.LeftShift || k == Key.RightShift;

        private void OnMoveTick(object? sender, EventArgs e)
        {
            var now = _moveClock.Elapsed;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            if (dt <= 0 || dt > 0.25) return;   // skip first tick / long pauses

            if (_heldKeys.Count == 0) return;

            // Base speed scales with scan size so small/large structures feel the same.
            double speed = Math.Max(_document.Diagonal, 20.0) * 0.6;   // units per second
            if (_heldKeys.Contains(Key.LeftShift) || _heldKeys.Contains(Key.RightShift))
                speed *= 3.0;

            var f = Forward();
            var r = Right();
            var u = new Vector3D(0, 1, 0);
            var delta = new Vector3D(0, 0, 0);

            if (_heldKeys.Contains(Key.W))     delta += f;
            if (_heldKeys.Contains(Key.S))     delta -= f;
            if (_heldKeys.Contains(Key.D))     delta += r;
            if (_heldKeys.Contains(Key.A))     delta -= r;
            if (_heldKeys.Contains(Key.Space)) delta += u;
            if (_heldKeys.Contains(Key.C))     delta -= u;

            if (delta.LengthSquared > 0)
            {
                delta.Normalize();
                _cameraPos += delta * speed * dt;
                UpdateCamera();
            }
        }

        // ── Hover-pick (Shape Gallery) ────────────────────────────────────────
        // Hit-test the cursor against the rendered mesh; if it lands inside a
        // gallery label's AABB, name it in the status bar. AABB centers are
        // stored pre-Center-subtract, so we apply the same offset here.
        private void UpdateHoverLabel(Point mouse)
        {
            var labels = _document.GalleryLabels;
            if (labels == null || labels.Count == 0) return;

            var hit = VisualTreeHelper.HitTest(Viewport, mouse) as RayMeshGeometry3DHitTestResult;
            if (hit == null)
            {
                _document.StatusText = _galleryIdleText;
                return;
            }

            var p = hit.PointHit;     // mesh-local (no transforms on MeshHost)
            var c = _document.Center; // labels are in pre-Center coords
            // RebuildMesh centers positions on the bbox midpoint and negates
            // Z to convert Empyrion's left-handed coords to WPF's right-handed
            // ones. Invert both operations here to recover the label's data-
            // space Z.
            double px = p.X + c.X;
            double py = p.Y + c.Y;
            double pz = c.Z - p.Z;

            foreach (var label in labels)
            {
                if (Math.Abs(px - label.CX) <= label.Extent &&
                    Math.Abs(py - label.CY) <= label.Extent &&
                    Math.Abs(pz - label.CZ) <= label.Extent)
                {
                    _document.StatusText = label.TotalVoxels > 0
                        ? $"{label.Name}  ({label.FilledCount}/{label.TotalVoxels} voxels filled)"
                        : label.Name;
                    return;
                }
            }
            _document.StatusText = _galleryIdleText;
        }

        private string _galleryIdleText
        {
            get
            {
                var labels = _document.GalleryLabels;
                if (labels == null || labels.Count == 0) return "";
                return labels[0].TotalVoxels > 0
                    ? $"Shape Gallery: {labels.Count} stamps -- hover for name"
                    : "Hover for cell info";
            }
        }
    }
}
