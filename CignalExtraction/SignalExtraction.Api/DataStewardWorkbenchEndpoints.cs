using System.Net;
using System.Text.Json;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

public static class DataStewardWorkbenchEndpoints
{
    public static IEndpointRouteBuilder MapDataStewardWorkbenchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/data-steward", (ICanonicalWorkbenchService canonicalWorkbenchService) =>
        {
            return Results.Content(
                BuildDataStewardWorkbenchPage(canonicalWorkbenchService.GetTables()),
                "text/html");
        });

        app.MapGet("/api/internal/data-steward/tables", (ICanonicalWorkbenchService canonicalWorkbenchService) =>
        {
            return Results.Ok(canonicalWorkbenchService.GetTables());
        });

        app.MapGet("/api/internal/data-steward/{tableName}", (
            string tableName,
            ICanonicalWorkbenchService canonicalWorkbenchService) =>
        {
            try
            {
                return Results.Ok(canonicalWorkbenchService.GetRecords(tableName));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapPut("/api/internal/data-steward/{tableName}/{id}", (
            string tableName,
            string id,
            CanonicalWorkbenchUpdateRequest request,
            ICanonicalWorkbenchService canonicalWorkbenchService) =>
        {
            try
            {
                var record = canonicalWorkbenchService.UpdateRecord(tableName, id, request.Values);
                return record == null
                    ? Results.NotFound($"No {tableName} record found for {id}.")
                    : Results.Ok(record);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return app;
    }

    private static string BuildDataStewardWorkbenchPage(IReadOnlyList<CanonicalWorkbenchTable> tables)
    {
        var tableData = JsonSerializer.Serialize(tables, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var tableOptions = string.Join(Environment.NewLine, tables.Select(table =>
            $$"""            <option value="{{WebUtility.HtmlEncode(table.Name)}}">{{WebUtility.HtmlEncode(table.Label)}}</option>"""));

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Data Steward Workbench</title>
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

    h1, h2, p { margin: 0; }

    .intro {
      display: grid;
      gap: 8px;
      margin-bottom: 18px;
    }

    .intro p, .notice, .muted, .status {
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

    .toolbar {
      display: grid;
      grid-template-columns: minmax(220px, 360px) minmax(0, 1fr);
      gap: 12px;
      align-items: end;
      margin-bottom: 16px;
      padding: 14px;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
    }

    label {
      display: grid;
      gap: 6px;
      color: var(--text-muted);
      font-size: 13px;
      font-weight: 700;
    }

    select, input, textarea, button {
      width: 100%;
      border-radius: 6px;
      border: 1px solid var(--border);
      font: inherit;
    }

    select, input {
      height: 40px;
      padding: 0 10px;
      background: var(--surface);
      color: var(--text);
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

    .table-wrap {
      overflow-x: auto;
      border: 1px solid var(--border);
      border-radius: 8px;
      background: var(--surface);
    }

    table {
      width: 100%;
      min-width: 1180px;
      border-collapse: collapse;
    }

    th, td {
      padding: 10px;
      border-bottom: 1px solid var(--border);
      text-align: left;
      vertical-align: top;
      font-size: 13px;
    }

    th {
      background: var(--surface-muted);
      color: var(--text-muted);
      font-weight: 700;
    }

    .field-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(180px, 1fr));
      gap: 10px;
    }

    .record-id {
      font-family: Consolas, "Courier New", monospace;
      font-size: 12px;
      word-break: break-word;
    }

    .status {
      min-height: 22px;
    }

    .status.success { color: var(--success); }
    .status.error { color: var(--warning); }

    @media (max-width: 880px) {
      .toolbar,
      .field-grid {
        grid-template-columns: 1fr;
      }
    }
  </style>
</head>
<body>
  <main>
    <section class="intro">
      <h1>Data Steward Workbench</h1>
      <p>Internal stewardship for canonical and provisional RiverSignals records.</p>
    </section>
    <div class="notice">Edits are direct durable record updates. Stored, reviewed, or edited records are not automatically trusted, promoted, reconciled, deduplicated, or used by estimates.</div>
    <section class="toolbar" aria-label="Data steward table controls">
      <label for="tableSelect">
        Canonical table
        <select id="tableSelect">
{{tableOptions}}
        </select>
      </label>
      <div id="statusMessage" class="status" aria-live="polite"></div>
    </section>
    <div id="recordsPanel" class="table-wrap" aria-live="polite"></div>
  </main>
  <script>
    const tableDefinitions = {{tableData}};
    const tableSelect = document.getElementById('tableSelect');
    const statusMessage = document.getElementById('statusMessage');
    const recordsPanel = document.getElementById('recordsPanel');

    function getSelectedTable() {
      return tableDefinitions.find(table => table.name === tableSelect.value);
    }

    function escapeHtml(value) {
      return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
    }

    function inputForField(record, field) {
      const value = record.values[field] ?? '';
      const safeId = `${record.id}-${field}`.replaceAll(/[^a-zA-Z0-9_-]/g, '_');
      const isLong = field.includes('notes') || field.includes('text');

      if (isLong) {
        return `<label for="${safeId}">${field}<textarea id="${safeId}" data-field="${field}">${escapeHtml(value)}</textarea></label>`;
      }

      return `<label for="${safeId}">${field}<input id="${safeId}" data-field="${field}" value="${escapeHtml(value)}" /></label>`;
    }

    function renderRecords(table, records) {
      if (records.length === 0) {
        recordsPanel.innerHTML = '<div class="notice">No records exist in this table yet.</div>';
        return;
      }

      const rows = records.map(record => `
        <tr data-record-id="${escapeHtml(record.id)}">
          <td class="record-id">${escapeHtml(record.id)}</td>
          <td><div class="field-grid">${table.editableFields.map(field => inputForField(record, field)).join('')}</div></td>
          <td><button type="button" data-action="save" data-record-id="${escapeHtml(record.id)}">Save</button></td>
        </tr>`).join('');

      recordsPanel.innerHTML = `
        <table>
          <thead>
            <tr>
              <th>Record ID</th>
              <th>Editable fields</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>${rows}</tbody>
        </table>`;

      recordsPanel.querySelectorAll('[data-action="save"]').forEach(button => {
        button.addEventListener('click', () => saveRecord(button.dataset.recordId));
      });
    }

    async function loadRecords() {
      const table = getSelectedTable();
      if (!table) return;

      statusMessage.textContent = `Loading ${table.label}...`;
      statusMessage.className = 'status';

      try {
        const response = await fetch(`/api/internal/data-steward/${table.name}`);
        if (!response.ok) throw new Error(await response.text());

        const records = await response.json();
        renderRecords(table, records);
        statusMessage.textContent = `${records.length} ${table.label} record(s) loaded.`;
        statusMessage.className = 'status success';
      } catch (error) {
        recordsPanel.innerHTML = '';
        statusMessage.textContent = error.message || 'Records could not be loaded.';
        statusMessage.className = 'status error';
      }
    }

    async function saveRecord(recordId) {
      const table = getSelectedTable();
      const row = recordsPanel.querySelector(`tr[data-record-id="${CSS.escape(recordId)}"]`);
      const values = {};

      row.querySelectorAll('[data-field]').forEach(input => {
        values[input.dataset.field] = input.value || null;
      });

      statusMessage.textContent = `Saving ${recordId}...`;
      statusMessage.className = 'status';

      try {
        const response = await fetch(`/api/internal/data-steward/${table.name}/${encodeURIComponent(recordId)}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ values })
        });

        if (!response.ok) throw new Error(await response.text());

        statusMessage.textContent = 'Record saved. No trust, promotion, reconciliation, or estimate influence was applied.';
        statusMessage.className = 'status success';
        await loadRecords();
      } catch (error) {
        statusMessage.textContent = error.message || 'Record could not be saved.';
        statusMessage.className = 'status error';
      }
    }

    tableSelect.addEventListener('change', loadRecords);
    loadRecords();
  </script>
</body>
</html>
""";
    }
}
