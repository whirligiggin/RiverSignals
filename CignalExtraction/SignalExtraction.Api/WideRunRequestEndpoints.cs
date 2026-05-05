using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

public static class WideRunRequestEndpoints
{
    public static IEndpointRouteBuilder MapWideRunRequestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/request-run", () =>
        {
            return Results.Content(BuildRequestRunPage(), "text/html");
        });

        app.MapPost("/api/run-requests", (
            WideRunRequest request,
            IWideRunRequestService wideRunRequestService,
            IBestAvailableRunEstimateService bestAvailableRunEstimateService) =>
        {
            try
            {
                var stored = wideRunRequestService.Store(request);
                var result = bestAvailableRunEstimateService.Estimate(request, stored);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return app;
    }

    private static string BuildRequestRunPage()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Request a Run</title>
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
      max-width: 720px;
      line-height: 1.5;
    }

    .panel {
      max-width: 760px;
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

    .two-column {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 12px;
    }

    label {
      display: grid;
      gap: 8px;
      font-size: 14px;
      color: var(--text-muted);
    }

    input, textarea, button {
      width: 100%;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
    }

    input {
      height: 44px;
      padding: 0 12px;
      color: var(--text);
      background: var(--surface);
    }

    textarea {
      min-height: 96px;
      padding: 12px;
      color: var(--text);
      background: var(--surface);
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

    button:hover { background: #175f98; }

    .summary {
      margin-top: 12px;
      padding: 14px;
      border-radius: 8px;
      background: var(--accent-soft);
      color: #174a77;
      font-size: 14px;
      line-height: 1.5;
    }

    .status {
      margin-top: 14px;
      min-height: 22px;
      font-size: 14px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 900px) {
      .app-shell,
      .two-column {
        grid-template-columns: 1fr;
      }

      .app-nav {
        position: static;
      }

      .top-header {
        align-items: flex-start;
        flex-direction: column;
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
      <a href="/completed-run">Report Completed Run</a>
      <a class="active" href="/request-run">Request Any Run</a>
    </nav>
    <div class="app-content">
      <header class="top-header">
        <strong>RiverSignals</strong>
        <div class="account-placeholder">Account placeholder</div>
      </header>

      <section class="intro">
        <h1>Request a Run</h1>
        <p>Send a river and access-to-access run that is not listed yet.</p>
      </section>
      <section class="panel">
        <div class="stack">
          <label for="riverNameInput">
            River
            <input id="riverNameInput" type="text" autocomplete="off" />
          </label>
          <div class="two-column">
            <label for="putInInput">
              Put-in
              <input id="putInInput" type="text" autocomplete="off" />
            </label>
            <label for="takeOutInput">
              Take-out
              <input id="takeOutInput" type="text" autocomplete="off" />
            </label>
          </div>
          <div class="two-column">
            <label for="putInAliasInput">
              Put-in alias
              <input id="putInAliasInput" type="text" autocomplete="off" />
            </label>
            <label for="takeOutAliasInput">
              Take-out alias
              <input id="takeOutAliasInput" type="text" autocomplete="off" />
            </label>
          </div>
          <label for="roughLocationInput">
            Rough location
            <input id="roughLocationInput" type="text" autocomplete="off" />
          </label>
          <label for="sourceHintsInput">
            Source hints
            <input id="sourceHintsInput" type="text" autocomplete="off" />
          </label>
          <label for="notesInput">
            Notes
            <textarea id="notesInput"></textarea>
          </label>
          <button id="submitRunRequestButton" type="button">Submit Run Request</button>
          <div id="runRequestStatus" class="status" aria-live="polite"></div>
          <div id="runRequestSummary" class="summary">When there is enough known context, RiverSignals returns a first estimate immediately.</div>
        </div>
      </section>
    </div>
  </main>
  <script>
    const riverNameInput = document.getElementById('riverNameInput');
    const putInInput = document.getElementById('putInInput');
    const takeOutInput = document.getElementById('takeOutInput');
    const putInAliasInput = document.getElementById('putInAliasInput');
    const takeOutAliasInput = document.getElementById('takeOutAliasInput');
    const roughLocationInput = document.getElementById('roughLocationInput');
    const sourceHintsInput = document.getElementById('sourceHintsInput');
    const notesInput = document.getElementById('notesInput');
    const submitRunRequestButton = document.getElementById('submitRunRequestButton');
    const runRequestStatus = document.getElementById('runRequestStatus');
    const runRequestSummary = document.getElementById('runRequestSummary');

    async function submitRunRequest() {
      const request = {
        riverName: riverNameInput.value || null,
        putInText: putInInput.value || null,
        takeOutText: takeOutInput.value || null,
        putInAlias: putInAliasInput.value || null,
        takeOutAlias: takeOutAliasInput.value || null,
        roughLocation: roughLocationInput.value || null,
        sourceHints: sourceHintsInput.value || null,
        notes: notesInput.value || null
      };

      runRequestStatus.textContent = 'Saving request...';
      runRequestStatus.className = 'status';

      try {
        const response = await fetch('/api/run-requests', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        });

        if (!response.ok) {
          const detail = await response.text();
          throw new Error(detail || 'Run request could not be saved.');
        }

        const result = await response.json();
        const stored = result.request;
        runRequestStatus.textContent = 'Run request saved.';
        runRequestStatus.className = 'status success';
        if (result.estimate) {
          const duration = formatDuration(result.estimate.estimatedDuration);
          runRequestSummary.textContent = `Request saved for ${stored.riverName}: ${stored.putInText} to ${stored.takeOutText}. First estimate: ${duration}.`;
        } else {
          runRequestSummary.textContent = `Request saved for ${stored.riverName}: ${stored.putInText} to ${stored.takeOutText}. More river context is needed before an estimate can be calculated.`;
        }
      } catch (error) {
        runRequestStatus.textContent = error.message || 'Run request could not be saved.';
        runRequestStatus.className = 'status error';
      }
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

    submitRunRequestButton.addEventListener('click', submitRunRequest);
  </script>
</body>
</html>
""";
    }
}
