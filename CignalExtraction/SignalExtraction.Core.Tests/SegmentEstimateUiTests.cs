using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SignalExtraction.Core.Tests;

public class SegmentEstimateUiTests
{
    [Fact]
    public async Task RootPage_ReturnsAppShellWithPrimaryEstimateWorkflow()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Segment Estimate</title>", html);
        Assert.Contains("RiverSignals", html);
        Assert.Contains("aria-label=\"Primary workflows\"", html);
        Assert.Contains("href=\"/\">Segment Estimate</a>", html);
        Assert.Contains("href=\"/compare-runs\">Compare Runs</a>", html);
        Assert.Contains("href=\"/completed-run\">Report Completed Run</a>", html);
        Assert.Contains("Account placeholder", html);
        Assert.Contains("id=\"estimateWorkflowHeading\">Estimate</h2>", html);
        Assert.Contains("id=\"riverSelect\"", html);
        Assert.Contains("id=\"segmentSelect\"", html);
        Assert.Contains("id=\"paddlingSpeedInput\"", html);
        Assert.Contains("id=\"launchDateInput\"", html);
        Assert.Contains("id=\"launchClockInput\"", html);
        Assert.Contains("id=\"launchTimeInput\"", html);
        Assert.Contains("type=\"hidden\"", html);
        Assert.Contains("Estimate Run Time", html);
        Assert.Contains("data-reserved-slot=\"dashboard\"", html);
        Assert.Contains("data-reserved-slot=\"sponsored\"", html);
        Assert.Contains("Haw", html);
        Assert.Contains("Cape Fear", html);
        Assert.Contains("Neuse", html);
        Assert.Contains("Tar River", html);
        Assert.Contains("Put-in address", html);
        Assert.Contains("Take-out address", html);
        Assert.Contains("Access notes", html);
        Assert.Contains("Context source", html);
        Assert.DoesNotContain("id=\"report\"", html);
        Assert.DoesNotContain("id=\"observationStartInput\"", html);
        Assert.DoesNotContain("Submit Observation", html);
        AssertPublicPageBoundary(html);
    }

    [Fact]
    public async Task RootPage_UsesExistingEstimateEndpoint_AndStartControlsFeedLaunchTime()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("const segmentData =", html);
        Assert.Contains("/api/segments/${segmentId}/estimate", html);
        Assert.Contains("/api/segments/${segmentId}", html);
        Assert.Contains("populateSegmentSelect()", html);
        Assert.Contains("getSegmentsForSelectedRiver()", html);
        Assert.Contains("setTomorrowMorningLaunchTime()", html);
        Assert.Contains("getTomorrowMorningWindow()", html);
        Assert.Contains("isWithinTomorrowMorningWindow(date)", html);
        Assert.Contains("syncLaunchTimeInput()", html);
        Assert.Contains("launchTimeInput.value = launchDateInput.value && launchClockInput.value", html);
        Assert.Contains("launchDateInput.addEventListener('change', syncLaunchTimeInput);", html);
        Assert.Contains("launchClockInput.addEventListener('change', syncLaunchTimeInput);", html);
        Assert.Contains("tomorrowMorning.setHours(9, 0, 0, 0);", html);
        Assert.Contains("runEstimate();", html);
        Assert.Contains("Estimate Input", html);
        Assert.Contains("return 'Planned Time';", html);
        Assert.Contains("return 'Latest Conditions';", html);
        Assert.Contains("return 'Baseline';", html);
        Assert.Contains("estimateButton.addEventListener('click', runEstimate);", html);
        Assert.Contains("Estimate explanation will appear here.", html);
        Assert.Contains("Select a river and segment to see available signal.", html);
        Assert.Contains("does not rank quality or freshness", html);
    }

    [Fact]
    public async Task CompareRunsPage_ReturnsDedicatedComparisonWorkflow()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/compare-runs");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Compare Runs</title>", html);
        Assert.Contains("href=\"/\">Segment Estimate</a>", html);
        Assert.Contains("class=\"active\" href=\"/compare-runs\">Compare Runs</a>", html);
        Assert.Contains("href=\"/completed-run\">Report Completed Run</a>", html);
        Assert.Contains("Account placeholder", html);
        Assert.Contains("id=\"comparisonOptions\"", html);
        Assert.Contains("id=\"compareSelectedRunsButton\"", html);
        Assert.Contains("Compare Selected Runs", html);
        Assert.Contains("compareSelectedRuns()", html);
        Assert.Contains("/api/segments/${segmentId}/estimate", html);
        Assert.Contains("Neuse River - Falls Dam River Access to Thornton Road River Access", html);
        Assert.DoesNotContain("id=\"riverSelect\"", html);
        Assert.DoesNotContain("id=\"completedRunStartInput\"", html);
        AssertPublicPageBoundary(html);
    }

    [Fact]
    public async Task RootPage_DoesNotRedirectAwayFromUiSlice()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompletedRunPage_ReturnsDedicatedPostRunSubmissionFlow()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/completed-run");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Completed Run Submission</title>", html);
        Assert.Contains("href=\"/\">Segment Estimate</a>", html);
        Assert.Contains("href=\"/compare-runs\">Compare Runs</a>", html);
        Assert.Contains("class=\"active\" href=\"/completed-run\">Report Completed Run</a>", html);
        Assert.Contains("Account placeholder", html);
        Assert.Contains("Report a Completed Run", html);
        Assert.Contains("Choose the access-to-access run you finished and send the trip details.", html);
        Assert.Contains("id=\"riverSelect\"", html);
        Assert.Contains("id=\"segmentSelect\"", html);
        Assert.Contains("id=\"completedRunDateInput\"", html);
        Assert.Contains("id=\"completedRunStartClockInput\"", html);
        Assert.Contains("id=\"completedRunStartInput\"", html);
        Assert.Contains("type=\"hidden\"", html);
        Assert.Contains("id=\"completedRunDurationInput\"", html);
        Assert.Contains("id=\"completedRunPutInInput\"", html);
        Assert.Contains("id=\"completedRunTakeOutInput\"", html);
        Assert.Contains("id=\"completedRunNotesInput\"", html);
        Assert.Contains("id=\"submitCompletedRunButton\"", html);
        Assert.Contains("syncCompletedRunStartInput()", html);
        Assert.Contains("completedRunDateInput.addEventListener('change', syncCompletedRunStartInput);", html);
        Assert.Contains("completedRunStartClockInput.addEventListener('change', syncCompletedRunStartInput);", html);
        Assert.Contains("Completed runs are saved for RiverSignals.", html);
        Assert.Contains("They do not change estimates automatically.", html);
        Assert.Contains("Completed run saved", html);
        Assert.Contains("Submitted values do not change estimates automatically.", html);
        Assert.Contains("/api/segments/${segmentId}/observations", html);
        Assert.Contains("submitCompletedRunButton.addEventListener('click', submitCompletedRun);", html);
        Assert.DoesNotContain("Finish Time", html);
        AssertPublicPageBoundary(html);
    }

    [Fact]
    public async Task CompletedRunPage_StaysRunCentered_WithoutEstimateOrCommunitySurface()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/completed-run");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"name\":\"Neuse River - Falls Dam River Access to Thornton Road River Access\"", html);
        Assert.Contains("\"name\":\"Neuse River - River Bend Park Kayak Launch to Buffaloe Road River Access\"", html);
        Assert.DoesNotContain("Neuse River - Falls Dam River Access to Buffaloe Road River Access", html);
        Assert.DoesNotContain("/api/segments/${segmentId}/estimate", html);
        Assert.DoesNotContain("Condition Basis", html);
        Assert.DoesNotContain("Estimate Input", html);
        Assert.DoesNotContain("validated", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trusted", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ground truth", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("improve estimates", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("community", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publish", html, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPublicPageBoundary(string html)
    {
        Assert.DoesNotContain("href=\"/internal", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/api/internal", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-steward", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-access-candidates", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schema", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("River miles", html);
        Assert.DoesNotContain("riverMile", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reconciliation", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unreviewed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provisional", html, StringComparison.OrdinalIgnoreCase);
    }
}
