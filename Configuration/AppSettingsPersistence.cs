using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using etch_ui;

namespace etch_ui.Configuration;

public static class AppSettingsPersistence
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string SettingsFilePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static AppSettingsSnapshot Load()
    {
        string path = SettingsFilePath;
        if (!File.Exists(path))
        {
            return new AppSettingsSnapshot();
        }

        try
        {
            string json = File.ReadAllText(path);
            AppSettingsSnapshot? model = JsonSerializer.Deserialize<AppSettingsSnapshot>(json, WriteOptions);
            return model ?? new AppSettingsSnapshot();
        }
        catch
        {
            return AppSettingsSnapshot.FromCurrent();
        }
    }

    public static bool TryValidate(AppSettingsSnapshot snapshot, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(snapshot.FlaskBaseUrl))
        {
            errorMessage = "Flask URL을 입력하세요.";
            return false;
        }

        if (snapshot.AdsPort <= 0 || snapshot.AdsPort > 65535)
        {
            errorMessage = "ADS 포트는 1~65535 범위여야 합니다.";
            return false;
        }

        InterlockThresholds il = snapshot.Interlock;
        if (il.PressureMtorrMin >= il.PressureMtorrMax)
        {
            errorMessage = "압력 하한은 상한보다 작아야 합니다.";
            return false;
        }

        if (il.PressureMtorrMin < 0 || il.PressureMtorrMax <= 0)
        {
            errorMessage = "압력 범위(mTorr)는 0보다 커야 합니다.";
            return false;
        }

        if (il.VibrationGMax <= 0)
        {
            errorMessage = "진동 상한(g)은 0보다 커야 합니다.";
            return false;
        }

        if (il.TempCMin >= il.TempCMax)
        {
            errorMessage = "온도 하한은 상한보다 작아야 합니다.";
            return false;
        }

        if (il.HumiMin >= il.HumiMax)
        {
            errorMessage = "습도 하한은 상한보다 작아야 합니다.";
            return false;
        }

        if (il.HumiMin < 0 || il.HumiMax > 100)
        {
            errorMessage = "습도는 0~100 % 범위로 설정하세요.";
            return false;
        }

        PressureScaleSettings ps = snapshot.PressureScale;
        if (ps.RawMin >= ps.RawMax)
        {
            errorMessage = "압력 Raw 하한은 상한보다 작아야 합니다.";
            return false;
        }

        if (ps.MtorrAtRawMax <= ps.MtorrAtRawMin)
        {
            errorMessage = "압력 스케일: RawMax mTorr는 RawMin mTorr보다 커야 합니다.";
            return false;
        }

        if (ps.Decimals is < 0 or > 3)
        {
            errorMessage = "압력 소수 자릿수는 0~3입니다.";
            return false;
        }

        ProcessRecipeSettings recipe = snapshot.ProcessRecipe;
        if (!TryValidateProcessTicks(recipe.EtchProcessTicks, "식각(Etch)", out errorMessage)
            || !TryValidateProcessTicks(recipe.StripProcessTicks, "Strip", out errorMessage)
            || !TryValidateProcessTicks(recipe.AlignProcessTicks, "Aligner", out errorMessage))
        {
            return false;
        }

        if (recipe.StripProcessTicks > recipe.EtchProcessTicks)
        {
            errorMessage = "Strip tick은 Etch tick 이하로 두는 것을 권장합니다(현장 정책).";
            return false;
        }

        if (!ProcessRecipePmMapping.TryValidateSequence(recipe.EtchPmSequence, out errorMessage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(recipe.RecipeName))
        {
            errorMessage = "레시피 이름을 입력하세요.";
            return false;
        }

        return true;
    }

    private static bool TryValidateProcessTicks(int ticks, string label, out string errorMessage)
    {
        if (ticks is < 1 or > 50_000)
        {
            errorMessage = $"{label} 가공 tick은 1~50000 범위여야 합니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool TrySave(AppSettingsSnapshot snapshot, out string errorMessage)
    {
        if (!TryValidate(snapshot, out errorMessage))
        {
            return false;
        }

        try
        {
            snapshot.FlaskBaseUrl = snapshot.FlaskBaseUrl.Trim().TrimEnd('/');
            string json = JsonSerializer.Serialize(snapshot, WriteOptions);
            File.WriteAllText(SettingsFilePath, json);
            AppSettings.ReloadFromDisk();
            ProcessRecipeXml.SyncFromSnapshot(snapshot);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"저장 실패: {ex.Message}";
            return false;
        }
    }
}
