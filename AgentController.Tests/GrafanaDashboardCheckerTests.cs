using System.Text.Json;
using System.Text.RegularExpressions;
using AgentController.Models;
using AgentController.Services;
using AgentController.Tests.Support;

namespace AgentController.Tests;

public class GrafanaDashboardCheckerTests
{
    [Fact]
    public void A_valid_dashboard_has_no_problem()
    {
        Assert.Empty(GrafanaDashboardChecker.Check(Fixtures.ValidDashboard));
    }

    [Fact]
    public void The_example_in_the_architect_prompt_passes_the_check()
    {
        var prompt = File.ReadAllText(Fixtures.RepositoryFile("pipelines/grafana-k6/architect.md"));
        var example = Regex.Match(prompt, "```json\n(.*?)\n```", RegexOptions.Singleline).Groups[1].Value;

        Assert.Empty(GrafanaDashboardChecker.Check(example));
    }

    [Fact]
    public void Text_that_is_not_json_is_reported_with_the_position()
    {
        var problems = GrafanaDashboardChecker.Check("{ \"uid\": \"a\", }");

        var problem = Assert.Single(problems);
        Assert.StartsWith("The output is not valid JSON", problem);
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    public void The_root_must_be_an_object(string json)
    {
        Assert.Equal(["The dashboard must be a JSON object"], GrafanaDashboardChecker.Check(json));
    }

    [Fact]
    public void A_dashboard_wrapped_in_a_dashboard_key_is_reported()
    {
        var problems = GrafanaDashboardChecker.Check("""{ "dashboard": { "uid": "a", "title": "t", "panels": [] } }""");

        Assert.Contains(problems, p => p.Contains("wrapped in a 'dashboard' key"));
    }

    [Theory]
    [InlineData("\"uid\": \"k6-test\",", "Dashboard: field 'uid' is missing or empty")]
    [InlineData("\"title\": \"k6 test\",", "Dashboard: field 'title' is missing or empty")]
    [InlineData("\"schemaVersion\": 41,", "Dashboard: field 'schemaVersion' is missing or is not a number")]
    public void Required_dashboard_fields_are_reported(string removed, string expected)
    {
        var problems = GrafanaDashboardChecker.Check(Fixtures.ValidDashboard.Replace(removed, ""));

        Assert.Contains(expected, problems);
    }

    [Fact]
    public void Panels_must_be_an_array()
    {
        var problems = GrafanaDashboardChecker.Check("""{ "uid": "a", "title": "t", "schemaVersion": 41 }""");

        Assert.Contains("Dashboard: field 'panels' is missing or is not an array", problems);
    }

    [Fact]
    public void Two_panels_with_the_same_id_are_reported()
    {
        var problems = GrafanaDashboardChecker.Check(Fixtures.ValidDashboard.Replace("\"id\": 2", "\"id\": 1"));

        Assert.Contains("Panel id 1: the id is used by more than one panel", problems);
    }

    [Theory]
    [InlineData("\"id\": 2,", "Panel number 2: field 'id' is missing or is not a positive integer")]
    [InlineData("\"id\": 2", "")]
    public void A_panel_without_a_usable_id_is_reported(string removed, string expected)
    {
        if (expected == "")
            return;

        var problems = GrafanaDashboardChecker.Check(Fixtures.ValidDashboard.Replace(removed, ""));

        Assert.Contains(expected, problems);
    }

    [Theory]
    [InlineData("\"x\": 0, \"y\": 1, \"w\": 24, \"h\": 10", "\"x\": 12, \"y\": 1, \"w\": 24, \"h\": 10", "Panel id 2: gridPos.x + gridPos.w = 36, it must not exceed 24 columns")]
    [InlineData("\"x\": 0, \"y\": 1, \"w\": 24, \"h\": 10", "\"x\": -1, \"y\": 1, \"w\": 24, \"h\": 10", "Panel id 2: gridPos.x and gridPos.y must not be negative")]
    [InlineData("\"x\": 0, \"y\": 1, \"w\": 24, \"h\": 10", "\"x\": 0, \"y\": 1, \"w\": 0, \"h\": 10", "Panel id 2: gridPos.w and gridPos.h must be greater than 0")]
    [InlineData("\"x\": 0, \"y\": 1, \"w\": 24, \"h\": 10", "\"x\": 0, \"y\": 1, \"w\": 24", "Panel id 2: gridPos.h is missing or is not an integer")]
    public void A_grid_position_outside_the_grid_is_reported(string original, string replacement, string expected)
    {
        var problems = GrafanaDashboardChecker.Check(Fixtures.ValidDashboard.Replace(original, replacement));

        Assert.Contains(expected, problems);
    }

    [Fact]
    public void A_panel_without_grid_position_is_reported()
    {
        var problems = GrafanaDashboardChecker.Check(
            Fixtures.ValidDashboard.Replace("\"gridPos\": { \"x\": 0, \"y\": 1, \"w\": 24, \"h\": 10 },", ""));

        Assert.Contains("Panel id 2: field 'gridPos' is missing", problems);
    }

    [Fact]
    public void A_query_panel_needs_targets_with_a_query()
    {
        var withoutTargets = Fixtures.ValidDashboard.Replace(
            "\"targets\": [ { \"refId\": \"A\", \"expr\": \"max(k6_http_req_duration_p95)\", \"legendFormat\": \"{{__name__}}\" } ]",
            "\"targets\": []");
        var emptyQuery = Fixtures.ValidDashboard.Replace("max(k6_http_req_duration_p95)", " ");

        Assert.Contains("Panel id 2: field 'targets' is missing or empty", GrafanaDashboardChecker.Check(withoutTargets));
        Assert.Contains("Panel id 2: target number 1 has no 'expr' query", GrafanaDashboardChecker.Check(emptyQuery));
    }

    [Fact]
    public void Rows_and_text_panels_need_no_targets()
    {
        var json = Fixtures.ValidDashboard.Replace(
            "\"id\": 1, \"type\": \"row\"",
            "\"id\": 1, \"type\": \"text\"");

        Assert.Empty(GrafanaDashboardChecker.Check(json));
    }

    [Fact]
    public void Panels_inside_a_collapsed_row_are_checked_too()
    {
        var json = Fixtures.ValidDashboard.Replace(
            "{ \"id\": 1, \"type\": \"row\", \"title\": \"Overview\", \"gridPos\": { \"x\": 0, \"y\": 0, \"w\": 24, \"h\": 1 } }",
            "{ \"id\": 1, \"type\": \"row\", \"title\": \"Overview\", \"gridPos\": { \"x\": 0, \"y\": 0, \"w\": 24, \"h\": 1 }, " +
            "\"panels\": [ { \"id\": 2, \"type\": \"stat\", \"title\": \"Inner\", \"gridPos\": { \"x\": 0, \"y\": 1, \"w\": 4, \"h\": 4 }, \"targets\": [ { \"expr\": \"up\" } ] } ] }");

        Assert.Contains("Panel id 2: the id is used by more than one panel", GrafanaDashboardChecker.Check(json));
    }

    [Fact]
    public void A_datasource_variable_that_does_not_exist_is_reported_with_its_location()
    {
        var json = Fixtures.ValidDashboard.Replace("${DS_PROMETHEUS}", "${DS_OTHER}");

        var problems = GrafanaDashboardChecker.Check(json);

        Assert.Contains("dashboard.panels[1].datasource: the datasource variable 'DS_OTHER' is not defined in __inputs or in templating.list", problems);
    }

    [Fact]
    public void A_datasource_variable_defined_in_templating_is_accepted()
    {
        var json = Fixtures.ValidDashboard
            .Replace("\"__inputs\": [ { \"name\": \"DS_PROMETHEUS\", \"type\": \"datasource\", \"pluginId\": \"prometheus\" } ],",
                     "\"templating\": { \"list\": [ { \"name\": \"DS_PROMETHEUS\", \"type\": \"datasource\" } ] },");

        Assert.Empty(GrafanaDashboardChecker.Check(json));
    }

    [Fact]
    public void A_datasource_given_by_name_is_not_a_variable_reference()
    {
        var json = Fixtures.ValidDashboard.Replace("\"uid\": \"${DS_PROMETHEUS}\"", "\"uid\": \"abc123\"");

        Assert.Empty(GrafanaDashboardChecker.Check(json));
    }

    [Theory]
    [InlineData("TODO")]
    [InlineData("FIXME")]
    [InlineData("XXX")]
    [InlineData("{{PLACEHOLDER}}")]
    public void A_placeholder_left_in_the_text_is_reported(string placeholder)
    {
        var json = Fixtures.ValidDashboard.Replace("\"title\": \"Duration\"", $"\"title\": \"Duration {placeholder}\"");

        Assert.Contains(GrafanaDashboardChecker.Check(json), p => p.Contains("contains the placeholder"));
    }

    [Fact]
    public void Grafana_legend_templates_are_not_placeholders()
    {
        // The valid dashboard already uses {{__name__}} as a legend
        Assert.Empty(GrafanaDashboardChecker.Check(Fixtures.ValidDashboard));
    }

    [Fact]
    public void The_list_of_problems_is_limited()
    {
        var panels = string.Join(",", Enumerable.Range(1, 60).Select(i =>
            $$"""{ "id": {{i}}, "type": "stat", "title": "p{{i}}", "gridPos": { "x": 0, "y": {{i}}, "w": 4, "h": 4 } }"""));
        var json = $$"""{ "uid": "a", "title": "t", "schemaVersion": 41, "panels": [ {{panels}} ] }""";

        var problems = GrafanaDashboardChecker.Check(json);

        Assert.Equal(31, problems.Count);
        Assert.EndsWith("more problems", problems[^1]);
    }
}

public class BundledPipelineTests
{
    [Fact]
    public void Every_bundled_pipeline_is_consistent()
    {
        var pipelinesFolder = Path.GetDirectoryName(Fixtures.RepositoryFile("pipelines/grafana-k6/pipeline.json"))!;
        var pipeline = JsonSerializer.Deserialize<PipelineInfo>(File.ReadAllText(Path.Combine(pipelinesFolder, "pipeline.json")))!;

        foreach (var agent in pipeline.Agents)
        {
            Assert.True(AgentTypes.IsValid(agent.Type), $"{agent.Role} has an unknown type");

            if (agent.Type == AgentTypes.Check)
                Assert.True(Checkers.Exists(agent.Checker), $"{agent.Role} has an unknown checker");
            else
                Assert.True(File.Exists(Path.Combine(pipelinesFolder, agent.PromptFile)), $"{agent.PromptFile} is missing");
        }

        Assert.Equal(pipeline.Agents.Select(a => a.Step).Order(), Enumerable.Range(1, pipeline.Agents.Count));
    }
}
