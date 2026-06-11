using System.IO;
using System.Text.Json;
using System.Windows;

namespace etch_ui.Services.Hmi;

/// <summary>분리 창 위치·크기를 로컬 JSON에 저장 (듀얼 모니터 재배치).</summary>
public static class HmiPopoutLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string LayoutPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "hmi_popout_layout.json");

    private sealed class LayoutFile
    {
        public Dictionary<string, WindowRect> Windows { get; set; } = new();
    }

    private sealed class WindowRect
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Topmost { get; set; }
    }

    public static void TryApply(Window window, HmiPopoutKind kind, double defaultWidth, double defaultHeight)
    {
        window.Width = defaultWidth;
        window.Height = defaultHeight;

        if (!TryLoad(kind, out WindowRect? rect) || rect is null)
        {
            return;
        }

        if (rect.Width < window.MinWidth || rect.Height < window.MinHeight)
        {
            return;
        }

        if (!IsReasonableOnScreen(rect))
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = rect.Left;
        window.Top = rect.Top;
        window.Width = rect.Width;
        window.Height = rect.Height;
        window.Topmost = rect.Topmost;
    }

    /// <summary>주 모니터 오른쪽(일반적인 2번 모니터)으로 이동.</summary>
    public static void ApplySecondaryMonitorPreset(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = SystemParameters.VirtualScreenLeft + SystemParameters.PrimaryScreenWidth + 24;
        window.Top = SystemParameters.VirtualScreenTop + 48;
    }

    public static void Save(Window window, HmiPopoutKind kind)
    {
        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        try
        {
            LayoutFile file = LoadFile();
            file.Windows[kind.ToString()] = new WindowRect
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height,
                Topmost = window.Topmost
            };

            string? dir = Path.GetDirectoryName(LayoutPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(LayoutPath, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch
        {
            // 레이아웃 저장 실패는 HMI 동작에 영향 없음
        }
    }

    private static bool TryLoad(HmiPopoutKind kind, out WindowRect? rect)
    {
        rect = null;
        LayoutFile file = LoadFile();
        return file.Windows.TryGetValue(kind.ToString(), out rect);
    }

    private static LayoutFile LoadFile()
    {
        try
        {
            if (!File.Exists(LayoutPath))
            {
                return new LayoutFile();
            }

            string json = File.ReadAllText(LayoutPath);
            return JsonSerializer.Deserialize<LayoutFile>(json) ?? new LayoutFile();
        }
        catch
        {
            return new LayoutFile();
        }
    }

    private static bool IsReasonableOnScreen(WindowRect rect)
    {
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        if (rect.Width < 320 || rect.Height < 240)
        {
            return false;
        }

        double right = rect.Left + rect.Width;
        double bottom = rect.Top + rect.Height;
        return rect.Left >= vLeft - 80
               && rect.Top >= vTop - 40
               && right <= vLeft + vWidth + 80
               && bottom <= vTop + vHeight + 40;
    }
}
