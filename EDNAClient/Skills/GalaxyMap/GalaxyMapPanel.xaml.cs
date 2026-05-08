using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point          = System.Windows.Point;

namespace EDNAClient.Skills.GalaxyMap
{
    public partial class GalaxyMapPanel : System.Windows.Controls.UserControl
    {
        private readonly GalaxyMapViewModel _vm;

        // Camera state
        private Point3D _camPos  = new Point3D(115, 80, 20);
        private double  _yaw     = 0.0;    // 0 = looking toward +Z
        private double  _pitch   = -20.0;  // negative = looking down
        private double  _moveSpeed = 5.0;

        // Input state
        private readonly HashSet<Key> _heldKeys = new HashSet<Key>();
        private Point _dragStart;
        private bool  _mouseDown;

        private readonly DispatcherTimer _moveTimer;

        internal GalaxyMapPanel(GalaxyMapViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            StatusTb.Text = vm.StatusText;
            vm.PropertyChanged += OnViewModelChanged;

            UpdateCamera();

            _moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _moveTimer.Tick += OnMoveTick;
            _moveTimer.Start();
        }

        // ---- ViewModel events ----

        private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GalaxyMapViewModel.VisibleSystems))
                RebuildStarField();
            else if (e.PropertyName == nameof(GalaxyMapViewModel.StatusText))
                StatusTb.Text = _vm.StatusText;
        }

        // ---- Star mesh (octahedra -- 6 vertices, 8 triangles each) ----

        private void RebuildStarField()
        {
            var systems = _vm.VisibleSystems;
            if (systems == null || systems.Length == 0)
            {
                StarField.Content = null;
                UpdateLabels();
                return;
            }

            const double S = 0.5;

            var positions = new Point3DCollection(systems.Length * 6);
            var triangles = new Int32Collection(systems.Length * 24);

            int vi = 0;
            for (int i = 0; i < systems.Length; i++)
            {
                var s = systems[i];
                double x = s.X, y = s.Y, z = s.Z;

                //   v0=+X  v1=-X  v2=+Y  v3=-Y  v4=+Z  v5=-Z
                positions.Add(new Point3D(x + S, y,     z    ));
                positions.Add(new Point3D(x - S, y,     z    ));
                positions.Add(new Point3D(x,     y + S, z    ));
                positions.Add(new Point3D(x,     y - S, z    ));
                positions.Add(new Point3D(x,     y,     z + S));
                positions.Add(new Point3D(x,     y,     z - S));

                // Upper hemisphere (v2 = apex)
                triangles.Add(vi+2); triangles.Add(vi+0); triangles.Add(vi+4);
                triangles.Add(vi+2); triangles.Add(vi+4); triangles.Add(vi+1);
                triangles.Add(vi+2); triangles.Add(vi+1); triangles.Add(vi+5);
                triangles.Add(vi+2); triangles.Add(vi+5); triangles.Add(vi+0);
                // Lower hemisphere (v3 = apex)
                triangles.Add(vi+3); triangles.Add(vi+4); triangles.Add(vi+0);
                triangles.Add(vi+3); triangles.Add(vi+1); triangles.Add(vi+4);
                triangles.Add(vi+3); triangles.Add(vi+5); triangles.Add(vi+1);
                triangles.Add(vi+3); triangles.Add(vi+0); triangles.Add(vi+5);

                vi += 6;
            }

            var mesh  = new MeshGeometry3D { Positions = positions, TriangleIndices = triangles };
            var mat   = new EmissiveMaterial(Brushes.White);
            var model = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            var group = new Model3DGroup();
            group.Children.Add(model);
            StarField.Content = group;

            UpdateLabels();
        }

        // ---- Camera math ----

        private Vector3D Forward()
        {
            double yr = _yaw   * Math.PI / 180.0;
            double pr = _pitch * Math.PI / 180.0;
            return new Vector3D(
                Math.Cos(pr) * Math.Sin(yr),
                Math.Sin(pr),
                Math.Cos(pr) * Math.Cos(yr));
        }

        private Vector3D Right()
        {
            double yr = _yaw * Math.PI / 180.0;
            return new Vector3D(Math.Cos(yr), 0, -Math.Sin(yr));
        }

        private void UpdateCamera()
        {
            Cam.Position      = _camPos;
            Cam.LookDirection = Forward();
            Cam.UpDirection   = new Vector3D(0, 1, 0);
            UpdateLabels();
        }

        // ---- Movement timer ----

        private void OnMoveTick(object? sender, EventArgs e)
        {
            if (_heldKeys.Count == 0) return;

            var fwd   = Forward();
            var right = Right();

            if (_heldKeys.Contains(Key.W)) _camPos += fwd   * _moveSpeed;
            if (_heldKeys.Contains(Key.S)) _camPos -= fwd   * _moveSpeed;
            if (_heldKeys.Contains(Key.A)) _camPos -= right * _moveSpeed;
            if (_heldKeys.Contains(Key.D)) _camPos += right * _moveSpeed;
            if (_heldKeys.Contains(Key.Q)) _camPos -= new Vector3D(0, _moveSpeed, 0);
            if (_heldKeys.Contains(Key.E)) _camPos += new Vector3D(0, _moveSpeed, 0);

            UpdateCamera();
        }

        // ---- Input handlers ----

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            _heldKeys.Add(e.Key);
            e.Handled = e.Key == Key.W || e.Key == Key.S ||
                        e.Key == Key.A || e.Key == Key.D ||
                        e.Key == Key.Q || e.Key == Key.E;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            _heldKeys.Remove(e.Key);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            Focus();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _mouseDown = false;
            _heldKeys.Clear();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(this);
            _mouseDown = true;
            CaptureMouse();
            Focus();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            _mouseDown = false;

            bool wasDrag = (e.GetPosition(this) - _dragStart).Length > 4.0;
            if (!wasDrag)
                HitTest(e.GetPosition(Vp));
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed) return;

            var pos   = e.GetPosition(this);
            var delta = pos - _dragStart;
            _dragStart = pos;

            _yaw   += delta.X * 0.3;
            _pitch -= delta.Y * 0.3;
            _pitch  = Math.Max(-89.0, Math.Min(89.0, _pitch));

            UpdateCamera();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _moveSpeed = Math.Max(0.5, Math.Min(100.0, _moveSpeed * (e.Delta > 0 ? 1.2 : 1.0 / 1.2)));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _moveTimer.Stop();
        }

        // ---- Shared screen-space projection ----

        private readonly struct CamProj
        {
            public readonly double   Width, Height, Aspect, FovScale;
            public readonly Vector3D LookDir, Right, CorrUp;
            public readonly Point3D  CamPos;

            public CamProj(double w, double h, double fovScale,
                           Vector3D look, Vector3D right, Vector3D corrUp, Point3D pos)
            {
                Width = w; Height = h; Aspect = w / h; FovScale = fovScale;
                LookDir = look; Right = right; CorrUp = corrUp; CamPos = pos;
            }

            public bool TryProject(double wx, double wy, double wz,
                                   out double px, out double py)
            {
                double dx = wx - CamPos.X, dy = wy - CamPos.Y, dz = wz - CamPos.Z;
                double cx = dx * Right.X  + dy * Right.Y  + dz * Right.Z;
                double cy = dx * CorrUp.X + dy * CorrUp.Y + dz * CorrUp.Z;
                double cz = dx * LookDir.X + dy * LookDir.Y + dz * LookDir.Z;
                if (cz <= 0.001) { px = py = 0; return false; }
                double sx = cx * FovScale / (cz * Aspect);
                double sy = cy * FovScale / cz;
                px = (sx + 1.0) * Width  / 2.0;
                py = (1.0 - sy) * Height / 2.0;
                return true;
            }
        }

        private bool TryBuildProj(out CamProj proj)
        {
            proj = default;
            double w = Vp.ActualWidth, h = Vp.ActualHeight;
            if (w <= 0 || h <= 0) return false;

            var look  = Cam.LookDirection; look.Normalize();
            var up    = Cam.UpDirection;   up.Normalize();
            var right = Vector3D.CrossProduct(look, up);   right.Normalize();
            var corr  = Vector3D.CrossProduct(right, look); corr.Normalize();
            double fovScale = 1.0 / Math.Tan(Cam.FieldOfView * Math.PI / 180.0 / 2.0);

            proj = new CamProj(w, h, fovScale, look, right, corr, Cam.Position);
            return true;
        }

        // ---- 10 LY name labels (Canvas overlay) ----

        private static readonly System.Windows.Media.FontFamily LabelFont =
            new System.Windows.Media.FontFamily("Consolas");

        private void UpdateLabels()
        {
            LabelCanvas.Children.Clear();
            var nearby = _vm.GetNearbyStars(10.0);
            if (nearby.Length == 0) return;
            if (!TryBuildProj(out var proj)) return;

            foreach (var s in nearby)
            {
                if (!proj.TryProject(s.X, s.Y, s.Z, out double px, out double py)) continue;
                if (px < -100 || px > proj.Width + 100 ||
                    py < -100 || py > proj.Height + 100) continue;

                var tb = new TextBlock
                {
                    Text       = s.Name,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xD8, 0xFF)),
                    FontFamily = LabelFont,
                    FontSize   = 10,
                };
                Canvas.SetLeft(tb, px + 7);
                Canvas.SetTop(tb,  py - 7);
                LabelCanvas.Children.Add(tb);
            }
        }

        // ---- Click hit test ----

        private void HitTest(Point click)
        {
            var systems = _vm.VisibleSystems;
            if (systems == null || systems.Length == 0) return;
            if (!TryBuildProj(out var proj)) return;

            StarSystem? nearest     = null;
            double      nearestDist = 16.0;

            foreach (var s in systems)
            {
                if (!proj.TryProject(s.X, s.Y, s.Z, out double px, out double py)) continue;

                double dx   = px - click.X;
                double dy   = py - click.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest     = s;
                }
            }

            if (nearest != null)
                _vm.SetSelected(nearest);
        }
    }
}
