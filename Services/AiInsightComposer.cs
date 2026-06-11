using System.Windows.Media;
using etch_ui.Services.Simulation;
using etch_ui.ViewModels;

namespace etch_ui.Services;

public static class AiInsightComposer
{
    public static IReadOnlyList<AiInsightRow> Compose(
        EtchAiDiagnosis? diag,
        bool flaskReachable,
        bool showSensors,
        double pressureMtorr,
        double vibrationG,
        double tempC,
        double humiPct,
        bool accessSafe,
        bool accessInputValid,
        TmTransferSimulator? transfer)
    {
        var rows = new List<AiInsightRow>();

        if (diag is null || !diag.Success)
        {
            rows.Add(new AiInsightRow
            {
                Category = "종합",
                Signal = flaskReachable ? "AI 대기" : "Flask OFF",
                Detail = flaskReachable
                    ? "sensor-data 수신 후 ML/스텁 진단이 갱신됩니다"
                    : "원격 AI 조언을 받으려면 Flask를 실행하세요",
                SeverityLabel = "정보",
                SeverityBrush = Brushes.DimGray
            });
        }
        else
        {
            (string severity, Brush brush) = ScoreSeverity(diag.AnomalyScore);
            rows.Add(new AiInsightRow
            {
                Category = "종합",
                Signal = $"이상 점수 {diag.AnomalyScore:F2}" + (diag.Stub ? " (규칙)" : " (ML)"),
                Detail = diag.SuggestedAction ?? diag.Note ?? "추가 조치 문구 없음",
                SeverityLabel = severity,
                SeverityBrush = brush
            });

            string pred = diag.PredictedAlarm?.Trim().ToUpperInvariant() ?? "NONE";
            if (pred is not ("" or "NONE"))
            {
                rows.Add(new AiInsightRow
                {
                    Category = "예측",
                    Signal = $"예상 알람 {pred}",
                    Detail = $"신뢰 {diag.PredictionConfidence:P0} · 인터락·Start 자동 변경 없음(조언만)",
                    SeverityLabel = "주의",
                    SeverityBrush = Brushes.DarkGoldenrod
                });
            }

            if (diag.TopSignals is { Count: > 0 })
            {
                foreach (string signal in diag.TopSignals.Take(5))
                {
                    rows.Add(new AiInsightRow
                    {
                        Category = "ML",
                        Signal = signal,
                        Detail = "모델 feature 기여 (Flask topSignals)",
                        SeverityLabel = "근거",
                        SeverityBrush = Brushes.SteelBlue
                    });
                }
            }
        }

        if (showSensors)
        {
            AppendSensorRow(rows, "압력", pressureMtorr,
                AppSettings.PressureMtorrMin, AppSettings.PressureMtorrMax, "mTorr",
                "F" + AppSettings.PressureDecimals);
            AppendSensorRow(rows, "진동", vibrationG, 0, AppSettings.VibrationGMax, "g", "F2");
            AppendSensorRow(rows, "온도", tempC, AppSettings.TempCMin, AppSettings.TempCMax, "℃", "F1");
            AppendSensorRow(rows, "습도", humiPct, AppSettings.HumiMin, AppSettings.HumiMax, "%", "F1");

            if (accessInputValid && !accessSafe)
            {
                rows.Add(new AiInsightRow
                {
                    Category = "인터락",
                    Signal = "Load Lock 열림",
                    Detail = "접촉 DI5 · A004 계열 알람·이송 차단 검토",
                    SeverityLabel = "경고",
                    SeverityBrush = Brushes.OrangeRed
                });
            }
        }

        if (transfer is not null)
        {
            if (transfer.IsSchedulerHold)
            {
                rows.Add(new AiInsightRow
                {
                    Category = "스케줄",
                    Signal = "TM HOLD",
                    Detail = transfer.PhaseHint,
                    SeverityLabel = "주의",
                    SeverityBrush = Brushes.DarkGoldenrod
                });
            }

            (int efemQ, int vacQ, int vacPending, int vacBlades) = transfer.GetQueueDiagnostics();
            if (vacPending > 0 || efemQ > 3 || vacQ > 3)
            {
                rows.Add(new AiInsightRow
                {
                    Category = "스케줄",
                    Signal = "큐 적체",
                    Detail = $"EFEM {efemQ} · Vacuum {vacQ} · drop대기 {vacPending} · Blade {vacBlades}/{transfer.VacuumBladeCapacity}",
                    SeverityLabel = vacPending > 0 ? "주의" : "정보",
                    SeverityBrush = vacPending > 0 ? Brushes.DarkGoldenrod : Brushes.SlateGray
                });
            }

            if (transfer.ClusterState.IsSideStorageAwaitingCassetteSwap)
            {
                rows.Add(new AiInsightRow
                {
                    Category = "스케줄",
                    Signal = "Side Stg 만석",
                    Detail = "카세트 교체 또는 Side 비우기 후 재개",
                    SeverityLabel = "경고",
                    SeverityBrush = Brushes.OrangeRed
                });
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new AiInsightRow
            {
                Category = "종합",
                Signal = "정상",
                Detail = "현재 편차·HOLD·큐 이상 없음",
                SeverityLabel = "정상",
                SeverityBrush = Brushes.ForestGreen
            });
        }

        return rows;
    }

    private static void AppendSensorRow(
        List<AiInsightRow> rows,
        string name,
        double value,
        double min,
        double max,
        string unit,
        string format)
    {
        if (value >= min && value <= max)
        {
            return;
        }

        string direction = value < min ? "하한 미만" : "상한 초과";
        rows.Add(new AiInsightRow
        {
            Category = "센서",
            Signal = $"{name} {value.ToString(format)} {unit}",
            Detail = $"{direction} · 허용 {min.ToString(format)}–{max.ToString(format)} {unit}",
            SeverityLabel = "편차",
            SeverityBrush = Brushes.OrangeRed
        });
    }

    private static (string Label, Brush Brush) ScoreSeverity(double score) => score switch
    {
        >= 0.75 => ("경고", Brushes.OrangeRed),
        >= 0.45 => ("주의", Brushes.DarkGoldenrod),
        _ => ("정상", Brushes.ForestGreen)
    };
}
