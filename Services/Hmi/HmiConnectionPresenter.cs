using System.Windows.Media;

namespace etch_ui.Services.Hmi;

/// <summary>EtherCAT·데이터 품질 헤더 칩 문구 (벤치/실측/미연결 통일).</summary>
public static class HmiConnectionPresenter
{
    public readonly record struct StatusPresentation(string Text, Brush Brush);

    public static StatusPresentation DescribePlc(
        bool benchMode,
        bool hasLiveSensorData,
        bool plcConnected,
        bool simulationFallbackEnabled)
    {
        if (benchMode)
        {
            return new StatusPresentation("벤치 · 시뮬", Brushes.Goldenrod);
        }

        if (hasLiveSensorData)
        {
            return new StatusPresentation("EtherCAT · 실측", Brushes.LimeGreen);
        }

        if (plcConnected)
        {
            return new StatusPresentation("EtherCAT · 연결됨", Brushes.Goldenrod);
        }

        return simulationFallbackEnabled
            ? new StatusPresentation("EtherCAT · 미연결 (시뮬)", Brushes.Goldenrod)
            : new StatusPresentation("EtherCAT · 미연결", Brushes.OrangeRed);
    }

    public static StatusPresentation DescribeDataQuality(
        bool hasLiveSensorData,
        bool benchMode,
        DateTime lastSampleUtc)
    {
        if (hasLiveSensorData)
        {
            string t = lastSampleUtc.ToLocalTime().ToString("HH:mm:ss");
            return new StatusPresentation($"실측 ADS · {t}", Brushes.DeepSkyBlue);
        }

        if (benchMode && lastSampleUtc != DateTime.MinValue)
        {
            string t = lastSampleUtc.ToLocalTime().ToString("HH:mm:ss");
            return new StatusPresentation($"벤치 시뮬 · {t}", Brushes.Goldenrod);
        }

        return new StatusPresentation("샘플 없음 · 시뮬 허용 또는 EtherCAT 확인", Brushes.OrangeRed);
    }

    public static string BenchModeHint(bool simulationFallbackEnabled) =>
        simulationFallbackEnabled
            ? "TwinCAT 없음 —「시뮬 허용」ON · 가상 TM·데모 센서 사용"
            : "TwinCAT 미연결 —「시뮬 허용」을 켜거나 EtherCAT을 연결하세요";
}
