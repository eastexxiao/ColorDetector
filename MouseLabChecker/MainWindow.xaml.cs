using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorDetector
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        private DispatcherTimer _timer;
        private ObservableCollection<ColorInfo> _colorInfos;

        public MainWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 每100毫秒获取一次鼠标位置
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _colorInfos = new ObservableCollection<ColorInfo>();
            ColorDataGrid.ItemsSource = _colorInfos;

            // 注册全局热键 Alt+X
            var hotkeyColor = new HotKey(Key.X, ModifierKeys.Alt, OnHotKeyHandler);

            // 注册全局热键 Alt+P
            var hotkeyScreenshot = new HotKey(Key.P, ModifierKeys.Alt, OnScreenshotHotKeyHandler);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            GetCursorPos(out POINT p);
            var color = GetColorAt(p.X, p.Y);
            RgbTextBlock.Text = $"R: {color.R}, G: {color.G}, B: {color.B}";
            var lab = color.ToLab();
            LabTextBlock.Text = $"L: {lab.L:F2}, A: {lab.A:F2}, B: {lab.B:F2}";
            var hsv = color.ToHsv();
            HsvTextBlock.Text = $"H: {hsv.H:F2}, S: {hsv.S:F2}, V: {hsv.V:F2}";
            var hsl = color.ToHsl();
            HslTextBlock.Text = $"H: {hsl.H:F2}, S: {hsl.S:F2}, L: {hsl.L:F2}";

            // 更新颜色展示框的背景颜色
            ColorDisplayBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        }

        private System.Drawing.Color GetColorAt(int x, int y)
        {
            using (var bmp = new Bitmap(1, 1))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(1, 1));
                }
                return bmp.GetPixel(0, 0);
            }
        }

        private void TopmostCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
        }

        private void TopmostCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
        }

        private void OnScreenshotHotKeyHandler(HotKey hotKey)
        {
            var bounds = new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                }
                Clipboard.SetImage(bitmap.ToBitmapSource());
            }
        }

        private void OnHotKeyHandler(HotKey hotKey)
        {
            GetCursorPos(out POINT p);
            var color = GetColorAt(p.X, p.Y);
            var lab = color.ToLab();
            var hsv = color.ToHsv();
            var hsl = color.ToHsl();

            var backgroundColor = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
            var foregroundColor = GetComplementaryColor(backgroundColor);

            var colorInfo = new ColorInfo
            {
                RGB = $"R: {color.R}, G: {color.G}, B: {color.B}",
                LAB = $"L: {lab.L:F2}, A: {lab.A:F2}, B: {lab.B:F2}",
                HSV = $"H: {hsv.H:F2}, S: {hsv.S:F2}, V: {hsv.V:F2}",
                HSL = $"H: {hsl.H:F2}, S: {hsl.S:F2}, L: {hsl.L:F2}",
                BackgroundColor = new SolidColorBrush(backgroundColor),
                ForegroundColor = new SolidColorBrush(foregroundColor)
            };

            _colorInfos.Add(colorInfo);
        }

        private System.Windows.Media.Color GetComplementaryColor(System.Windows.Media.Color color)
        {
            return System.Windows.Media.Color.FromRgb((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
        }

        private void ClearListButton_Click(object sender, RoutedEventArgs e)
        {
            _colorInfos.Clear();
        }
    }

    public class ColorInfo
    {
        public string RGB { get; set; }
        public string LAB { get; set; }
        public string HSV { get; set; }
        public string HSL { get; set; }
        public SolidColorBrush BackgroundColor { get; set; }
        public SolidColorBrush ForegroundColor { get; set; }
    }

    public class HotKey
    {
        private readonly int _id;
        private bool _isKeyRegistered;

        public HotKey(Key key, ModifierKeys modifierKeys, Action<HotKey> action)
        {
            _id = GetHashCode();
            RegisterHotKey(key, modifierKeys);
            ComponentDispatcher.ThreadPreprocessMessage += (ref MSG msg, ref bool handled) =>
            {
                if (handled)
                    return;

                if (msg.message == 0x0312 && (int)msg.wParam == _id)
                {
                    action(this);
                    handled = true;
                }
            };
        }

        private void RegisterHotKey(Key key, ModifierKeys modifierKeys) 
        {
            var virtualKeyCode = KeyInterop.VirtualKeyFromKey(key);
            _isKeyRegistered = RegisterHotKey(IntPtr.Zero, _id, (uint)modifierKeys, (uint)virtualKeyCode);
        }

        ~HotKey()
        {
            if (_isKeyRegistered)
                UnregisterHotKey(IntPtr.Zero, _id);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    public static class ColorExtensions
    {
        public static (double L, double A, double B) ToLab(this System.Drawing.Color color)
        {
            // 将RGB转换为XYZ
            double r = PivotRgb(color.R / 255.0);
            double g = PivotRgb(color.G / 255.0);
            double b = PivotRgb(color.B / 255.0);

            double x = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
            double y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
            double z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

            // 将XYZ转换为LAB
            x = PivotXyz(x / 0.95047);
            y = PivotXyz(y / 1.00000);
            z = PivotXyz(z / 1.08883);

            double l = Math.Max(0, 116 * y - 16);
            double a = 500 * (x - y);
            double labB = 200 * (y - z);

            return (l, a, labB);
        }

        private static double PivotRgb(double n)
        {
            return (n > 0.04045) ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92;
        }

        private static double PivotXyz(double n)
        {
            return (n > 0.008856) ? Math.Pow(n, 1.0 / 3.0) : (7.787 * n) + (16.0 / 116.0);
        }


        public static (double H, double S, double V) ToHsv(this System.Drawing.Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta != 0)
            {
                if (max == r)
                {
                    h = (g - b) / delta + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / delta + 2;
                }
                else
                {
                    h = (r - g) / delta + 4;
                }
                h /= 6;
            }

            double s = max == 0 ? 0 : delta / max;
            double v = max;

            return (h * 360, s * 100, v * 100);
        }



        public static (double H, double S, double L) ToHsl(this System.Drawing.Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta != 0)
            {
                if (max == r)
                {
                    h = (g - b) / delta + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / delta + 2;
                }
                else
                {
                    h = (r - g) / delta + 4;
                }
                h /= 6;
            }

            double l = (max + min) / 2;
            double s = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * l - 1));

            return (h * 360, s * 100, l * 100);
        }
    }
    public static class BitmapExtensions
    {
        public static BitmapSource ToBitmapSource(this Bitmap bitmap)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}
