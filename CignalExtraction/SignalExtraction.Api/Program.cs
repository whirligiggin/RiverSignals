using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddScoped<IExtractionService, ExtractionService>();
builder.Services.AddScoped<ExtractionPlanningHandoffEvaluator>();
builder.Services.AddSingleton<ISegmentCatalogService, SegmentCatalogService>();
builder.Services.AddSingleton<IRawAccessPointCandidateReviewStore>(_ =>
{
    var sharedConnectionString = builder.Configuration.GetConnectionString("RiverSignals");
    var connectionString = builder.Configuration.GetConnectionString("RawAccessReviews")
        ?? sharedConnectionString
        ?? builder.Configuration.GetConnectionString("FlowReadings")
        ?? "Data Source=../data/riversignals.db";

    return new SqliteRawAccessPointCandidateReviewStore(connectionString);
});
builder.Services.AddSingleton<IRawAccessPointCandidateService, RawAccessPointCandidateService>();
builder.Services.AddSingleton<IAccessPointIdentifierService, AccessPointIdentifierService>();
builder.Services.AddSingleton<IProvisionalAccessChoiceSignalService, ProvisionalAccessChoiceSignalService>();
builder.Services.AddSingleton<IProvisionalAccessPairService, ProvisionalAccessPairService>();
builder.Services.AddHttpClient<IUsgsInstantaneousValuesClient, UsgsInstantaneousValuesClient>(client =>
{
    client.BaseAddress = new Uri("https://waterservices.usgs.gov/");
});
builder.Services.AddSingleton<IFlowReadingService>(_ =>
{
    var sharedConnectionString = builder.Configuration.GetConnectionString("RiverSignals");
    var connectionString = builder.Configuration.GetConnectionString("FlowReadings")
        ?? sharedConnectionString
        ?? "Data Source=../data/riversignals.db";

    return new SqliteFlowReadingService(connectionString);
});
builder.Services.AddSingleton<ITripObservationService>(_ =>
{
    var sharedConnectionString = builder.Configuration.GetConnectionString("RiverSignals");
    var connectionString = builder.Configuration.GetConnectionString("FlowReadings")
        ?? sharedConnectionString
        ?? "Data Source=../data/riversignals.db";

    return new SqliteTripObservationService(connectionString);
});
builder.Services.AddSingleton<ICanonicalWorkbenchService>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("RiverSignals")
        ?? builder.Configuration.GetConnectionString("FlowReadings")
        ?? "Data Source=../data/riversignals.db";

    return new SqliteCanonicalWorkbenchService(connectionString);
});
builder.Services.AddSingleton<IWideRunRequestService>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("RiverSignals")
        ?? builder.Configuration.GetConnectionString("FlowReadings")
        ?? "Data Source=../data/riversignals.db";

    return new SqliteWideRunRequestService(connectionString);
});
builder.Services.AddScoped<IBestAvailableRunEstimateService, BestAvailableRunEstimateService>();
builder.Services.AddScoped<ITripEstimationService, TripEstimationService>();
builder.Services.AddScoped<IUsgsFlowImportService, UsgsFlowImportService>();
builder.Services.AddScoped<INearTermFlowOutlookService, NearTermFlowOutlookService>();

var app = builder.Build();

var canonicalConnectionString = builder.Configuration.GetConnectionString("RiverSignals")
    ?? builder.Configuration.GetConnectionString("FlowReadings")
    ?? "Data Source=../data/riversignals.db";
new SqliteCanonicalSchemaInitializer(canonicalConnectionString).EnsureCreated();
new SqliteSeededCanonicalCatalogSeeder(
    canonicalConnectionString,
    app.Services.GetRequiredService<ISegmentCatalogService>())
    .SeedMissing();

app.MapGet("/", (ISegmentCatalogService segmentCatalogService) =>
{
    var segments = segmentCatalogService
        .GetPresetSegments()
        .Where(segment => segment.IsActive)
        .OrderBy(segment => segment.Name)
        .ToList();
    var riverNames = segmentCatalogService
        .GetPresetRivers()
        .ToDictionary(river => river.Id, river => river.Name);

    return Results.Content(BuildSegmentEstimatePage(segments, riverNames), "text/html");
});

app.MapGet("/completed-run", (ISegmentCatalogService segmentCatalogService) =>
{
    var segments = segmentCatalogService
        .GetPresetSegments()
        .Where(segment => segment.IsActive)
        .OrderBy(segment => segment.Name)
        .ToList();
    var riverNames = segmentCatalogService
        .GetPresetRivers()
        .ToDictionary(river => river.Id, river => river.Name);

    return Results.Content(BuildCompletedRunSubmissionPage(segments, riverNames), "text/html");
});

app.MapGet("/compare-runs", (ISegmentCatalogService segmentCatalogService) =>
{
    var segments = segmentCatalogService
        .GetPresetSegments()
        .Where(segment => segment.IsActive)
        .OrderBy(segment => segment.Name)
        .ToList();
    var riverNames = segmentCatalogService
        .GetPresetRivers()
        .ToDictionary(river => river.Id, river => river.Name);

    return Results.Content(BuildCompareRunsPage(segments, riverNames), "text/html");
});

app.MapGet("/internal/raw-access-candidates", (IRawAccessPointCandidateService rawAccessPointCandidateService) =>
{
    return Results.Content(
        BuildRawAccessCandidateReviewPage(rawAccessPointCandidateService.GetRawAccessPointCandidates()),
        "text/html");
});

app.MapDataStewardWorkbenchEndpoints();
app.MapWideRunRequestEndpoints();

app.MapControllers();

app.Run();

