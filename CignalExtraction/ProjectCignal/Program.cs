
using ProjectCignal.Services;
using ProjectCignal.Models;
using SignalExtraction.Core.Services;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<ProjectCignal.Services.TripEstimatorService>();
builder.Services.AddScoped<ITripEstimationService, TripEstimationService>();
builder.Services.AddScoped<ISegmentCatalogService, SegmentCatalogService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapPost("/api/trip-estimates",(TripEstimateRequest request, TripEstimatorService tripEstimatorService) =>
{
    if (string.IsNullOrWhiteSpace(request.SegmentName)) {
        return Results.BadRequest(new { error = "SegmentName is required." });
    }
    if (request.DistanceMiles <= 0) {
        return Results.BadRequest(new { error = "DistanceMiles must be a positive number." });
    }
    if (request.PaddlingSpeedMph <= 0) {
        return Results.BadRequest(new { error = "PaddlingSpeedMph must be a positive number." });
    }
try
    {
        var estimate = tripEstimatorService.Estimate(
            request.SegmentName,
            request.DistanceMiles,
            request.PaddlingSpeedMph,
            request.RiverCurrentMph,
            request.LaunchTime);

        var response = new TripEstimateResponse
        {
            SegmentName = estimate.SegmentName,
            EstimatedDurationHours = estimate.EstimatedDuration.TotalHours,
            EstimatedDurationText = tripEstimatorService.FormatDuration(estimate.EstimatedDuration),
            EstimatedFinishTime = estimate.EstimatedFinishTime,
            Assumptions = estimate.Assumptions
        };
        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }  
});

app.Run();