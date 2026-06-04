using System.IO;

namespace etch_ui.Configuration;

/// <summary>현재 활성 레시피 (설정 저장·Start 시 갱신).</summary>
public static class ProcessRecipeRuntime
{
    public static ProcessRecipeDefinition Active { get; private set; } = ProcessRecipeDefinition.CreateDefault();

    public static void ReloadFromAppSettings()
    {
        AppSettingsSnapshot snapshot = AppSettingsPersistence.Load();
        Active = ProcessRecipeDefinition.FromSettings(snapshot.ProcessRecipe);
        if (File.Exists(ProcessRecipeXml.DefaultRecipePath))
        {
            try
            {
                ProcessRecipeDefinition xml = ProcessRecipeXml.Load(ProcessRecipeXml.DefaultRecipePath);
                Active = MergeXmlWithSettings(xml, snapshot.ProcessRecipe);
            }
            catch
            {
                // appsettings 기준 유지
            }
        }
    }

    public static void ApplySnapshot(ProcessRecipeSettings settings)
    {
        Active = ProcessRecipeDefinition.FromSettings(settings);
        ProcessRecipeXml.SyncFromSnapshot(new AppSettingsSnapshot { ProcessRecipe = settings });
    }

    private static ProcessRecipeDefinition MergeXmlWithSettings(
        ProcessRecipeDefinition xml,
        ProcessRecipeSettings settings)
    {
        return new ProcessRecipeDefinition
        {
            Id = string.IsNullOrWhiteSpace(settings.RecipeId) ? xml.Id : settings.RecipeId,
            Name = string.IsNullOrWhiteSpace(settings.RecipeName) ? xml.Name : settings.RecipeName,
            Version = string.IsNullOrWhiteSpace(settings.RecipeVersion) ? xml.Version : settings.RecipeVersion,
            Description = string.IsNullOrWhiteSpace(settings.Description) ? xml.Description : settings.Description,
            EtchProcessTicks = settings.EtchProcessTicks > 0 ? settings.EtchProcessTicks : xml.EtchProcessTicks,
            StripProcessTicks = settings.StripProcessTicks > 0 ? settings.StripProcessTicks : xml.StripProcessTicks,
            AlignProcessTicks = settings.AlignProcessTicks > 0 ? settings.AlignProcessTicks : xml.AlignProcessTicks,
            EtchPmIds = ProcessRecipePmMapping.ParseSequence(settings.EtchPmSequence),
            StripPmId = xml.StripPmId
        };
    }
}
