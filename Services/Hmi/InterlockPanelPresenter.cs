using System.Windows.Media;
using etch_ui.Services;
using etch_ui.ViewModels;

namespace etch_ui.Services.Hmi;

/// <summary>인터락 패널 VM 문구·브러시 (MainWindow·분리 창 공용).</summary>
public static class InterlockPanelPresenter
{
    public static void Apply(
        MainViewModel vm,
        InterlockSensorContext ctx,
        InterlockDecision decision,
        bool effectiveAccessSafe)
    {
        if (ctx.MaintenanceMode)
        {
            ApplyMaintenance(vm, ctx, effectiveAccessSafe);
            return;
        }

        if (ctx.IsBenchMode)
        {
            ApplyBench(vm, ctx);
            return;
        }

        ApplyLive(vm, ctx, decision, effectiveAccessSafe);
    }

    private static void ApplyMaintenance(MainViewModel vm, InterlockSensorContext ctx, bool effectiveAccessSafe)
    {
        bool hasLive = ctx.HasLiveSensorData;
        string pressureFmt = "F" + AppSettings.PressureDecimals;
        string pressureRange =
            $"{AppSettings.PressureMtorrMin.ToString(pressureFmt)}–{AppSettings.PressureMtorrMax.ToString(pressureFmt)} mTorr";
        vm.InterlockPlcText = hasLive ? "[MNT] EtherCAT 연결 (모니터링)" : "[MNT] EtherCAT 미연결";
        vm.InterlockPlcBrush = Brushes.MediumPurple;
        vm.InterlockPressureText = hasLive && ctx.PressureSignalValid
            ? $"[MNT] 압력 {pressureRange}"
            : "[MNT] 압력 신호 없음";
        vm.InterlockPressureBrush = Brushes.MediumPurple;
        vm.InterlockPressureDetailText = hasLive && ctx.PressureSignalValid
            ? $"현재 {ctx.PressureMtorr.ToString(pressureFmt)} mTorr · Start 차단"
            : $"허용 {pressureRange} (참고)";
        vm.InterlockVibText = hasLive ? "[MNT] 진동 모니터링" : "[MNT] 진동 미측정";
        vm.InterlockVibBrush = Brushes.MediumPurple;
        vm.InterlockTempText = hasLive ? "[MNT] 온도 모니터링" : "[MNT] 온도 미측정";
        vm.InterlockTempBrush = Brushes.MediumPurple;
        vm.InterlockHumiText = hasLive ? "[MNT] 습도 모니터링" : "[MNT] 습도 미측정";
        vm.InterlockHumiBrush = Brushes.MediumPurple;
        vm.InterlockAccessText = hasLive && ctx.AccessInputValid
            ? effectiveAccessSafe ? "[MNT] Load Lock 닫힘" : "[MNT] Load Lock 열림"
            : "[MNT] Load Lock 미측정";
        vm.InterlockAccessBrush = Brushes.MediumPurple;
        vm.InterlockResultText = "유지보수 — 공정 시작 불가 · 정비 후 해제";
        vm.InterlockResultBrush = Brushes.MediumPurple;
    }

    private static void ApplyBench(MainViewModel vm, InterlockSensorContext ctx)
    {
        string pressureFmt = "F" + AppSettings.PressureDecimals;
        vm.InterlockPlcText = "[데모] EtherCAT·인터락 미적용";
        vm.InterlockPlcBrush = Brushes.Goldenrod;
        vm.InterlockPressureText = "[데모] 압력(시뮬 표시)";
        vm.InterlockPressureBrush = Brushes.Goldenrod;
        vm.InterlockPressureDetailText =
            $"허용 {AppSettings.PressureMtorrMin.ToString(pressureFmt)}–" +
            $"{AppSettings.PressureMtorrMax.ToString(pressureFmt)} mTorr · 현재 {ctx.PressureMtorr.ToString(pressureFmt)} (참고)";
        vm.InterlockVibText = "[데모] 진동(시뮬)";
        vm.InterlockVibBrush = Brushes.Goldenrod;
        vm.InterlockTempText = "[데모] 온도(시뮬)";
        vm.InterlockTempBrush = Brushes.Goldenrod;
        vm.InterlockHumiText = "[데모] 습도(시뮬)";
        vm.InterlockHumiBrush = Brushes.Goldenrod;
        vm.InterlockAccessText = "[데모] Load Lock(시뮬)";
        vm.InterlockAccessBrush = Brushes.Goldenrod;
        vm.InterlockResultText = "데모 모드 · 가상 이송·로직 확인용 Start 가능";
        vm.InterlockResultBrush = Brushes.DarkGoldenrod;
    }