static string BuildSegmentEstimatePage(List<Segment> segments, Dictionary<Guid, string> riverNames)
{
    var riverOptions = segments
        .Select(segment => new RiverOption(segment.RiverId, GetRiverName(segment, riverNames)))
        .DistinctBy(river => river.RiverId)
        .OrderBy(river => river.RiverName)
        .ToList();

    var riverOptionMarkup = string.Join(Environment.NewLine, riverOptions.Select(river =>
        $$"""
                  <option value="{{river.RiverId}}">
                    {{WebUtility.HtmlEncode(river.RiverName)}}
                  </option>
        """));

    var segmentData = BuildSegmentData(segments, riverNames);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Segment Estimate</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f6f8;
      --surface: #ffffff;
      --surface-muted: #eef2f5;
      --border: #d6dde6;
      --text: #17212b;
      --text-muted: #586574;
      --accent: #1f6fb2;
      --accent-soft: #dbeafe;
      --success: #166534;
      --warning: #9a3412;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: "Segoe UI", Arial, sans-serif;
      background: linear-gradient(180deg, #edf3f8 0%, #f7f9fb 100%);
      color: var(--text);
    }

    main {
      max-width: 1280px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .app-shell {
      display: grid;
      grid-template-columns: 220px minmax(0, 1fr);
      gap: 24px;
      align-items: start;
    }

    .app-nav {
      position: sticky;
      top: 20px;
      display: grid;
      gap: 12px;
      padding: 18px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
    }

    .brand {
      font-size: 18px;
      font-weight: 800;
      color: var(--text);
      margin-bottom: 4px;
    }

    .app-nav a {
      display: block;
      padding: 10px 12px;
      border-radius: 6px;
      color: var(--text);
      text-decoration: none;
      font-weight: 700;
    }

    .app-nav a:hover,
    .app-nav a.active {
      background: var(--accent-soft);
      color: #174a77;
    }

    .app-content {
      min-width: 0;
    }

    .top-header {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      align-items: center;
      margin-bottom: 20px;
      padding: 16px 18px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
    }

    .top-header strong {
      display: block;
      font-size: 16px;
    }

    .account-placeholder {
      color: var(--text-muted);
      font-size: 13px;
      white-space: nowrap;
    }

    .shell {
      display: grid;
      grid-template-columns: minmax(300px, 360px) minmax(0, 1fr);
      gap: 20px;
      align-items: start;
    }

    .panel {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
      padding: 20px;
    }

    h1, h2, h3, p { margin: 0; }

    .intro {
      margin-bottom: 20px;
    }

    .intro h1 {
      font-size: 30px;
      line-height: 1.1;
      margin-bottom: 8px;
    }

    .intro p {
      color: var(--text-muted);
      max-width: 720px;
      line-height: 1.5;
    }

    .intro a {
      color: var(--accent);
      font-weight: 600;
    }

    .launch-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      margin-top: 14px;
    }

    .launch-actions a {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 40px;
      padding: 0 14px;
      border-radius: 6px;
      border: 1px solid var(--accent);
      color: var(--accent);
      text-decoration: none;
      font-weight: 700;
    }

    .launch-actions a.primary-link {
      background: var(--accent);
      color: #ffffff;
    }

    .layout {
      display: grid;
      gap: 20px;
    }

    .content-with-rail {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 220px;
      gap: 20px;
      align-items: start;
    }

    .reserved-rail {
      display: grid;
      gap: 14px;
    }

    .reserved-slot {
      min-height: 148px;
      border: 1px dashed #c2ccd8;
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.62);
    }

    .workflow-section {
      scroll-margin-top: 20px;
    }

    .workflow-heading {
      margin-bottom: 14px;
    }

    .workflow-heading h2 {
      font-size: 22px;
      line-height: 1.2;
    }

    .stack {
      display: grid;
      gap: 16px;
    }

    .detail-note {
      margin-top: -8px;
      font-size: 13px;
      color: var(--text-muted);
      line-height: 1.5;
    }

    .inline-actions {
      display: flex;
      gap: 10px;
      align-items: end;
      flex-wrap: wrap;
    }

    .inline-actions label {
      flex: 1 1 220px;
    }

    .secondary-button {
      width: auto;
      min-width: 220px;
      padding: 0 16px;
      background: #ffffff;
      color: var(--accent);
      border: 1px solid var(--accent);
    }

    .secondary-button:hover {
      background: #eef6fc;
    }

    label {
      display: grid;
      gap: 8px;
      font-size: 14px;
      color: var(--text-muted);
    }

    select, input, button {
      width: 100%;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
    }

    select, input {
      height: 44px;
      padding: 0 12px;
      color: var(--text);
      background: var(--surface);
    }

    button {
      height: 46px;
      background: var(--accent);
      color: white;
      font-weight: 600;
      border: none;
      cursor: pointer;
    }

    button:hover { background: #175f98; }
    button:disabled {
      background: #9db8cf;
      cursor: default;
    }

    .segment-meta {
      display: grid;
      gap: 10px;
      background: var(--surface-muted);
      border-radius: 8px;
      padding: 14px;
    }

    .meta-row {
      display: grid;
      grid-template-columns: 120px minmax(0, 1fr);
      gap: 12px;
      font-size: 14px;
    }

    .meta-label {
      color: var(--text-muted);
    }

    .result-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
      margin-top: 18px;
    }

    .metric {
      background: var(--surface-muted);
      border-radius: 8px;
      padding: 14px;
      min-height: 86px;
    }

    .metric h3 {
      font-size: 13px;
      color: var(--text-muted);
      margin-bottom: 8px;
      font-weight: 600;
    }

    .metric .value {
      font-size: 22px;
      font-weight: 700;
      color: var(--text);
    }

    .metric .detail {
      margin-top: 6px;
      font-size: 13px;
      color: var(--text-muted);
      line-height: 1.4;
    }

    .summary {
      margin-top: 12px;
      padding: 14px;
      border-radius: 8px;
      background: var(--accent-soft);
      color: #174a77;
      font-size: 14px;
      line-height: 1.5;
    }

    textarea {
      width: 100%;
      min-height: 96px;
      padding: 12px;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
      color: var(--text);
      background: var(--surface);
      resize: vertical;
    }

    .evidence-panel {
      margin-top: 18px;
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 16px;
      background: #f8fbfd;
    }

    .evidence-header {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      align-items: center;
      margin-bottom: 12px;
    }

    .evidence-header h3 {
      font-size: 15px;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      min-height: 28px;
      padding: 0 10px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 700;
      background: #dbeafe;
      color: #174a77;
    }

    .badge.muted {
      background: #e5e7eb;
      color: #4b5563;
    }

    .evidence-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }

    .evidence-item {
      background: var(--surface);
      border-radius: 8px;
      padding: 12px;
      border: 1px solid #e3e9ef;
      min-height: 78px;
    }

    .evidence-item h4 {
      font-size: 12px;
      color: var(--text-muted);
      margin-bottom: 8px;
      font-weight: 600;
    }

    .evidence-item .value {
      font-size: 18px;
      font-weight: 700;
    }

    .evidence-item .detail {
      margin-top: 6px;
      font-size: 13px;
      color: var(--text-muted);
      line-height: 1.4;
    }

    .evidence-note {
      margin-top: 12px;
      font-size: 13px;
      color: var(--text-muted);
      line-height: 1.5;
    }

    .comparison-options {
      display: grid;
      gap: 10px;
      margin-top: 16px;
    }

    .compare-row {
      display: grid;
      grid-template-columns: 24px minmax(0, 1fr) auto;
      gap: 10px;
      align-items: center;
      padding: 10px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
    }

    .compare-row input {
      width: 18px;
      height: 18px;
      padding: 0;
    }

    .compare-row strong {
      display: block;
      font-size: 14px;
    }

    .compare-row span {
      display: block;
      color: var(--text-muted);
      font-size: 13px;
      margin-top: 4px;
    }

    .compare-distance {
      color: var(--text-muted);
      font-size: 13px;
      white-space: nowrap;
    }

    .comparison-table-wrap {
      overflow-x: auto;
      margin-top: 16px;
      border: 1px solid var(--border);
      border-radius: 8px;
    }

    .comparison-table {
      width: 100%;
      min-width: 760px;
      border-collapse: collapse;
      background: var(--surface);
    }

    .comparison-table th,
    .comparison-table td {
      padding: 10px;
      border-bottom: 1px solid var(--border);
      text-align: left;
      vertical-align: top;
      font-size: 13px;
    }

    .comparison-table th {
      background: var(--surface-muted);
      color: var(--text-muted);
      font-weight: 700;
    }

    .comparison-empty {
      margin-top: 12px;
      color: var(--text-muted);
      font-size: 14px;
    }

    .status {
      margin-top: 14px;
      min-height: 22px;
      font-size: 14px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 900px) {
      .app-shell {
        grid-template-columns: 1fr;
      }

      .app-nav {
        position: static;
      }

      .shell {
        grid-template-columns: 1fr;
      }

      .top-header {
        align-items: flex-start;
        flex-direction: column;
      }

      .content-with-rail {
        grid-template-columns: 1fr;
      }

      .result-grid {
        grid-template-columns: 1fr;
      }

      .evidence-grid {
        grid-template-columns: 1fr;
      }

      .compare-row {
        grid-template-columns: 24px minmax(0, 1fr);
      }

      .compare-distance {
        grid-column: 2;
      }

      .meta-row {
        grid-template-columns: 1fr;
        gap: 4px;
      }
    }
  </style>
