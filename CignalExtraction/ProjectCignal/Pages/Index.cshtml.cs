using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ProjectCignal.Pages;

public class IndexModel : PageModel
{
    private readonly ITripEstimationService _tripEstimationService;
    private readonly ISegmentCatalogService _segmentCatalogService;

    public IndexModel(
        ITripEstimationService tripEstimationService,
        ISegmentCatalogService segmentCatalogService)
    {
        _tripEstimationService = tripEstimationService;
        _segmentCatalogService = segmentCatalogService;
    }

    [BindProperty]
    public TripInput Input { get; set; } = new();

    public TripResult? Result { get; set; }

    public List<Segment> PresetSegments { get; set; } = new();

    public void OnGet()
    {
        PresetSegments = _segmentCatalogService.GetPresetSegments();
    }

    public void OnPost()
    {
        PresetSegments = _segmentCatalogService.GetPresetSegments();

        if (!string.IsNullOrEmpty(Input.SelectedSegment))
        {
            var segment = PresetSegments.FirstOrDefault(s => s.Name == Input.SelectedSegment);
            if (segment != null)
            {
                Input.SegmentId = segment.Id;
                Input.SegmentName = segment.Name;
                Input.DistanceMiles = segment.DistanceMiles;
                Input.RiverCurrentMph = segment.DefaultCurrentMph ?? 0;

                ModelState.Remove("Input.SegmentName");
                ModelState.Remove("Input.DistanceMiles");
                ModelState.Remove("Input.RiverCurrentMph");
            }
        }

        if (!ModelState.IsValid)
        {
            return;
        }

        try
        {
            var request = new TripEstimateRequest
            {
                SegmentId = Input.SegmentId,
                SegmentName = Input.SegmentName,
                DistanceMiles = Input.DistanceMiles,
                PaddlingSpeedMph = Input.PaddlingSpeedMph,
                RiverCurrentMphOverride = Input.RiverCurrentMph,
                LaunchTimeLocal = Input.LaunchTime
            };

            var estimate = _tripEstimationService.Estimate(request);

            Result = new TripResult
            {
                SegmentName = estimate.SegmentName,
                EstimatedDurationText = _tripEstimationService.FormatDuration(estimate.EstimatedDuration),
                EstimatedFinishTimeText = estimate.EstimatedFinishTimeLocal.HasValue
                    ? estimate.EstimatedFinishTimeLocal.Value.ToString("f")
                    : "No launch time provided",
                Assumptions = estimate.Assumptions
            };
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
    }

    public class TripInput
    {
        [Display(Name = "Preset Segment")]
        public string? SelectedSegment { get; set; }

        public Guid? SegmentId { get; set; }

        [Display(Name = "River Segment")]
        [Required]
        public string SegmentName { get; set; } = string.Empty;

        [Display(Name = "Distance (miles)")]
        [Range(0.1, 500)]
        public double DistanceMiles { get; set; }

        [Display(Name = "Paddling Speed (mph)")]
        [Range(0.1, 20)]
        public double PaddlingSpeedMph { get; set; }

        [Display(Name = "River Current (mph)")]
        [Range(-10, 20)]
        public double RiverCurrentMph { get; set; }

        [Display(Name = "Launch Time")]
        [DataType(DataType.DateTime)]
        public DateTime? LaunchTime { get; set; }
    }

    public class TripResult
    {
        public string SegmentName { get; set; } = string.Empty;
        public string EstimatedDurationText { get; set; } = string.Empty;
        public string EstimatedFinishTimeText { get; set; } = string.Empty;
        public string Assumptions { get; set; } = string.Empty;
    }
}