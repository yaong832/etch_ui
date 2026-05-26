using System.IO;
using System.Text.Json;
using etch_ui.Plc;

namespace etch_ui;

/// <summary>
/// 출력 폴더의 appsettings.json (없으면 기본값).
/// </summary>
public static class AppSettings
{
    public static string FlaskBaseUrl { get; private set; } = "http://127.0.0.1:5000";
    public static int AdsPort { get; private set; } = PlcAdsService.DefaultPort;
    public static bool SimulationEnabled { get; private set; }

    /// <summary>인터락·정상 대역 하한 (mTorr). 레시피 확정 후 조정.</summary>
    public static double PressureMtorrMin { get; private set; } = 50.0;

    /// <summary>인터락·정상 대역 상한 (mTorr).</summary>
    public static double PressureMtorrMax { get; private set; } = 150.0;

    public static double VibrationGMax { get; private set; } = 0.80;
    public static double TempCMin { get; private set; } = 20.0;
    public static double TempCMax { get; private set; } = 30.0;
    public static double HumiMin { get; private set; } = 30.0;
    public static double HumiMax { get; private set; } = 55.0;

    public static int PressureRawMin { get; private set; } = 5;
    public static int PressureRawMax { get; private set; } = 3575;
    public static double PressureMtorrAtRawMin { get; private set; }
    public static double PressureMtorrAtRawMax { get; private set; } = 1000.0;
    public static int PressureDecimals { get; private set; } = 1;

    static AppSettings()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("FlaskBaseUrl", out JsonElement f))
            {
                string? u = f.GetString();
                if (!string.IsNullOrWhiteSpace(u))
                {
                    FlaskBaseUrl = u.TrimEnd('/');
                }
            }

            if (root.TryGetProperty("AdsPort", out JsonElement p) && p.TryGetInt32(out int port) && port > 0)
            {
                AdsPort = port;
            }

            if (root.TryGetProperty("SimulationEnabled", out JsonElement s))
            {
                if (s.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    SimulationEnabled = s.GetBoolean();
                }
                else if (s.ValueKind is JsonValueKind.Number && s.TryGetInt32(out int n))
                {
                    SimulationEnabled = n != 0;
                }
            }

            if (root.TryGetProperty("Interlock", out JsonElement il) && il.ValueKind == JsonValueKind.Object)
            {
                if (il.TryGetProperty("PressureMtorrMin", out JsonElement mMin) && mMin.TryGetDouble(out double vMin))
                {
                    PressureMtorrMin = vMin;
                }

                if (il.TryGetProperty("PressureMtorrMax", out JsonElement mMax) && mMax.TryGetDouble(out double vMax))
                {
                    PressureMtorrMax = vMax;
                }

                if (il.TryGetProperty("VibrationGMax", out JsonElement vib) && vib.TryGetDouble(out double vibMax))
                {
                    VibrationGMax = vibMax;
                }

                if (il.TryGetProperty("TempCMin", out JsonElement tMn) && tMn.TryGetDouble(out double tMin))
                {
                    TempCMin = tMin;
                }

                if (il.TryGetProperty("TempCMax", out JsonElement tMx) && tMx.TryGetDouble(out double tMax))
                {
                    TempCMax = tMax;
                }

                if (il.TryGetProperty("HumiMin", out JsonElement hMn) && hMn.TryGetDouble(out double huMin))
                {
                    HumiMin = huMin;
                }

                if (il.TryGetProperty("HumiMax", out JsonElement hMx) && hMx.TryGetDouble(out double huMax))
                {
                    HumiMax = huMax;
                }
            }

            if (root.TryGetProperty("PressureScale", out JsonElement ps) && ps.ValueKind == JsonValueKind.Object)
            {
                if (ps.TryGetProperty("RawMin", out JsonElement rmn) && rmn.TryGetInt32(out int rawMin))
                {
                    PressureRawMin = rawMin;
                }

                if (ps.TryGetProperty("RawMax", out JsonElement rmx) && rmx.TryGetInt32(out int rawMax))
                {
                    PressureRawMax = rawMax;
                }

                if (ps.TryGetProperty("MtorrAtRawMin", out JsonElement mmn) && mmn.TryGetDouble(out double atMin))
                {
                    PressureMtorrAtRawMin = atMin;
                }

                if (ps.TryGetProperty("MtorrAtRawMax", out JsonElement mmx) && mmx.TryGetDouble(out double atMax))
                {
                    PressureMtorrAtRawMax = atMax;
                }

                if (ps.TryGetProperty("Decimals", out JsonElement dec) && dec.TryGetInt32(out int d) && d >= 0 && d <= 3)
                {
                    PressureDecimals = d;
                }
            }
        }
        catch
        {
            // 필드 초기값 유지
        }
    }
}