</head>
<body>
  <main class="app-shell">
    <nav class="app-nav" aria-label="Primary workflows">
      <div class="brand">RiverSignals</div>
      <a class="active" href="/">Segment Estimate</a>
      <a href="/compare-runs">Compare Runs</a>
      <a href="/completed-run">Report Completed Run</a>
      <a href="/request-run">Request Any Run</a>
    </nav>

    <div class="app-content">
      <header class="top-header">
        <strong>RiverSignals</strong>
        <div class="account-placeholder">Account placeholder</div>
      </header>

      <section id="estimate" class="intro workflow-section">
        <h1>Estimate a Paddle</h1>
        <p>Choose a known run for an immediate trip-time estimate.</p>
      </section>

      <div class="content-with-rail">
      <div class="layout">
        <section class="workflow-section" aria-labelledby="estimateWorkflowHeading">
          <div class="workflow-heading">
            <h2 id="estimateWorkflowHeading">Estimate</h2>
          </div>
          <div class="shell">
      <section class="panel">
        <div class="stack">
          <label for="riverSelect">
            Which River?
            <select id="riverSelect" name="riverId">
{{riverOptionMarkup}}
            </select>
          </label>

          <label for="segmentSelect">
            Segment
            <select id="segmentSelect" name="segmentId">
            </select>
          </label>

          <div class="segment-meta" aria-live="polite">
            <div class="meta-row">
              <div class="meta-label">Put-in</div>
              <div id="putInName"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Take-out</div>
              <div id="takeOutName"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Distance</div>
              <div id="distanceMiles"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Put-in address</div>
              <div id="putInAddress"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Take-out address</div>
              <div id="takeOutAddress"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Access notes</div>
              <div id="accessNotes"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Default current</div>
              <div id="defaultCurrent"></div>
            </div>
            <div class="meta-row">
              <div class="meta-label">Context source</div>
              <div id="planningSource"></div>
            </div>
          </div>

          <label for="paddlingSpeedInput">
            Paddling Speed (mph)
            <input id="paddlingSpeedInput" name="paddlingSpeedMph" type="number" min="0.1" step="0.1" placeholder="Defaults to 3.0" />
          </label>

          <div class="inline-actions">
            <label for="launchDateInput">
              Launch Date
              <input id="launchDateInput" name="launchDate" type="date" />
            </label>
            <label for="launchClockInput">
              Start Time
              <input id="launchClockInput" name="launchClockTime" type="time" />
            </label>
            <input id="launchTimeInput" name="launchTimeLocal" type="hidden" />
            <button id="tomorrowMorningButton" class="secondary-button" type="button">Tomorrow Morning (9:00 AM)</button>
          </div>
          <div id="launchTimeWindowNote" class="detail-note">Choose a launch time tomorrow between 5:00 AM and 11:59 AM local for bounded near-term outlook support. Outside that window, the estimate falls back to the latest available or baseline conditions.</div>

          <button id="estimateButton" type="button">Estimate Run Time</button>
          <div id="statusMessage" class="status" aria-live="polite"></div>
        </div>
      </section>

      <section class="panel">
        <h2 id="resultSegmentName">Select a segment to begin</h2>
        <div class="result-grid">
          <article class="metric">
            <h3>Estimated Duration</h3>
            <div id="durationValue" class="value">--</div>
            <div id="durationDetail" class="detail">Duration appears here after estimation.</div>
          </article>
          <article class="metric">
            <h3>Effective Speed</h3>
            <div id="effectiveSpeedValue" class="value">--</div>
            <div id="currentSourceDetail" class="detail">Current source appears here.</div>
          </article>
          <article class="metric">
            <h3>Current Used</h3>
            <div id="currentValue" class="value">--</div>
            <div id="currentDetail" class="detail">Flow or fallback current appears here.</div>
          </article>
          <article class="metric">
            <h3>Estimate Input</h3>
            <div id="conditionBasisValue" class="value">--</div>
            <div id="conditionBasisDetail" class="detail">Estimate input state appears here.</div>
          </article>
          <article class="metric">
            <h3>Finish Time</h3>
            <div id="finishValue" class="value">--</div>
            <div id="finishDetail" class="detail">Shown when a launch time is provided.</div>
          </article>
        </div>
        <div id="summaryBox" class="summary">Estimate explanation will appear here.</div>
        <section class="evidence-panel" aria-live="polite">
          <div class="evidence-header">
            <h3>Observation Signal</h3>
            <div id="observationBadge" class="badge muted">Checking segment signal...</div>
          </div>
          <div class="evidence-grid">
            <article class="evidence-item">
              <h4>Latest observation</h4>
              <div id="observationObservedAt" class="value">--</div>
              <div id="observationObservedDetail" class="detail">Load a segment to see whether a flow reading exists.</div>
            </article>
            <article class="evidence-item">
              <h4>Signal source</h4>
              <div id="observationSource" class="value">--</div>
              <div id="observationSourceDetail" class="detail">The system will show the backing source when signal exists.</div>
            </article>
            <article class="evidence-item">
              <h4>Estimated current</h4>
              <div id="observationCurrent" class="value">--</div>
              <div id="observationCurrentDetail" class="detail">Falls back to seeded current when no live reading is present.</div>
            </article>
            <article class="evidence-item">
              <h4>Flow rate</h4>
              <div id="observationFlowRate" class="value">--</div>
              <div id="observationFlowRateDetail" class="detail">Shown only when a latest reading includes flow rate.</div>
            </article>
          </div>
          <div id="observationNote" class="evidence-note">Observation visibility is factual only. It does not rank quality or freshness.</div>
        </section>
      </section>
      </div>
        </section>
      </div>
      <aside class="reserved-rail" aria-label="Reserved layout space">
        <div class="reserved-slot" data-reserved-slot="dashboard"></div>
        <div class="reserved-slot" data-reserved-slot="sponsored"></div>
      </aside>
      </div>
    </div>
  </main>

  <script>
    const segmentData = {{segmentData}};
    const riverSelect = document.getElementById('riverSelect');
    const segmentSelect = document.getElementById('segmentSelect');
    const paddlingSpeedInput = document.getElementById('paddlingSpeedInput');
    const launchDateInput = document.getElementById('launchDateInput');
    const launchClockInput = document.getElementById('launchClockInput');
    const launchTimeInput = document.getElementById('launchTimeInput');
    const tomorrowMorningButton = document.getElementById('tomorrowMorningButton');
    const estimateButton = document.getElementById('estimateButton');
    const statusMessage = document.getElementById('statusMessage');
    const putInName = document.getElementById('putInName');
    const takeOutName = document.getElementById('takeOutName');
    const distanceMiles = document.getElementById('distanceMiles');
    const putInAddress = document.getElementById('putInAddress');
    const takeOutAddress = document.getElementById('takeOutAddress');
    const accessNotes = document.getElementById('accessNotes');
    const defaultCurrent = document.getElementById('defaultCurrent');
    const planningSource = document.getElementById('planningSource');
    const resultSegmentName = document.getElementById('resultSegmentName');
    const durationValue = document.getElementById('durationValue');
    const durationDetail = document.getElementById('durationDetail');
    const effectiveSpeedValue = document.getElementById('effectiveSpeedValue');
    const currentSourceDetail = document.getElementById('currentSourceDetail');
    const currentValue = document.getElementById('currentValue');
    const currentDetail = document.getElementById('currentDetail');
    const conditionBasisValue = document.getElementById('conditionBasisValue');
    const conditionBasisDetail = document.getElementById('conditionBasisDetail');
    const finishValue = document.getElementById('finishValue');
    const finishDetail = document.getElementById('finishDetail');
    const summaryBox = document.getElementById('summaryBox');
    const observationBadge = document.getElementById('observationBadge');
    const observationObservedAt = document.getElementById('observationObservedAt');
    const observationObservedDetail = document.getElementById('observationObservedDetail');
    const observationSource = document.getElementById('observationSource');
    const observationSourceDetail = document.getElementById('observationSourceDetail');
    const observationCurrent = document.getElementById('observationCurrent');
    const observationCurrentDetail = document.getElementById('observationCurrentDetail');
    const observationFlowRate = document.getElementById('observationFlowRate');
    const observationFlowRateDetail = document.getElementById('observationFlowRateDetail');
    const observationNote = document.getElementById('observationNote');
    function getSegmentsForSelectedRiver() {
      return segmentData.filter(segment => segment.riverId === riverSelect.value);
    }

    function populateSegmentSelect() {
      const riverSegments = getSegmentsForSelectedRiver();
      segmentSelect.innerHTML = riverSegments.map(segment => {
        const distance = Number(segment.distanceMiles).toFixed(segment.distanceMiles % 1 === 0 ? 0 : 1);
        const current = segment.defaultCurrentMph == null
          ? ''
          : Number(segment.defaultCurrentMph).toFixed(segment.defaultCurrentMph % 1 === 0 ? 0 : 1);

        return `<option value="${segment.id}" data-name="${segment.name}" data-putin="${segment.putInName}" data-takeout="${segment.takeOutName}" data-distance="${distance}" data-distance-source="${segment.distanceSource || ''}" data-putin-address="${segment.putInAddress || ''}" data-takeout-address="${segment.takeOutAddress || ''}" data-putin-amenities="${segment.putInAmenities || ''}" data-takeout-amenities="${segment.takeOutAmenities || ''}" data-planning-source="${segment.planningSource || ''}" data-current="${current}">${segment.name}</option>`;
      }).join('');

      segmentSelect.disabled = riverSegments.length === 0;
      estimateButton.disabled = riverSegments.length === 0;
    }

    function getSelectedOption() {
      return segmentSelect.options[segmentSelect.selectedIndex];
    }

    function updateSegmentDetails() {
      const selected = getSelectedOption();
      if (!selected) {
        putInName.textContent = '--';
        takeOutName.textContent = '--';
        distanceMiles.textContent = '--';
        putInAddress.textContent = '--';
        takeOutAddress.textContent = '--';
        accessNotes.textContent = '--';
        defaultCurrent.textContent = '--';
        planningSource.textContent = '--';
        resultSegmentName.textContent = 'Select a segment to begin';
        return;
      }

      putInName.textContent = selected.dataset.putin || '--';
      takeOutName.textContent = selected.dataset.takeout || '--';
      distanceMiles.textContent = selected.dataset.distance
        ? `${selected.dataset.distance} miles${selected.dataset.distanceSource ? ' from access data' : ''}`
        : '--';
      putInAddress.textContent = selected.dataset.putinAddress || '--';
      takeOutAddress.textContent = selected.dataset.takeoutAddress || '--';
      accessNotes.textContent = [selected.dataset.putinAmenities, selected.dataset.takeoutAmenities]
        .filter(Boolean)
        .join(' | ') || '--';
      defaultCurrent.textContent = selected.dataset.current ? `${selected.dataset.current} mph` : 'None';
      planningSource.textContent = selected.dataset.planningSource || '--';
      resultSegmentName.textContent = selected.dataset.name || 'Segment Estimate';
    }

    function formatDuration(durationText) {
      if (!durationText) return '--';
      const match = /^([0-9]+):([0-9]{2}):([0-9]{2}(?:\\.[0-9]+)?)$/.exec(durationText);
      if (!match) return durationText;
      const hours = Number(match[1]);
      const minutes = Number(match[2]);
      if (hours === 0) return `${minutes} min`;
      return `${hours} hr ${minutes} min`;
    }

    function formatDate(dateText) {
      if (!dateText) return '--';
      const date = new Date(dateText);
      if (Number.isNaN(date.getTime())) return '--';
      return date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
    }

    function toDateTimeLocalValue(date) {
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    function getTomorrowMorningWindow() {
      const start = new Date();
      start.setDate(start.getDate() + 1);
      start.setHours(5, 0, 0, 0);

      const end = new Date(start);
      end.setHours(11, 59, 0, 0);

      return { start, end };
    }

    function isWithinTomorrowMorningWindow(date) {
      const { start, end } = getTomorrowMorningWindow();
      return date >= start && date <= end;
    }

    function applySupportedNearTermWindow() {
      const { start, end } = getTomorrowMorningWindow();
      launchTimeInput.min = toDateTimeLocalValue(start);
      launchTimeInput.max = toDateTimeLocalValue(end);
      launchDateInput.min = toDateInputValue(start);
      launchDateInput.max = toDateInputValue(end);
    }

    function setTomorrowMorningLaunchTime() {
      const { start } = getTomorrowMorningWindow();
      const tomorrowMorning = new Date(start);
      tomorrowMorning.setHours(9, 0, 0, 0);
      launchTimeInput.value = toDateTimeLocalValue(tomorrowMorning);
      launchDateInput.value = toDateInputValue(tomorrowMorning);
      launchClockInput.value = toTimeInputValue(tomorrowMorning);
      statusMessage.textContent = 'Tomorrow morning uses a 9:00 AM local launch time by default.';
      statusMessage.className = 'status';
    }

    function toDateInputValue(date) {
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    }

    function toTimeInputValue(date) {
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${hours}:${minutes}`;
    }

    function syncLaunchTimeInput() {
      launchTimeInput.value = launchDateInput.value && launchClockInput.value
        ? `${launchDateInput.value}T${launchClockInput.value}`
        : '';
    }

    function clearObservationSignal(noteText) {
      observationBadge.textContent = 'No live observation';
      observationBadge.className = 'badge muted';
      observationObservedAt.textContent = '--';
      observationObservedDetail.textContent = 'No latest flow reading is stored for this segment.';
      observationSource.textContent = 'Seeded only';
      observationSourceDetail.textContent = 'Estimates rely on the segment catalog when no live observation exists.';
      observationCurrent.textContent = defaultCurrent.textContent || '--';
      observationCurrentDetail.textContent = 'Current shown here is the seeded segment default.';
      observationFlowRate.textContent = '--';
      observationFlowRateDetail.textContent = 'No observed flow rate is available.';
      observationNote.textContent = noteText || 'Observation visibility is factual only. It does not rank quality or freshness.';
    }

    function renderObservationSignal(latestFlow) {
      if (!latestFlow) {
        clearObservationSignal('No live segment observation is currently available. The estimate uses seeded segment defaults instead.');
        return;
      }

      observationBadge.textContent = 'Live observation available';
      observationBadge.className = 'badge';
      observationObservedAt.textContent = formatDate(latestFlow.observedAtUtc);
      observationObservedDetail.textContent = 'Latest recorded flow observation for this segment.';
      observationSource.textContent = latestFlow.source || '--';
      observationSourceDetail.textContent = 'Source reported by the latest stored flow reading.';
      observationCurrent.textContent = latestFlow.estimatedCurrentMph != null
        ? `${Number(latestFlow.estimatedCurrentMph).toFixed(1)} mph`
        : '--';
      observationCurrentDetail.textContent = latestFlow.estimatedCurrentMph != null
        ? 'Estimated current from the latest flow reading.'
        : 'Latest observation did not include an estimated current.';
      observationFlowRate.textContent = latestFlow.flowRateCfs != null
        ? `${Number(latestFlow.flowRateCfs).toFixed(0)} cfs`
        : '--';
      observationFlowRateDetail.textContent = latestFlow.flowRateCfs != null
        ? 'Observed flow rate from the latest flow reading.'
        : 'Latest observation did not include a flow rate.';
      observationNote.textContent = 'Observation visibility is factual only. It does not rank quality or freshness.';
    }

    async function loadObservationSignal() {
      const segmentId = segmentSelect.value;
      if (!segmentId) {
        clearObservationSignal('Select a river and segment to see available signal.');
        return;
      }

      try {
        const response = await fetch(`/api/segments/${segmentId}`);
        if (!response.ok) {
          throw new Error('Failed to load segment signal.');
        }

        const planning = await response.json();
        renderObservationSignal(planning.latestFlow);
      } catch (error) {
        observationBadge.textContent = 'Signal unavailable';
        observationBadge.className = 'badge muted';
        observationObservedAt.textContent = '--';
        observationObservedDetail.textContent = 'Segment signal could not be loaded right now.';
        observationSource.textContent = '--';
        observationSourceDetail.textContent = 'Try again after refreshing the page.';
        observationCurrent.textContent = '--';
        observationCurrentDetail.textContent = 'No signal details are available because the segment lookup failed.';
        observationFlowRate.textContent = '--';
        observationFlowRateDetail.textContent = 'No signal details are available because the segment lookup failed.';
        observationNote.textContent = error.message || 'Segment signal could not be loaded right now.';
      }
    }

    async function runEstimate() {
      const segmentId = segmentSelect.value;
      if (!segmentId) {
        statusMessage.textContent = 'Select a river and segment first.';
        statusMessage.className = 'status error';
        return;
      }

      const request = {};
      const selectedLaunchTime = launchTimeInput.value
        ? new Date(launchTimeInput.value)
        : null;
      const hasValidLaunchTime = selectedLaunchTime && !Number.isNaN(selectedLaunchTime.getTime());
      const selectedTimeWithinWindow = hasValidLaunchTime
        ? isWithinTomorrowMorningWindow(selectedLaunchTime)
        : false;

      if (paddlingSpeedInput.value) {
        request.paddlingSpeedMph = Number(paddlingSpeedInput.value);
      }
      if (launchTimeInput.value && selectedTimeWithinWindow) {
        request.launchTimeLocal = launchTimeInput.value;
      }

      if (launchTimeInput.value && !selectedTimeWithinWindow) {
        statusMessage.textContent = 'Near-term outlook support currently covers tomorrow between 5:00 AM and 11:59 AM local. This estimate will fall back to the latest available or baseline conditions.';
      } else {
        statusMessage.textContent = 'Loading estimate...';
      }
      statusMessage.className = 'status';

      try {
        const response = await fetch(`/api/segments/${segmentId}/estimate`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        });

        if (!response.ok) {
          const detail = await response.text();
          throw new Error(detail || 'Estimate request failed.');
        }

        const result = await response.json();
        resultSegmentName.textContent = result.segment.name;
        durationValue.textContent = formatDuration(result.estimate.estimatedDuration);
        durationDetail.textContent = `${result.estimate.distanceMiles} miles at ${result.estimate.paddlingSpeedMph} mph paddling speed`;
        effectiveSpeedValue.textContent = `${result.estimate.effectiveSpeedMph.toFixed(1)} mph`;
        currentSourceDetail.textContent = `Source: ${result.estimate.currentSource}`;
        currentValue.textContent = `${result.estimate.currentMphUsed.toFixed(1)} mph`;
        currentDetail.textContent = result.latestFlow
          ? `Latest flow observed at ${formatDate(result.latestFlow.observedAtUtc)}`
          : 'No live flow reading available. Using seeded current.';
        conditionBasisValue.textContent = getEstimateInputLabel(result.estimate);
        conditionBasisDetail.textContent = result.estimate.conditionBasisDetail || 'Estimate input state was not reported.';
        renderObservationSignal(result.latestFlow);
        finishValue.textContent = result.estimate.estimatedFinishTimeLocal
          ? formatDate(result.estimate.estimatedFinishTimeLocal)
          : '--';
        finishDetail.textContent = result.estimate.estimatedFinishTimeLocal
          ? 'Finish time based on provided launch time.'
          : 'Provide a launch time to calculate finish.';
        summaryBox.textContent = result.estimate.explanationSummary;

        if (launchTimeInput.value && !selectedTimeWithinWindow) {
          statusMessage.textContent = 'Estimate ready. The selected time is outside the bounded tomorrow-morning window, so the estimate used the reported condition basis instead.';
          statusMessage.className = 'status';
        } else if (request.launchTimeLocal && result.estimate.currentSource !== 'tomorrow-morning outlook') {
          statusMessage.textContent = 'Estimate ready. No supported tomorrow-morning outlook is available for this run right now, so the estimate used the reported condition basis instead.';
          statusMessage.className = 'status';
        } else {
          statusMessage.textContent = 'Estimate ready.';
          statusMessage.className = 'status success';
        }
      } catch (error) {
        statusMessage.textContent = error.message || 'Estimate request failed.';
        statusMessage.className = 'status error';
      }
    }

    function buildEstimateRequestForCurrentInputs() {
      const request = {};
      const selectedLaunchTime = launchTimeInput.value
        ? new Date(launchTimeInput.value)
        : null;
      const hasValidLaunchTime = selectedLaunchTime && !Number.isNaN(selectedLaunchTime.getTime());
      const selectedTimeWithinWindow = hasValidLaunchTime
        ? isWithinTomorrowMorningWindow(selectedLaunchTime)
        : false;

      if (paddlingSpeedInput.value) {
        request.paddlingSpeedMph = Number(paddlingSpeedInput.value);
      }
      if (launchTimeInput.value && selectedTimeWithinWindow) {
        request.launchTimeLocal = launchTimeInput.value;
      }

      return { request, selectedTimeWithinWindow };
    }

    function getEstimateInputLabel(estimate) {
      if (estimate.currentSource === 'tomorrow-morning outlook') {
        return 'Planned Time';
      }

      if (estimate.conditionBasis === 'Latest available conditions') {
        return 'Latest Conditions';
      }

      if (estimate.conditionBasis === 'Baseline conditions') {
        return 'Baseline';
      }

      return estimate.conditionBasis || '--';
    }

    riverSelect.addEventListener('change', async () => {
      populateSegmentSelect();
      updateSegmentDetails();
      await loadObservationSignal();
    });

    segmentSelect.addEventListener('change', async () => {
      updateSegmentDetails();
      await loadObservationSignal();
    });

    tomorrowMorningButton.addEventListener('click', () => {
      setTomorrowMorningLaunchTime();
      runEstimate();
    });
    launchDateInput.addEventListener('change', syncLaunchTimeInput);
    launchClockInput.addEventListener('change', syncLaunchTimeInput);
    estimateButton.addEventListener('click', runEstimate);
    applySupportedNearTermWindow();
    populateSegmentSelect();
    updateSegmentDetails();
    loadObservationSignal();
  </script>
</body>
</html>
""";
}

static string BuildCompletedRunSubmissionPage(List<Segment> segments, Dictionary<Guid, string> riverNames)
{
    var riverOptions = segments
        .Select(segment => new RiverOption(segment.RiverId, GetRiverName(segment, riverNames)))
        .DistinctBy(river => river.RiverId)
        .OrderBy(river => river.RiverName)
        .ToList();

    var riverOptionMarkup = string.Join(Environment.NewLine, riverOptions.Select(river =>
        $$"""
                  <option value="{{river.RiverId}}">
                    {{WebUtility.HtmlEncode(river.RiverName)}}
                  </option>
        """));

    var segmentData = BuildSegmentData(segments, riverNames);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Completed Run Submission</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f6f8;
      --surface: #ffffff;
      --surface-muted: #eef2f5;
      --border: #d6dde6;
      --text: #17212b;
      --text-muted: #586574;
      --accent: #1f6fb2;
      --accent-soft: #dbeafe;
      --success: #166534;
      --warning: #9a3412;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: "Segoe UI", Arial, sans-serif;
      background: #f7f9fb;
      color: var(--text);
    }

    main {
      max-width: 1180px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .app-shell {
      display: grid;
      grid-template-columns: 220px minmax(0, 1fr);
      gap: 24px;
      align-items: start;
    }

    .app-nav {
      position: sticky;
      top: 20px;
      display: grid;
      gap: 12px;
      padding: 18px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
    }

    .brand {
      font-size: 18px;
      font-weight: 800;
      color: var(--text);
      margin-bottom: 4px;
    }

    .app-nav a {
      display: block;
      padding: 10px 12px;
      border-radius: 6px;
      color: var(--text);
      text-decoration: none;
      font-weight: 700;
    }

    .app-nav a:hover,
    .app-nav a.active {
      background: var(--accent-soft);
      color: #174a77;
    }

    .app-content {
      min-width: 0;
    }

    .intro {
      margin-bottom: 20px;
    }

    h1, h2, p { margin: 0; }

    h1 {
      font-size: 30px;
      line-height: 1.1;
      margin-bottom: 8px;
    }

    .intro p, .detail, .summary {
      color: var(--text-muted);
      line-height: 1.5;
    }

    .panel {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
      padding: 20px;
    }

    .stack {
      display: grid;
      gap: 16px;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 16px;
    }

    label {
      display: grid;
      gap: 8px;
      font-size: 14px;
      color: var(--text-muted);
    }

    select, input, textarea, button {
      width: 100%;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
    }

    select, input {
      height: 44px;
      padding: 0 12px;
      color: var(--text);
      background: var(--surface);
    }

    textarea {
      min-height: 96px;
      padding: 12px;
      color: var(--text);
      resize: vertical;
    }

    button {
      height: 46px;
      background: var(--accent);
      color: white;
      font-weight: 600;
      border: none;
      cursor: pointer;
    }

    .segment-meta {
      display: grid;
      gap: 10px;
      background: var(--surface-muted);
      border-radius: 8px;
      padding: 14px;
    }

    .meta-row {
      display: grid;
      grid-template-columns: 128px minmax(0, 1fr);
      gap: 12px;
      font-size: 14px;
    }

    .meta-label {
      color: var(--text-muted);
    }

    .summary {
      padding: 14px;
      border-radius: 8px;
      background: var(--accent-soft);
      color: #174a77;
      font-size: 14px;
    }

    .status {
      min-height: 22px;
      font-size: 14px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 720px) {
      .app-shell {
        grid-template-columns: 1fr;
      }

      .app-nav {
        position: static;
      }

      .top-header {
        align-items: flex-start;
        flex-direction: column;
      }

      .grid {
        grid-template-columns: 1fr;
      }

      .meta-row {
        grid-template-columns: 1fr;
        gap: 4px;
      }
    }
  </style>
</head>
<body>
  <main class="app-shell">
    <nav class="app-nav" aria-label="Primary workflows">
      <div class="brand">RiverSignals</div>
      <a href="/">Segment Estimate</a>
      <a href="/compare-runs">Compare Runs</a>
      <a class="active" href="/completed-run">Report Completed Run</a>
      <a href="/request-run">Request Any Run</a>
    </nav>

    <div class="app-content">
      <header class="top-header">
        <strong>RiverSignals</strong>
        <div class="account-placeholder">Account placeholder</div>
      </header>

      <section class="intro">
        <h1>Report a Completed Run</h1>
        <p>Choose the access-to-access run you finished and send the trip details.</p>
      </section>

      <section class="panel">
      <div class="stack">
        <div class="grid">
          <label for="riverSelect">
            River
            <select id="riverSelect" name="riverId">
{{riverOptionMarkup}}
            </select>
          </label>

          <label for="segmentSelect">
            Run
            <select id="segmentSelect" name="segmentId"></select>
          </label>
        </div>

        <div class="segment-meta" aria-live="polite">
          <div class="meta-row">
            <div class="meta-label">Put-in</div>
            <div id="putInName"></div>
          </div>
          <div class="meta-row">
            <div class="meta-label">Take-out</div>
            <div id="takeOutName"></div>
          </div>
          <div class="meta-row">
            <div class="meta-label">Distance</div>
            <div id="distanceMiles"></div>
          </div>
        </div>

        <div class="grid">
          <label for="completedRunDateInput">
            Run Date
            <input id="completedRunDateInput" name="completedRunDate" type="date" />
          </label>

          <label for="completedRunStartClockInput">
            Start Time
            <input id="completedRunStartClockInput" name="completedRunStartClock" type="time" />
          </label>
        </div>
        <input id="completedRunStartInput" name="startTimeLocal" type="hidden" />

        <label for="completedRunDurationInput">
          Duration (minutes)
          <input id="completedRunDurationInput" name="durationMinutes" type="number" min="1" step="1" placeholder="Example: 135" />
        </label>

        <div class="grid">
          <label for="completedRunPutInInput">
            Put-in Notes
            <input id="completedRunPutInInput" name="putInText" type="text" placeholder="Optional access note" />
          </label>

          <label for="completedRunTakeOutInput">
            Take-out Notes
            <input id="completedRunTakeOutInput" name="takeOutText" type="text" placeholder="Optional access note" />
          </label>
        </div>

        <label for="completedRunNotesInput">
          Notes / Conditions
          <textarea id="completedRunNotesInput" name="notes" placeholder="Optional notes about conditions, pace, or anything worth keeping with the observation"></textarea>
        </label>

        <button id="submitCompletedRunButton" type="button">Submit Completed Run</button>
        <div id="completedRunStatus" class="status" aria-live="polite"></div>
        <div id="completedRunSummary" class="summary">Completed runs are saved for RiverSignals. They do not change estimates automatically.</div>
      </div>
      </section>
    </div>
  </main>

  <script>
    const segmentData = {{segmentData}};
    const riverSelect = document.getElementById('riverSelect');
    const segmentSelect = document.getElementById('segmentSelect');
    const putInName = document.getElementById('putInName');
    const takeOutName = document.getElementById('takeOutName');
    const distanceMiles = document.getElementById('distanceMiles');
    const completedRunDateInput = document.getElementById('completedRunDateInput');
    const completedRunStartClockInput = document.getElementById('completedRunStartClockInput');
    const completedRunStartInput = document.getElementById('completedRunStartInput');
    const completedRunDurationInput = document.getElementById('completedRunDurationInput');
    const completedRunPutInInput = document.getElementById('completedRunPutInInput');
    const completedRunTakeOutInput = document.getElementById('completedRunTakeOutInput');
    const completedRunNotesInput = document.getElementById('completedRunNotesInput');
    const submitCompletedRunButton = document.getElementById('submitCompletedRunButton');
    const completedRunStatus = document.getElementById('completedRunStatus');
    const completedRunSummary = document.getElementById('completedRunSummary');

    function getSegmentsForSelectedRiver() {
      return segmentData.filter(segment => segment.riverId === riverSelect.value);
    }

    function populateSegmentSelect() {
      const riverSegments = getSegmentsForSelectedRiver();
      segmentSelect.innerHTML = riverSegments.map(segment => {
        const distance = Number(segment.distanceMiles).toFixed(segment.distanceMiles % 1 === 0 ? 0 : 1);

        return `<option value="${segment.id}" data-name="${segment.name}" data-putin="${segment.putInName}" data-takeout="${segment.takeOutName}" data-distance="${distance}">${segment.name}</option>`;
      }).join('');

      segmentSelect.disabled = riverSegments.length === 0;
      submitCompletedRunButton.disabled = riverSegments.length === 0;
    }

    function getSelectedOption() {
      return segmentSelect.options[segmentSelect.selectedIndex];
    }

    function updateSegmentDetails() {
      const selected = getSelectedOption();
      if (!selected) {
        putInName.textContent = '--';
        takeOutName.textContent = '--';
        distanceMiles.textContent = '--';
        return;
      }

      putInName.textContent = selected.dataset.putin || '--';
      takeOutName.textContent = selected.dataset.takeout || '--';
      distanceMiles.textContent = selected.dataset.distance ? `${selected.dataset.distance} miles` : '--';
      completedRunPutInInput.placeholder = selected.dataset.putin
        ? `Optional details, e.g. ${selected.dataset.putin}`
        : 'Optional access note';
      completedRunTakeOutInput.placeholder = selected.dataset.takeout
        ? `Optional details, e.g. ${selected.dataset.takeout}`
        : 'Optional access note';
    }

    function formatDate(dateText) {
      if (!dateText) return '--';
      const date = new Date(dateText);
      if (Number.isNaN(date.getTime())) return '--';
      return date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
    }

    function syncCompletedRunStartInput() {
      completedRunStartInput.value = completedRunDateInput.value && completedRunStartClockInput.value
        ? `${completedRunDateInput.value}T${completedRunStartClockInput.value}`
        : '';
    }

    async function submitCompletedRun() {
      const segmentId = segmentSelect.value;
      if (!segmentId) {
        completedRunStatus.textContent = 'Select a river and run first.';
        completedRunStatus.className = 'status error';
        return;
      }

      if (!completedRunStartInput.value) {
        completedRunStatus.textContent = 'Start time is required.';
        completedRunStatus.className = 'status error';
        return;
      }

      if (!completedRunDurationInput.value) {
        completedRunStatus.textContent = 'Duration is required.';
        completedRunStatus.className = 'status error';
        return;
      }

      const request = {
        startTimeLocal: completedRunStartInput.value,
        finishTimeLocal: null,
        durationMinutes: completedRunDurationInput.value ? Number(completedRunDurationInput.value) : null,
        putInText: completedRunPutInInput.value || null,
        takeOutText: completedRunTakeOutInput.value || null,
        notes: completedRunNotesInput.value || null
      };

      completedRunStatus.textContent = 'Saving completed run...';
      completedRunStatus.className = 'status';

      try {
        const response = await fetch(`/api/segments/${segmentId}/observations`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        });

        if (!response.ok) {
          const detail = await response.text();
          throw new Error(detail || 'Completed run submission failed.');
        }

        const stored = await response.json();
        completedRunStatus.textContent = 'Completed run saved.';
        completedRunStatus.className = 'status success';
        completedRunSummary.textContent = `Completed run saved for ${stored.segmentName} starting ${formatDate(stored.startTimeLocal)}. Submitted values do not change estimates automatically.`;
      } catch (error) {
        completedRunStatus.textContent = error.message || 'Completed run submission failed.';
        completedRunStatus.className = 'status error';
      }
    }

    riverSelect.addEventListener('change', () => {
      populateSegmentSelect();
      updateSegmentDetails();
    });

    segmentSelect.addEventListener('change', updateSegmentDetails);
    completedRunDateInput.addEventListener('change', syncCompletedRunStartInput);
    completedRunStartClockInput.addEventListener('change', syncCompletedRunStartInput);
    submitCompletedRunButton.addEventListener('click', submitCompletedRun);
    populateSegmentSelect();
    updateSegmentDetails();
  </script>
</body>
</html>
""";
}

static string BuildCompareRunsPage(List<Segment> segments, Dictionary<Guid, string> riverNames)
{
    var segmentData = BuildSegmentData(segments, riverNames);
    var comparisonRows = string.Join(Environment.NewLine, segments
        .OrderBy(segment => GetRiverName(segment, riverNames))
        .ThenBy(segment => segment.RiverOrder)
        .ThenBy(segment => segment.Name)
        .Select(segment =>
        {
            var riverName = GetRiverName(segment, riverNames);
            var distance = segment.DistanceMiles.ToString("0.#");

            return $$"""
              <label class="compare-row" for="compare-{{segment.Id}}">
                <input id="compare-{{segment.Id}}" type="checkbox" value="{{segment.Id}}" />
                <span>
                  <strong>{{WebUtility.HtmlEncode(segment.Name)}}</strong>
                  <span>{{WebUtility.HtmlEncode(riverName)}} - {{WebUtility.HtmlEncode(segment.PutInName)}} to {{WebUtility.HtmlEncode(segment.TakeOutName)}}</span>
                </span>
                <span class="compare-distance">{{distance}} miles</span>
              </label>
            """;
        }));

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Compare Runs</title>
  <style>
    :root {
      color-scheme: light;
      --surface: #ffffff;
      --surface-muted: #eef2f5;
      --border: #d6dde6;
      --text: #17212b;
      --text-muted: #586574;
      --accent: #1f6fb2;
      --accent-soft: #dbeafe;
      --success: #166534;
      --warning: #9a3412;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: "Segoe UI", Arial, sans-serif;
      background: linear-gradient(180deg, #edf3f8 0%, #f7f9fb 100%);
      color: var(--text);
    }

    main {
      max-width: 1280px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .app-shell {
      display: grid;
      grid-template-columns: 220px minmax(0, 1fr);
      gap: 24px;
      align-items: start;
    }

    .app-nav {
      position: sticky;
      top: 20px;
      display: grid;
      gap: 12px;
      padding: 18px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
    }

    .brand {
      font-size: 18px;
      font-weight: 800;
      margin-bottom: 4px;
    }

    .app-nav a {
      display: block;
      padding: 10px 12px;
      border-radius: 6px;
      color: var(--text);
      text-decoration: none;
      font-weight: 700;
    }

    .app-nav a:hover,
    .app-nav a.active {
      background: var(--accent-soft);
      color: #174a77;
    }

    .top-header {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      align-items: center;
      margin-bottom: 20px;
      padding: 16px 18px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
    }

    .account-placeholder {
      color: var(--text-muted);
      font-size: 13px;
      white-space: nowrap;
    }

    h1, p { margin: 0; }

    .intro {
      margin-bottom: 20px;
    }

    .intro h1 {
      font-size: 30px;
      line-height: 1.1;
      margin-bottom: 8px;
    }

    .intro p {
      color: var(--text-muted);
      line-height: 1.5;
      max-width: 720px;
    }

    .panel {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(23, 33, 43, 0.08);
      padding: 20px;
    }

    .comparison-options {
      display: grid;
      gap: 10px;
      margin-top: 16px;
    }

    .compare-row {
      display: grid;
      grid-template-columns: 24px minmax(0, 1fr) auto;
      gap: 10px;
      align-items: center;
      padding: 10px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
    }

    .compare-row input {
      width: 18px;
      height: 18px;
      padding: 0;
    }

    .compare-row strong {
      display: block;
      font-size: 14px;
    }

    .compare-row span span {
      display: block;
      color: var(--text-muted);
      font-size: 13px;
      margin-top: 4px;
    }

    .compare-distance {
      color: var(--text-muted);
      font-size: 13px;
      white-space: nowrap;
    }

    button {
      width: 100%;
      height: 46px;
      margin-top: 16px;
      border: none;
      border-radius: 6px;
      background: var(--accent);
      color: white;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }

    .comparison-table-wrap {
      overflow-x: auto;
      margin-top: 16px;
      border: 1px solid var(--border);
      border-radius: 8px;
    }

    .comparison-table {
      width: 100%;
      min-width: 760px;
      border-collapse: collapse;
      background: var(--surface);
    }

    .comparison-table th,
    .comparison-table td {
      padding: 10px;
      border-bottom: 1px solid var(--border);
      text-align: left;
      vertical-align: top;
      font-size: 13px;
    }

    .comparison-table th {
      background: var(--surface-muted);
      color: var(--text-muted);
      font-weight: 700;
    }

    .status {
      margin-top: 14px;
      min-height: 22px;
      font-size: 14px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 900px) {
      .app-shell {
        grid-template-columns: 1fr;
      }

      .app-nav {
        position: static;
      }

      .top-header {
        align-items: flex-start;
        flex-direction: column;
      }

      .compare-row {
        grid-template-columns: 24px minmax(0, 1fr);
      }

      .compare-distance {
        grid-column: 2;
      }
    }
  </style>
</head>
<body>
  <main class="app-shell">
    <nav class="app-nav" aria-label="Primary workflows">
      <div class="brand">RiverSignals</div>
      <a href="/">Segment Estimate</a>
      <a class="active" href="/compare-runs">Compare Runs</a>
      <a href="/completed-run">Report Completed Run</a>
      <a href="/request-run">Request Any Run</a>
    </nav>

    <div class="app-content">
      <header class="top-header">
        <strong>RiverSignals</strong>
        <div class="account-placeholder">Account placeholder</div>
      </header>

      <section class="intro">
        <h1>Compare Runs</h1>
        <p>Select known runs and compare their current estimated duration using the existing estimate service.</p>
      </section>

      <section class="panel" aria-labelledby="compareRunsHeading">
        <h2 id="compareRunsHeading">Runs</h2>
        <div id="comparisonOptions" class="comparison-options">
{{comparisonRows}}
        </div>
        <button id="compareSelectedRunsButton" type="button">Compare Selected Runs</button>
        <div id="comparisonStatus" class="status" aria-live="polite"></div>
        <div class="comparison-table-wrap">
          <table class="comparison-table">
            <thead>
              <tr>
                <th>Run</th>
                <th>Distance</th>
                <th>Duration</th>
                <th>Effective speed</th>
                <th>Estimate input</th>
              </tr>
            </thead>
            <tbody id="comparisonResults">
              <tr><td colspan="5">Select runs to compare.</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  </main>

  <script>
    const segmentData = {{segmentData}};
    const comparisonOptions = document.getElementById('comparisonOptions');
    const compareSelectedRunsButton = document.getElementById('compareSelectedRunsButton');
    const comparisonStatus = document.getElementById('comparisonStatus');
    const comparisonResults = document.getElementById('comparisonResults');

    function formatDuration(durationText) {
      if (!durationText) return '--';
      const match = /^([0-9]+):([0-9]{2}):([0-9]{2}(?:\\.[0-9]+)?)$/.exec(durationText);
      if (!match) return durationText;
      const hours = Number(match[1]);
      const minutes = Number(match[2]);
      if (hours === 0) return `${minutes} min`;
      return `${hours} hr ${minutes} min`;
    }

    function getEstimateInputLabel(estimate) {
      if (estimate.currentSource === 'tomorrow-morning outlook') return 'Planned Time';
      if (estimate.conditionBasis === 'Latest available conditions') return 'Latest Conditions';
      if (estimate.conditionBasis === 'Baseline conditions') return 'Baseline';
      return estimate.conditionBasis || '--';
    }

    async function compareSelectedRuns() {
      const selectedIds = Array.from(comparisonOptions.querySelectorAll('input:checked')).map(input => input.value);

      if (selectedIds.length === 0) {
        comparisonStatus.textContent = 'Select at least one run.';
        comparisonStatus.className = 'status error';
        return;
      }

      comparisonStatus.textContent = 'Loading comparison...';
      comparisonStatus.className = 'status';

      try {
        const results = await Promise.all(selectedIds.map(async segmentId => {
          const response = await fetch(`/api/segments/${segmentId}/estimate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
          });

          if (!response.ok) {
            const detail = await response.text();
            throw new Error(detail || 'Comparison estimate failed.');
          }

          return response.json();
        }));

        comparisonResults.innerHTML = results.map(result => `
          <tr>
            <td>${result.segment.name}</td>
            <td>${result.estimate.distanceMiles} miles</td>
            <td>${formatDuration(result.estimate.estimatedDuration)}</td>
            <td>${Number(result.estimate.effectiveSpeedMph).toFixed(1)} mph</td>
            <td>${getEstimateInputLabel(result.estimate)}</td>
          </tr>
        `).join('');
        comparisonStatus.textContent = 'Comparison ready.';
        comparisonStatus.className = 'status success';
      } catch (error) {
        comparisonStatus.textContent = error.message || 'Comparison failed.';
        comparisonStatus.className = 'status error';
      }
    }

    compareSelectedRunsButton.addEventListener('click', compareSelectedRuns);
  </script>
