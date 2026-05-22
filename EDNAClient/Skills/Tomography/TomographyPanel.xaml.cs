using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using UserControl     = System.Windows.Controls.UserControl;
using MouseButton     = System.Windows.Input.MouseButton;
using MouseEventArgs  = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs  = System.Windows.Input.MouseWheelEventArgs;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace EDNAClient.Skills.Tomography
{
    public partial class TomographyPanel : UserControl
    {
        private readonly TomographyDocument _document;

        // Orbit state in spherical coords around the mesh center.
        private double _yaw;     // radians around Y
        private double _pitch;   // radians up/down
        private double _distance;
        private double _panX;
        private double _panY;

        private Point _lastMouse;
        private bool _orbiting;
        private bool _panning;

        public TomographyPanel(TomographyDocument document)
        {
            InitializeComponent();
            _document = document;
            DataContext = document;

            // Initial camera distance: enough to frame the mesh diagonal at the camera FOV.
            _distance = Math.Max(20.0, document.Diagonal * 1.2);
            _yaw = Math.PI * 0.25;
            _pitch = Math.PI * 0.18;

            UpdateCamera();
            RebuildVisual();

            document.PropertyChanged += OnDocumentPropertyChanged;

            MouseDown    += OnMouseDown;
            MouseUp      += OnMouseUp;
            MouseMove    += OnMouseMove;
            MouseWheel   += OnMouseWheel;
            Unloaded     += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TomographyDocument.Mesh))
                RebuildVisual();
        }

        // ── 3D content ────────────────────────────────────────────────────────

        private void RebuildVisual()
        {
            var mesh = _document.Mesh;
            if (mesh == null)
            {
                MeshHost.Content = null;
                return;
            }

            // Soft teal "scan" material -- diffuse plus a subtle specular highlight.
            var diffuse = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x60, 0xB8, 0xC8)));
            var specular = new SpecularMaterial(new SolidColorBrush(Color.FromRgb(0x80, 0xC0, 0xE0)), 24);
            var material = new MaterialGroup();
            material.Children.Add(diffuse);
            material.Children.Add(specular);

            var model = new GeometryModel3D
            {
                Geometry     = mesh,
                Material     = material,
                BackMaterial = diffuse,
            };

            MeshHost.Content = model;

            // Re-frame camera around the (possibly new) mesh.
            _distance = Math.Max(20.0, _document.Diagonal * 1.2);
            UpdateCamera();
        }

        // ── Camera ────────────────────────────────────────────────────────────

        private void UpdateCamera()
        {
            double cy = Math.Cos(_yaw),    sy = Math.Sin(_yaw);
            double cp = Math.Cos(_pitch),  sp = Math.Sin(_pitch);

            // Camera position on a sphere around the origin (mesh is centered there).
            double dx = _distance * cp * sy + _panX;
            double dy = _distance * sp      + _panY;
            double dz = _distance * cp * cy;

            Camera.Position = new Point3D(dx, dy, dz);
            Camera.LookDirection = new Vector3D(-dx + _panX, -dy + _panY, -dz);
            Camera.UpDirection = new Vector3D(0, 1, 0);
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMouse = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Left)
            {
                _orbiting = true;
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
            if (e.ChangedButton == MouseButton.Left)  _orbiting = false;
            if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) _panning = false;
            if (!_orbiting && !_panning) ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_orbiting && !_panning) return;
            var p = e.GetPosition(this);
            double dx = p.X - _lastMouse.X;
            double dy = p.Y - _lastMouse.Y;
            _lastMouse = p;

            if (_orbiting)
            {
                _yaw   -= dx * 0.01;
                _pitch += dy * 0.01;
                const double limit = Math.PI / 2 - 0.05;
                if (_pitch >  limit) _pitch =  limit;
                if (_pitch < -limit) _pitch = -limit;
            }
            else if (_panning)
            {
                // Pan in screen-space; scale to keep feel consistent with zoom level.
                double scale = _distance * 0.002;
                _panX -= dx * scale;
                _panY += dy * scale;
            }

            UpdateCamera();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Geometric zoom -- each notch ~10%.
            double factor = e.Delta > 0 ? 0.9 : 1.1;
            _distance *= factor;
            if (_distance < 2)    _distance = 2;
            if (_distance > 5000) _distance = 5000;
            UpdateCamera();
        }
    }
}
