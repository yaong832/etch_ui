using System.Globalization;
using System.Windows;
using etch_ui.Configuration;
using etch_ui.Security;
using etch_ui.Services;

namespace etch_ui;

public partial class InterlockSettingsWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly EtchFlaskClient? _flask;
    private readonly Func<string>? _resolveDataSource;
    private AppSettingsSnapshot _snapshot;

    public InterlockSettingsWindow(
        DatabaseService databaseService,
        EtchFlaskClient? flask = null,
        Func<string>? resolveDataSource = null)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _flask = flask;
        _resolveDataSource = resolveDataSource;
        _snapshot = AppSettingsPersistence.Load();
        TxtPath.Text = AppSettingsPersistence.SettingsFilePath;
        BindToForm(_snapshot);
    }

    private void BindToForm(AppSettingsSnapshot s)
    {
        TxtFlaskUrl.Text = s.FlaskBaseUrl;
        TxtAdsPort.Text = s.AdsPort.ToString(CultureInfo.InvariantCulture);
        ChkSimEnabled.IsChecked = s.SimulationEnabled;

        TxtPressureMin.Text = s.Interlock.PressureMtorrMin.ToString(CultureInfo.InvariantCulture);
        TxtPressureMax.Text = s.Interlock.PressureMtorrMax.ToString(CultureInfo.InvariantCulture);
        TxtVibMax.Text = s.Interlock.VibrationGMax.ToString(CultureInfo.InvariantCulture);
        TxtTempMin.Text = s.Interlock.TempCMin.ToString(CultureInfo.InvariantCulture);
        TxtTempMax.Text = s.Interlock.TempCMax.ToString(CultureInfo.InvariantCulture);
        TxtHumiMin.Text = s.Interlock.HumiMin.ToString(CultureInfo.InvariantCulture);
        TxtHumiMax.Text = s.Interlock.HumiMax.ToString(CultureInfo.InvariantCulture);

        TxtRawMin.Text = s.PressureScale.RawMin.ToString(CultureInfo.InvariantCulture);
        TxtRawMax.Text = s.PressureScale.RawMax.ToString(CultureInfo.InvariantCulture);
        TxtMtorrAtMin.Text = s.PressureScale.MtorrAtRawMin.ToString(CultureInfo.InvariantCulture);
        TxtMtorrAtMax.Text = s.PressureScale.MtorrAtRawMax.ToString(CultureInfo.InvariantCulture);
        TxtDecimals.Text = s.PressureScale.Decimals.ToString(CultureInfo.InvariantCulture);

        TxtEtchTicks.Text = s.ProcessRecipe.EtchProcessTicks.ToString(CultureInfo.InvariantCulture);
        TxtStripTicks.Text = s.ProcessRecipe.StripProcessTicks.ToString(CultureInfo.InvariantCulture);
        TxtAlignTicks.Text = s.ProcessRecipe.AlignProcessTicks.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryReadForm(out AppSettingsSnapshot snapshot, out string error)
    {
        error = string.Empty;
        snapshot = new AppSettingsSnapshot
        {
            FlaskBaseUrl = TxtFlaskUrl.Text.Trim(),
            SimulationEnabled = ChkSimEnabled.IsChecked == true,
            Interlock = new InterlockThresholds(),
            PressureScale = new PressureScaleSettings(),
            ProcessRecipe = new ProcessRecipeSettings()
        };

        if (!int.TryParse(TxtAdsPort.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
        {
            error = "ADS 포트는 정수여야 합니다.";
            return false;
        }

        snapshot.AdsPort = port;

        if (!TryParseDouble(TxtPressureMin.Text, out double pMin, "압력 하한", out error)
            || !TryParseDouble(TxtPressureMax.Text, out double pMax, "압력 상한", out error)
            || !TryParseDouble(TxtVibMax.Text, out double vMax, "진동 상한", out error)
            || !TryParseDouble(TxtTempMin.Text, out double tMin, "온도 하한", out error)
            || !TryParseDouble(TxtTempMax.Text, out double tMax, "온도 상한", out error)
            || !TryParseDouble(TxtHumiMin.Text, out double hMin, "습도 하한", out error)
            || !TryParseDouble(TxtHumiMax.Text, out double hMax, "습도 상한", out error))
        {
            return false;
        }

        snapshot.Interlock.PressureMtorrMin = pMin;
        snapshot.Interlock.PressureMtorrMax = pMax;
        snapshot.Interlock.VibrationGMax = vMax;
        snapshot.Interlock.TempCMin = tMin;
        snapshot.Interlock.TempCMax = tMax;
        snapshot.Interlock.HumiMin = hMin;
        snapshot.Interlock.HumiMax = hMax;

        if (!int.TryParse(TxtRawMin.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawMin))
        {
            error = "Raw Min은 정수여야 합니다.";
            return false;
        }

        if (!int.TryParse(TxtRawMax.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawMax))
        {
            error = "Raw Max는 정수여야 합니다.";
            return false;
        }

        if (!TryParseDouble(TxtMtorrAtMin.Text, out double mMin, "mTorr @ RawMin", out error)
            || !TryParseDouble(TxtMtorrAtMax.Text, out double mMax, "mTorr @ RawMax", out error))
        {
            return false;
        }

        if (!int.TryParse(TxtDecimals.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dec))
        {
            error = "소수 자릿수는 정수여야 합니다.";
            return false;
        }

        snapshot.PressureScale.RawMin = rawMin;
        snapshot.PressureScale.RawMax = rawMax;
        snapshot.PressureScale.MtorrAtRawMin = mMin;
        snapshot.PressureScale.MtorrAtRawMax = mMax;
        snapshot.PressureScale.Decimals = dec;

        if (!int.TryParse(TxtEtchTicks.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int etchTicks))
        {
            error = "Etch tick은 정수여야 합니다.";
            return false;
        }

        if (!int.TryParse(TxtStripTicks.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stripTicks))
        {
            error = "Strip tick은 정수여야 합니다.";
            return false;
        }

        if (!int.TryParse(TxtAlignTicks.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int alignTicks))
        {
            error = "Aligner tick은 정수여야 합니다.";
            return false;
        }

        snapshot.ProcessRecipe.EtchProcessTicks = etchTicks;
        snapshot.ProcessRecipe.StripProcessTicks = stripTicks;
        snapshot.ProcessRecipe.AlignProcessTicks = alignTicks;
        return true;
    }

    private static bool TryParseDouble(string text, out double value, string field, out string error)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            error = $"{field}은(는) 숫자여야 합니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtMsg.Text = string.Empty;
        if (!TryReadForm(out AppSettingsSnapshot snapshot, out string parseErr))
        {
            TxtMsg.Text = parseErr;
            return;
        }

        MessageBoxResult confirm = MessageBox.Show(
            this,
            "인터락·레시피·연결 설정을 저장합니다.\n즉시 HMI에 반영되며, 레시피 tick은 다음 Start부터 적용됩니다.",
            "설정 저장 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK)
        {
            TxtMsg.Text = "저장이 취소되었습니다.";
            TxtMsg.Foreground = System.Windows.Media.Brushes.DimGray;
            return;
        }

        var reauth = new AdminReauthWindow(_databaseService) { Owner = this };
        if (reauth.ShowDialog() != true)
        {
            TxtMsg.Text = "관리자 확인이 취소되었습니다.";
            TxtMsg.Foreground = System.Windows.Media.Brushes.DimGray;
            return;
        }

        if (!AppSettingsPersistence.TrySave(snapshot, out string saveErr))
        {
            TxtMsg.Text = saveErr;
            return;
        }

        _snapshot = snapshot;
        string actor = SessionContext.CurrentUser?.Username ?? "?";
        ProcessRecipeSettings r = snapshot.ProcessRecipe;
        string auditMsg =
            $"설정 저장: 압력 {snapshot.Interlock.PressureMtorrMin}-{snapshot.Interlock.PressureMtorrMax} mTorr, " +
            $"레시피 Etch={r.EtchProcessTicks} Strip={r.StripProcessTicks} Align={r.AlignProcessTicks}";
        _databaseService.AppendEventLog(actor, null, null, auditMsg);
        ForwardSettingsEvent(actor, auditMsg);

        TxtMsg.Foreground = System.Windows.Media.Brushes.ForestGreen;
        TxtMsg.Text = "저장되었습니다. 인터락은 즉시 반영, 레시피 tick은 다음 Start부터 적용됩니다.";
        DialogResult = true;
    }

    private void BtnDefaults_Click(object sender, RoutedEventArgs e)
    {
        BindToForm(new AppSettingsSnapshot());
        TxtMsg.Text = "기본값을 폼에 불러왔습니다. 저장을 눌러야 파일에 반영됩니다.";
        TxtMsg.Foreground = System.Windows.Media.Brushes.DimGray;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ForwardSettingsEvent(string actor, string message)
    {
        if (_flask is null || _resolveDataSource is null)
        {
            return;
        }

        string dataSource = _resolveDataSource();
        if (dataSource == "offline")
        {
            return;
        }

        var item = new FlaskEventItem
        {
            Time = DateTime.UtcNow.ToString("o"),
            Kind = "settings_change",
            Message = message,
            Username = actor
        };
        _ = _flask.TryPostEtchEventsAsync([item], dataSource);
    }
}
