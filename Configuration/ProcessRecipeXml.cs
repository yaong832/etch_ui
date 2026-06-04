using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace etch_ui.Configuration;

public static class ProcessRecipeXml
{
    public static string DefaultRecipePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipes", "default.process.xml");

    public static ProcessRecipeDefinition LoadOrDefault(string? path = null)
    {
        string file = string.IsNullOrWhiteSpace(path) ? DefaultRecipePath : path;
        if (!File.Exists(file))
        {
            return ProcessRecipeDefinition.CreateDefault();
        }

        try
        {
            return Load(file);
        }
        catch
        {
            return ProcessRecipeDefinition.CreateDefault();
        }
    }

    public static ProcessRecipeDefinition Load(string path)
    {
        XDocument doc = XDocument.Load(path);
        XElement root = doc.Root ?? throw new InvalidDataException("ProcessRecipe 루트가 없습니다.");
        if (!string.Equals(root.Name.LocalName, "ProcessRecipe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("ProcessRecipe 요소가 필요합니다.");
        }

        var etchPmIds = new List<string>();
        XElement? pipeline = root.Element("EtchPipeline");
        if (pipeline is not null)
        {
            foreach (XElement step in pipeline.Elements("Step"))
            {
                bool enabled = !string.Equals(step.Attribute("enabled")?.Value, "false", StringComparison.OrdinalIgnoreCase);
                string? pm = step.Attribute("pm")?.Value?.Trim();
                if (enabled && !string.IsNullOrWhiteSpace(pm))
                {
                    etchPmIds.Add(pm.ToUpperInvariant());
                }
            }
        }

        if (etchPmIds.Count == 0)
        {
            etchPmIds.AddRange(["PM2", "PM3", "PM4"]);
        }

        XElement? timing = root.Element("Timing");
        return new ProcessRecipeDefinition
        {
            Id = root.Attribute("id")?.Value?.Trim() ?? "default",
            Name = root.Attribute("name")?.Value?.Trim() ?? "기본 식각",
            Version = root.Attribute("version")?.Value?.Trim() ?? "1",
            Description = root.Element("Description")?.Value?.Trim() ?? string.Empty,
            EtchProcessTicks = ReadTick(timing?.Element("Etch"), 120),
            StripProcessTicks = ReadTick(timing?.Element("Strip"), 28),
            AlignProcessTicks = ReadTick(timing?.Element("Align"), 2),
            EtchPmIds = etchPmIds,
            StripPmId = root.Element("Strip")?.Attribute("pm")?.Value?.Trim().ToUpperInvariant() ?? "PM1"
        };
    }

    public static void Save(string path, ProcessRecipeDefinition recipe)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("ProcessRecipe",
                new XAttribute("id", recipe.Id),
                new XAttribute("version", recipe.Version),
                new XAttribute("name", recipe.Name),
                string.IsNullOrWhiteSpace(recipe.Description)
                    ? null
                    : new XElement("Description", recipe.Description),
                new XElement("Timing",
                    new XElement("Etch", new XAttribute("unit", "tick"), recipe.EtchProcessTicks),
                    new XElement("Strip", new XAttribute("unit", "tick"), recipe.StripProcessTicks),
                    new XElement("Align", new XAttribute("unit", "tick"), recipe.AlignProcessTicks)),
                new XElement("EtchPipeline",
                    recipe.EtchPmIds.Select(pm => new XElement("Step",
                        new XAttribute("pm", pm),
                        new XAttribute("enabled", "true")))),
                new XElement("Strip", new XAttribute("pm", recipe.StripPmId))));

        doc.Save(path);
    }

    public static void SyncFromSnapshot(AppSettingsSnapshot snapshot)
    {
        ProcessRecipeDefinition def = ProcessRecipeDefinition.FromSettings(snapshot.ProcessRecipe);
        Save(DefaultRecipePath, def);
    }

    private static int ReadTick(XElement? element, int fallback)
    {
        if (element is null)
        {
            return fallback;
        }

        string text = element.Value.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0
            ? v
            : fallback;
    }
}
