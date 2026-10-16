using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentController.Services;

/// <summary>
/// Checks what would stop Grafana from importing a dashboard of Prometheus queries:
/// valid JSON, required fields, unique panel ids, panel positions inside the grid,
/// datasource references that exist, and no placeholder left in the text.
/// </summary>
public static class GrafanaDashboardChecker
{
    private const int GridWidth = 24;
    private const int MaxErrors = 30;

    private static readonly Regex Placeholder = new(@"\{\{\s*PLACEHOLDER\s*\}\}|\b(TODO|FIXME|XXX)\b");
    private static readonly Regex VariableReference = new(@"^\$\{(\w+)\}$|^\$(\w+)$");

    /// <summary>Returns one message per problem. An empty list means the dashboard can be imported.</summary>
    public static List<string> Check(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return [$"The output is not valid JSON: {ex.Message}"];
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ["The dashboard must be a JSON object"];

            var errors = new List<string>();
            CheckRootFields(root, errors);
            CheckPanels(root, errors);
            CheckTextAndDatasources(root, DefinedVariables(root), errors);

            if (errors.Count > MaxErrors)
            {
                var more = errors.Count - MaxErrors;
                errors = errors.Take(MaxErrors).ToList();
                errors.Add($"... and {more} more problems");
            }

            return errors;
        }
    }

    private static void CheckRootFields(JsonElement root, List<string> errors)
    {
        if (!root.TryGetProperty("panels", out _) && root.TryGetProperty("dashboard", out _))
            errors.Add("The dashboard is wrapped in a 'dashboard' key: the fields must be at the root of the JSON");

        RequireText(root, "uid", "Dashboard", errors);
        RequireText(root, "title", "Dashboard", errors);

        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number)
            errors.Add("Dashboard: field 'schemaVersion' is missing or is not a number");

        if (!root.TryGetProperty("panels", out var panels) || panels.ValueKind != JsonValueKind.Array)
            errors.Add("Dashboard: field 'panels' is missing or is not an array");
    }

    private static void CheckPanels(JsonElement root, List<string> errors)
    {
        if (!root.TryGetProperty("panels", out var panels) || panels.ValueKind != JsonValueKind.Array)
            return;

        CheckPanelList(panels, new HashSet<int>(), errors);
    }

    // A collapsed row holds its own panels, so the list is checked recursively
    private static void CheckPanelList(JsonElement panels, HashSet<int> seenIds, List<string> errors)
    {
        int position = 0;
        foreach (var panel in panels.EnumerateArray())
        {
            position++;
            if (panel.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Panel number {position}: must be an object");
                continue;
            }

            var label = PanelLabel(panel, position);
            CheckId(panel, label, seenIds, errors);
            RequireText(panel, "type", label, errors);
            RequireText(panel, "title", label, errors);
            CheckGridPosition(panel, label, errors);

            var type = panel.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
            if (type == "row")
            {
                if (panel.TryGetProperty("panels", out var nested) && nested.ValueKind == JsonValueKind.Array)
                    CheckPanelList(nested, seenIds, errors);
            }
            else if (type != "text")
            {
                CheckTargets(panel, label, errors);
            }
        }
    }

    private static string PanelLabel(JsonElement panel, int position)
    {
        if (panel.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
            return $"Panel id {id.GetRawText()}";

        return $"Panel number {position}";
    }

    private static void CheckId(JsonElement panel, string label, HashSet<int> seenIds, List<string> errors)
    {
        if (!panel.TryGetProperty("id", out var id) || !id.TryGetInt32(out var value) || value <= 0)
        {
            errors.Add($"{label}: field 'id' is missing or is not a positive integer");
            return;
        }

        if (!seenIds.Add(value))
            errors.Add($"{label}: the id is used by more than one panel");
    }

    private static void CheckGridPosition(JsonElement panel, string label, List<string> errors)
    {
        if (!panel.TryGetProperty("gridPos", out var grid) || grid.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label}: field 'gridPos' is missing");
            return;
        }

        var values = new Dictionary<string, int>();
        foreach (var name in new[] { "x", "y", "w", "h" })
        {
            if (!grid.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number))
            {
                errors.Add($"{label}: gridPos.{name} is missing or is not an integer");
                return;
            }
            values[name] = number;
        }

        if (values["x"] < 0 || values["y"] < 0)
            errors.Add($"{label}: gridPos.x and gridPos.y must not be negative");
        if (values["w"] <= 0 || values["h"] <= 0)
            errors.Add($"{label}: gridPos.w and gridPos.h must be greater than 0");
        if (values["x"] + values["w"] > GridWidth)
            errors.Add($"{label}: gridPos.x + gridPos.w = {values["x"] + values["w"]}, it must not exceed {GridWidth} columns");
    }

    private static void CheckTargets(JsonElement panel, string label, List<string> errors)
    {
        if (!panel.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Array || targets.GetArrayLength() == 0)
        {
            errors.Add($"{label}: field 'targets' is missing or empty");
            return;
        }

        int index = 0;
        foreach (var target in targets.EnumerateArray())
        {
            index++;
            var hasQuery = target.ValueKind == JsonValueKind.Object
                && target.TryGetProperty("expr", out var expr)
                && expr.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(expr.GetString());

            if (!hasQuery)
                errors.Add($"{label}: target number {index} has no 'expr' query");
        }
    }

    private static void RequireText(JsonElement element, string name, string label, List<string> errors)
    {
        var isText = element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString());

        if (!isText)
            errors.Add($"{label}: field '{name}' is missing or empty");
    }

    // Names that "${NAME}" can refer to: the inputs asked at import time and the dashboard variables
    private static HashSet<string> DefinedVariables(JsonElement root)
    {
        var names = new HashSet<string>();
        AddNames(root, "__inputs", names);

        if (root.TryGetProperty("templating", out var templating) && templating.ValueKind == JsonValueKind.Object)
            AddNames(templating, "list", names);

        return names;
    }

    private static void AddNames(JsonElement parent, string property, HashSet<string> names)
    {
        if (!parent.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String)
                names.Add(name.GetString()!);
        }
    }

    private static void CheckTextAndDatasources(
        JsonElement element, HashSet<string> defined, List<string> errors, string path = "dashboard")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = $"{path}.{property.Name}";
                    if (property.Name == "datasource")
                        CheckDatasource(property.Value, defined, childPath, errors);

                    CheckTextAndDatasources(property.Value, defined, errors, childPath);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                    CheckTextAndDatasources(item, defined, errors, $"{path}[{index++}]");
                break;

            case JsonValueKind.String:
                var match = Placeholder.Match(element.GetString()!);
                if (match.Success)
                    errors.Add($"{path}: contains the placeholder '{match.Value}'");
                break;
        }
    }

    private static void CheckDatasource(JsonElement datasource, HashSet<string> defined, string path, List<string> errors)
    {
        var reference = datasource.ValueKind switch
        {
            JsonValueKind.String => datasource.GetString(),
            JsonValueKind.Object when datasource.TryGetProperty("uid", out var uid) && uid.ValueKind == JsonValueKind.String => uid.GetString(),
            _ => null
        };

        if (reference is null)
            return;

        var match = VariableReference.Match(reference);
        if (!match.Success)
            return;

        var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        if (!defined.Contains(name))
            errors.Add($"{path}: the datasource variable '{name}' is not defined in __inputs or in templating.list");
    }
}