</body>
</html>
""";
}

static string GetRiverName(Segment segment, IReadOnlyDictionary<Guid, string> riverNames)
{
    if (riverNames.TryGetValue(segment.RiverId, out var riverName))
        return riverName;

    var separatorIndex = segment.Name.IndexOf(" - ", StringComparison.Ordinal);
    return separatorIndex > 0
        ? segment.Name[..separatorIndex]
        : segment.Name;
}

static string BuildSegmentData(List<Segment> segments, Dictionary<Guid, string> riverNames)
{
    return JsonSerializer.Serialize(segments
        .Select(segment => new UiSegmentOption(
            segment.Id,
            segment.RiverId,
            segment.Name,
            GetRiverName(segment, riverNames),
            segment.RiverOrder,
            segment.PutInName,
            segment.TakeOutName,
            segment.DistanceMiles,
            segment.DistanceSource,
            segment.PutInAddress,
            segment.TakeOutAddress,
            segment.PutInAmenities,
            segment.TakeOutAmenities,
            segment.PlanningSource,
            segment.DefaultCurrentMph))
        .OrderBy(segment => segment.RiverName)
        .ThenBy(segment => segment.RiverOrder)
        .ThenBy(segment => segment.Name)
        .ToList(),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

static string BuildRawAccessCandidateReviewPage(IReadOnlyList<RawAccessPointCandidate> candidates)
{
    var reviewOptions = Enum.GetValues<RawAccessPointReviewState>()
        .Select(state => $$"""<option value="{{state}}">{{FormatReviewState(state)}}</option>""")
        .ToList();
    var reviewStateCounts = Enum.GetValues<RawAccessPointReviewState>()
        .Select(state =>
        {
            var label = FormatReviewState(state);
            var count = candidates.Count(candidate => candidate.ReviewState == state);

            return $$"""<span class="count-pill" data-count-state="{{state}}">{{label}}: <strong>{{count}}</strong></span>""";
        });
    var sourceOptions = candidates
        .Select(candidate => candidate.SourceName)
        .Distinct()
        .OrderBy(sourceName => sourceName)
        .Select(sourceName => $$"""<option value="{{WebUtility.HtmlEncode(sourceName)}}">{{WebUtility.HtmlEncode(sourceName)}}</option>""");

    var rows = string.Join(Environment.NewLine, candidates
        .OrderBy(candidate => candidate.SourceName)
        .ThenBy(candidate => candidate.Name)
        .Select(candidate =>
        {
            var location = candidate.Latitude.HasValue && candidate.Longitude.HasValue
                ? $"{candidate.Latitude.Value}, {candidate.Longitude.Value}"
                : candidate.Address ?? "--";
            var optionMarkup = string.Join(string.Empty, reviewOptions.Select(option =>
                option.Replace(
                    $"value=\"{candidate.ReviewState}\"",
                    $"value=\"{candidate.ReviewState}\" selected")));

            return $$"""
              <tr data-review-state="{{candidate.ReviewState}}" data-source-name="{{WebUtility.HtmlEncode(candidate.SourceName)}}">
                <td>
                  <strong>{{WebUtility.HtmlEncode(candidate.Name)}}</strong>
                  <div class="muted">{{WebUtility.HtmlEncode(candidate.RiverName)}}</div>
                </td>
                <td>{{WebUtility.HtmlEncode(location)}}</td>
                <td>{{WebUtility.HtmlEncode(candidate.DescriptiveClues ?? "--")}}</td>
                <td>
                  <div>{{WebUtility.HtmlEncode(candidate.SourceName)}}</div>
                  <div class="muted">{{candidate.SourceType}}</div>
                  <a href="{{WebUtility.HtmlEncode(candidate.SourceUrl)}}" rel="noreferrer">Source</a>
                </td>
                <td>
                  <select class="review-state" data-candidate-id="{{candidate.Id}}">
                    {{optionMarkup}}
                  </select>
                </td>
                <td>
                  <textarea class="review-note" data-candidate-id="{{candidate.Id}}" placeholder="Optional reviewer note">{{WebUtility.HtmlEncode(candidate.ReviewerNote ?? string.Empty)}}</textarea>
                </td>
                <td>
                  <button class="save-review" type="button" data-candidate-id="{{candidate.Id}}">Save</button>
                </td>
              </tr>
            """;
        }));

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Raw Access Candidate Review</title>
  <style>
    :root {
      color-scheme: light;
      --surface: #ffffff;
      --surface-muted: #eef2f5;
      --border: #d6dde6;
      --text: #17212b;
      --text-muted: #586574;
      --accent: #1f6fb2;
      --success: #166534;
      --warning: #9a3412;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: "Segoe UI", Arial, sans-serif;
      background: #f7f9fb;
      color: var(--text);
    }

    main {
      max-width: 1280px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    h1, p { margin: 0; }

    .intro {
      margin-bottom: 20px;
    }

    .intro p, .muted, .notice {
      color: var(--text-muted);
      line-height: 1.5;
    }

    .notice {
      margin-bottom: 16px;
      padding: 14px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface-muted);
    }

    .worklist {
      display: grid;
      gap: 14px;
      margin-bottom: 16px;
      padding: 14px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
    }

    .counts {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .count-pill {
      padding: 7px 10px;
      border: 1px solid var(--border);
      border-radius: 999px;
      background: var(--surface-muted);
      color: var(--text-muted);
      font-size: 13px;
    }

    .filters {
      display: grid;
      grid-template-columns: repeat(3, minmax(180px, 1fr));
      gap: 12px;
      align-items: end;
    }

    label {
      display: grid;
      gap: 6px;
      color: var(--text-muted);
      font-size: 13px;
      font-weight: 700;
    }

    .table-wrap {
      overflow-x: auto;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 8px;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      min-width: 1120px;
    }

    th, td {
      padding: 12px;
      border-bottom: 1px solid var(--border);
      text-align: left;
      vertical-align: top;
      font-size: 14px;
    }

    th {
      background: var(--surface-muted);
      color: var(--text-muted);
      font-weight: 700;
    }

    select, textarea, button {
      width: 100%;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
    }

    select {
      height: 40px;
      padding: 0 10px;
      background: var(--surface);
    }

    textarea {
      min-height: 70px;
      padding: 10px;
      resize: vertical;
    }

    button {
      height: 40px;
      background: var(--accent);
      color: white;
      border: none;
      font-weight: 700;
      cursor: pointer;
    }

    a { color: var(--accent); }

    .status {
      min-height: 22px;
      margin-top: 14px;
      font-size: 14px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 760px) {
      .filters {
        grid-template-columns: 1fr;
      }
    }
  </style>
</head>
<body>
  <main>
    <section class="intro">
      <h1>Raw Access Candidate Review</h1>
      <p>Internal review for raw, unresolved source records. Review labels do not create access points, runs, river miles, gauges, or planning entities.</p>
    </section>
    <div class="notice">These records are unresolved source material. Use labels and notes for stewardship only.</div>
    <section class="worklist" aria-label="Raw candidate worklist status">
      <div class="counts" id="reviewCounts">
{{string.Join(Environment.NewLine, reviewStateCounts)}}
      </div>
      <div class="filters">
        <label for="reviewStateFilter">
          Review label
          <select id="reviewStateFilter">
            <option value="">All labels</option>
{{string.Join(Environment.NewLine, Enum.GetValues<RawAccessPointReviewState>().Select(state => $$"""            <option value="{{state}}">{{FormatReviewState(state)}}</option>"""))}}
          </select>
        </label>
        <label for="sourceFilter">
          Source
          <select id="sourceFilter">
            <option value="">All sources</option>
{{string.Join(Environment.NewLine, sourceOptions)}}
          </select>
        </label>
        <label for="visibleCandidateCount">
          Visible records
          <output id="visibleCandidateCount">{{candidates.Count}}</output>
        </label>
      </div>
    </section>
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Candidate</th>
            <th>Location</th>
            <th>Clues</th>
            <th>Source</th>
            <th>Review label</th>
            <th>Reviewer note</th>
            <th>Action</th>
          </tr>
        </thead>
        <tbody>
{{rows}}
        </tbody>
      </table>
    </div>
    <div id="reviewStatus" class="status" aria-live="polite"></div>
  </main>
  <script>
    const reviewStatus = document.getElementById('reviewStatus');
    const reviewStateFilter = document.getElementById('reviewStateFilter');
    const sourceFilter = document.getElementById('sourceFilter');
    const visibleCandidateCount = document.getElementById('visibleCandidateCount');
    const candidateRows = Array.from(document.querySelectorAll('tbody tr'));

    function refreshReviewCounts() {
      document.querySelectorAll('[data-count-state]').forEach(countPill => {
        const state = countPill.dataset.countState;
        const count = candidateRows.filter(row => row.dataset.reviewState === state).length;
        const label = countPill.textContent.split(':')[0];
        countPill.innerHTML = `${label}: <strong>${count}</strong>`;
      });
    }

    function applyFilters() {
      const selectedState = reviewStateFilter.value;
      const selectedSource = sourceFilter.value;
      let visibleCount = 0;

      candidateRows.forEach(row => {
        const stateMatches = !selectedState || row.dataset.reviewState === selectedState;
        const sourceMatches = !selectedSource || row.dataset.sourceName === selectedSource;
        const isVisible = stateMatches && sourceMatches;

        row.hidden = !isVisible;
        if (isVisible) {
          visibleCount += 1;
        }
      });

      visibleCandidateCount.textContent = visibleCount;
    }

    async function saveReview(candidateId) {
      const state = document.querySelector(`.review-state[data-candidate-id="${candidateId}"]`).value;
      const note = document.querySelector(`.review-note[data-candidate-id="${candidateId}"]`).value;

      reviewStatus.textContent = 'Saving review label...';
      reviewStatus.className = 'status';

      try {
        const response = await fetch(`/api/raw-access-point-candidates/${candidateId}/review`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ reviewState: state, reviewerNote: note || null })
        });

        if (!response.ok) {
          const detail = await response.text();
          throw new Error(detail || 'Review label could not be saved.');
        }

        reviewStatus.textContent = 'Review label saved. Candidate remains raw and unresolved.';
        reviewStatus.className = 'status success';
        const row = document.querySelector(`.save-review[data-candidate-id="${candidateId}"]`).closest('tr');
        row.dataset.reviewState = state;
        refreshReviewCounts();
        applyFilters();
      } catch (error) {
        reviewStatus.textContent = error.message || 'Review label could not be saved.';
        reviewStatus.className = 'status error';
      }
    }

    document.querySelectorAll('.save-review').forEach(button => {
      button.addEventListener('click', () => saveReview(button.dataset.candidateId));
    });
    reviewStateFilter.addEventListener('change', applyFilters);
    sourceFilter.addEventListener('change', applyFilters);
    refreshReviewCounts();
    applyFilters();
  </script>
</body>
</html>
""";
}

static string FormatReviewState(RawAccessPointReviewState state)
{
    return state switch
    {
        RawAccessPointReviewState.NeedsMoreSourceContext => "needs_more_source_context",
        RawAccessPointReviewState.DuplicateCandidate => "duplicate_candidate",
        RawAccessPointReviewState.LikelyExistingAccessPoint => "likely_existing_access_point",
        RawAccessPointReviewState.PromotableLater => "promotable_later",
        RawAccessPointReviewState.OutOfScope => "out_of_scope",
        _ => "unreviewed"
    };
}

internal sealed record RiverOption(Guid RiverId, string RiverName);

internal sealed record UiSegmentOption(
    Guid Id,
    Guid RiverId,
    string Name,
    string RiverName,
    int RiverOrder,
    string PutInName,
    string TakeOutName,
    double DistanceMiles,
    string? DistanceSource,
    string? PutInAddress,
    string? TakeOutAddress,
    string? PutInAmenities,
    string? TakeOutAmenities,
    string? PlanningSource,
    double? DefaultCurrentMph);

public partial class Program { }