    private static void ApplyLive(
        MainViewModel vm,
        InterlockSensorContext ctx,
        InterlockDecision decision,
        bool effectiveAccessSafe)
    {
        bool hasLive = ctx.HasLiveSensorData;
        string pressureFmt = "F" + AppSettings.PressureDecimals;
        string pressureRange =
            $"{AppSettings.PressureMtorrMin.ToString(pressureFmt)}–{AppSettings.PressureMtorrMax.ToString(pressureFmt)} mTorr";

        InterlockSeverity pressureSev = hasLive ? decision.PressureSeverity : InterlockSeverity.Alarm;
        InterlockSeverity vibSev = hasLive ? decision.VibrationSeverity : InterlockSeverity.Alarm;
        InterlockSeverity tempSev = hasLive ? decision.TemperatureSeverity : InterlockSeverity.Alarm;
        InterlockSeverity humiSev = hasLive ? decision.HumiditySeverity : InterlockSeverity.Alarm;
        bool interlockAccessOk = hasLive && ctx.AccessInputValid && effectiveAccessSafe;

        if (!hasLive)
        {
            vm.InterlockPlcText = "[－] EtherCAT 샘플 미측정";
            vm.InterlockPlcBrush = Brushes.DimGray;
            vm.InterlockPressureText = "[－] 압력 신호 미측정";
            vm.InterlockPressureBrush = Brushes.DimGray;
            vm.InterlockPressureDetailText = $"허용 {pressureRange} (EtherCAT 미연결/샘플 없음)";
            vm.InterlockVibText = "[－] 진동 신호 미측정";
            vm.InterlockVibBrush = Brushes.DimGray;
            vm.InterlockTempText = "[－] 온도 신호 미측정";
            vm.InterlockTempBrush = Brushes.DimGray;
            vm.InterlockHumiText = "[－] 습도 신호 미측정";
            vm.InterlockHumiBrush = Brushes.DimGray;
        }
        else
        {
            vm.InterlockPlcText = $"[{ToMark(decision.PlcLinkOk)}] EtherCAT/데이터 통신";
            vm.InterlockPlcBrush = ItemBrush(decision.PlcLinkOk);

            vm.InterlockPressureText = ctx.PressureSignalValid
                ? $"[{ToMark(pressureSev)}] 압력 ({pressureRange})"
                : "[✗] 압력 신호 없음";
            vm.InterlockPressureBrush = ctx.PressureSignalValid ? ItemBrush(pressureSev) : Brushes.OrangeRed;

            if (ctx.PressureSignalValid)
            {
                string cur = ctx.PressureMtorr.ToString(pressureFmt);
                string alarmRange =
                    $"{AppSettings.PressureMtorrAlarmMin.ToString(pressureFmt)}–{AppSettings.PressureMtorrAlarmMax.ToString(pressureFmt)} mTorr";
                vm.InterlockPressureDetailText =
                    $"정상 {pressureRange}  ·  알람 {alarmRange}  ·  현재 {cur} mTorr" +
                    (pressureSev == InterlockSeverity.Ok ? "" : pressureSev == InterlockSeverity.Warning ? "  ← 경고" : "  ← 알람");
            }
            else
            {
                vm.InterlockPressureDetailText = $"허용 {pressureRange} (압력 신호 없음)";
            }

            vm.InterlockVibText = $"[{ToMark(vibSev)}] 진동 (정상 ≤{AppSettings.VibrationGMax:F2} g)";
            vm.InterlockVibBrush = ItemBrush(vibSev);
            vm.InterlockTempText = $"[{ToMark(tempSev)}] 온도 (정상 {AppSettings.TempCMin:F0}–{AppSettings.TempCMax:F0} ℃)";
            vm.InterlockTempBrush = ItemBrush(tempSev);
            vm.InterlockHumiText = $"[{ToMark(humiSev)}] 습도 (정상 {AppSettings.HumiMin:F0}–{AppSettings.HumiMax:F0} %)";
            vm.InterlockHumiBrush = ItemBrush(humiSev);
        }

        if (!hasLive || !ctx.AccessInputValid)
        {
            vm.InterlockAccessText = "[－] Load Lock 접촉 미측정";
            vm.InterlockAccessBrush = Brushes.DimGray;
        }
        else
        {
            vm.InterlockAccessText = $"[{ToMark(interlockAccessOk)}] Load Lock 접촉(닫힘)";
            vm.InterlockAccessBrush = ItemBrush(interlockAccessOk);
        }

        vm.InterlockResultText = decision.ProductionInterlockOk ? "공정 시작 가능" : "공정 시작 불가";
        vm.InterlockResultBrush = decision.ProductionInterlockOk ? Brushes.ForestGreen : Brushes.OrangeRed;
    }

    public static Brush ItemBrush(InterlockSeverity severity) => severity switch
    {
        InterlockSeverity.Ok => Brushes.ForestGreen,
        InterlockSeverity.Warning => Brushes.Goldenrod,
        _ => Brushes.OrangeRed
    };

    public static Brush ItemBrush(bool ok) => ok ? Brushes.ForestGreen : Brushes.OrangeRed;

    private static string ToMark(InterlockSeverity severity) => severity switch
    {
        InterlockSeverity.Ok => "✓",
        InterlockSeverity.Warning => "!",
        _ => "✗"
    };

    private static string ToMark(bool ok) => ok ? "✓" : "✗";
}
