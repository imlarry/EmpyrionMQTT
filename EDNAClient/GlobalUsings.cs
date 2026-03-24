// Resolve ambiguities introduced by UseWindowsForms + UseWPF
global using Application = System.Windows.Application;
global using Brush        = System.Windows.Media.Brush;
global using Brushes      = System.Windows.Media.Brushes;
global using Timer        = System.Threading.Timer;
