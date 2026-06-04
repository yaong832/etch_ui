using etch_ui.Equipment.Models;

namespace etch_ui.Configuration;

/// <summary>가상 공정 레시피 (XML · appsettings · Flask 공유).</summary>
public sealed class ProcessRecipeDefinition
{
    public string Id { get; init; } = "default";
    public string Name { get; init; } = "기본 식각";
    public string Version { get; init; } = "1";
    public string Description { get; init; } = string.Empty;

    public int EtchProcessTicks { get; init; } = 120;
    public int StripProcessTicks { get; init; } = 28;
    public int AlignProcessTicks { get; init; } = 2;

    /// <summary>식각 파이프라인 순서 (예: PM2, PM3, PM4).</summary>
    public IReadOnlyList<string> EtchPmIds { get; init; } = ["PM2", "PM3", "PM4"];

    public string StripPmId { get; init; } = "PM1";

    public static ProcessRecipeDefinition CreateDefault() => new();

    public static ProcessRecipeDefinition FromSettings(ProcessRecipeSettings settings)
    {
        IReadOnlyList<string> pmIds = ProcessRecipePmMapping.ParseSequence(settings.EtchPmSequence);
        return new ProcessRecipeDefinition
        {
            Id = string.IsNullOrWhiteSpace(settings.RecipeId) ? "default" : settings.RecipeId.Trim(),
            Name = string.IsNullOrWhiteSpace(settings.RecipeName) ? "기본 식각" : settings.RecipeName.Trim(),
            Version = string.IsNullOrWhiteSpace(settings.RecipeVersion) ? "1" : settings.RecipeVersion.Trim(),
            Description = settings.Description?.Trim() ?? string.Empty,
            EtchProcessTicks = settings.EtchProcessTicks,
            StripProcessTicks = settings.StripProcessTicks,
            AlignProcessTicks = settings.AlignProcessTicks,
            EtchPmIds = pmIds,
            StripPmId = "PM1"
        };
    }

    public ProcessRecipeSettings ToSettings() => new()
    {
        RecipeId = Id,
        RecipeName = Name,
        RecipeVersion = Version,
        Description = Description,
        EtchProcessTicks = EtchProcessTicks,
        StripProcessTicks = StripProcessTicks,
        AlignProcessTicks = AlignProcessTicks,
        EtchPmSequence = ProcessRecipePmMapping.FormatSequence(EtchPmIds)
    };

    public EquipmentRegion[] ResolveEtchRegions()
    {
        var list = new List<EquipmentRegion>();
        foreach (string pm in EtchPmIds)
        {
            if (ProcessRecipePmMapping.TryToRegion(pm, out EquipmentRegion region)
                && ProcessRecipePmMapping.IsEtchChamberRegion(region)
                && !list.Contains(region))
            {
                list.Add(region);
            }
        }

        return list.Count > 0
            ? list.ToArray()
            : ProcessRecipePmMapping.DefaultEtchRegions;
    }

    public string SummaryText =>
        $"{Name} v{Version} · Etch {string.Join("→", EtchPmIds)} · {EtchProcessTicks}/{StripProcessTicks}/{AlignProcessTicks} tick";
}
