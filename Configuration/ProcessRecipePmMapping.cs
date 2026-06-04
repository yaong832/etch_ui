using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

namespace etch_ui.Configuration;

public static class ProcessRecipePmMapping
{
    public static readonly EquipmentRegion[] DefaultEtchRegions =
    [
        EquipmentRegion.ChamberB,
        EquipmentRegion.ChamberC,
        EquipmentRegion.ChamberD
    ];

    private static readonly Dictionary<string, EquipmentRegion> PmToRegion =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PM1"] = EquipmentRegion.ChamberA,
            ["PM2"] = EquipmentRegion.ChamberB,
            ["PM3"] = EquipmentRegion.ChamberC,
            ["PM4"] = EquipmentRegion.ChamberD,
            ["STRIP"] = EquipmentRegion.ChamberA,
            ["ETCH2"] = EquipmentRegion.ChamberB,
            ["ETCH3"] = EquipmentRegion.ChamberC,
            ["ETCH4"] = EquipmentRegion.ChamberD,
        };

    public static bool IsEtchChamberRegion(EquipmentRegion region) =>
        region is EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD;

    public static bool TryToRegion(string? pmId, out EquipmentRegion region)
    {
        region = EquipmentRegion.LoadLock;
        if (string.IsNullOrWhiteSpace(pmId))
        {
            return false;
        }

        string key = pmId.Trim().ToUpperInvariant();
        if (PmToRegion.TryGetValue(key, out region))
        {
            return true;
        }

        return false;
    }

    public static string RegionToPmId(EquipmentRegion region) => region switch
    {
        EquipmentRegion.ChamberA => "PM1",
        EquipmentRegion.ChamberB => "PM2",
        EquipmentRegion.ChamberC => "PM3",
        EquipmentRegion.ChamberD => "PM4",
        _ => region.ToString()
    };

    public static IReadOnlyList<string> ParseSequence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ["PM2", "PM3", "PM4"];
        }

        List<string> list = [];
        foreach (string part in text.Split([',', ';', ' ', '→', '>'], StringSplitOptions.RemoveEmptyEntries))
        {
            string pm = part.Trim().ToUpperInvariant();
            if (pm.Length == 0)
            {
                continue;
            }

            if (!pm.StartsWith("PM", StringComparison.Ordinal))
            {
                pm = "PM" + pm.TrimStart('P', 'M');
            }

            if (TryToRegion(pm, out EquipmentRegion region) && IsEtchChamberRegion(region) && !list.Contains(pm))
            {
                list.Add(pm);
            }
        }

        return list.Count > 0 ? list : ["PM2", "PM3", "PM4"];
    }

    public static string FormatSequence(IReadOnlyList<string> pmIds) =>
        string.Join(",", pmIds);

    public static bool TryValidateSequence(string? text, out string errorMessage)
    {
        IReadOnlyList<string> ids = ParseSequence(text);
        if (ids.Count == 0)
        {
            errorMessage = "식각 PM 순서에 PM2~PM4 중 하나 이상 필요합니다.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
