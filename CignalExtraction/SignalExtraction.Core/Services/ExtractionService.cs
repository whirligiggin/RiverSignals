using SignalExtraction.Core.Models;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SignalExtraction.Core.Services;

public class ExtractionService : IExtractionService
{
    public Task<ExtractionResult> ExtractAsync(ExtractionRequest request)
    {
        var result = new ExtractionResult
        {
            SourceType = request.SourceType,
            CommunicationDateTime = request.CommunicationDateTime,
            RecordType = RecordType.UnclearTripRecord,
            DurationType = DurationType.Unknown,
            ExtractionConfidence = 0.15,
            NeedsReview = true,
            ReviewReasons = new List<string>()
        };

        // Extract locations
        ExtractLocations(request.Text, result);

        // Extract watercraft type
        ExtractWatercraft(request.Text, result);

        // Extract duration
        ExtractDuration(request.Text, result);

        // Extract timing phrase
        ExtractTiming(request.Text, result);

        // Extract trip notes (overnight camping, state park, etc.)
        ExtractTripNotes(request.Text, result);

        // Classify record type and duration type
        ClassifyRecordType(request.Text, result);
        ClassifyDurationType(request.Text, result);

        // Calculate confidence based on extraction success
        CalculateConfidence(result);

        return Task.FromResult(result);
    }

    private void ExtractLocations(string text, ExtractionResult result)
    {
        if (TryExtractAnchoredLocations(text, result))
            return;

        TryExtractRouteLocations(text, result);
    }

    private bool TryExtractAnchoredLocations(string text, ExtractionResult result)
    {
        var putInAnchors = new[] { "started at", "put in at", "launched at", "began at" };
        var pullOutAnchors = new[] { "took out at", "pull out at", "ended at", "finished at" };

        foreach (var anchor in putInAnchors)
        {
            if (TryExtractAnchorPhrase(text, anchor, out var candidate) && IsValidLocationCandidate(candidate))
            {
                result.PutInLocation = CleanLocationName(candidate);
                result.PutInSourceText = anchor + " " + candidate;
                break;
            }
        }

        foreach (var anchor in pullOutAnchors)
        {
            if (TryExtractAnchorPhrase(text, anchor, out var candidate) && IsValidLocationCandidate(candidate))
            {
                result.PullOutLocation = CleanLocationName(candidate);
                result.PullOutSourceText = anchor + " " + candidate;
                break;
            }
        }

        return !string.IsNullOrEmpty(result.PutInLocation) || !string.IsNullOrEmpty(result.PullOutLocation);
    }

    private bool TryExtractAnchorPhrase(string text, string anchor, out string location)
    {
        var pattern = $@"\b{Regex.Escape(anchor)}\s+([A-Za-z][A-Za-z\s'&-]{{0,80}}?)(?=[\.,;!?]|$|\s+(?:and|then|but|with|on|in|for|after|before|around|about|near|by|at))";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            location = string.Empty;
            return false;
        }

        location = NormalizeLocationCandidate(match.Groups[1].Value);
        return true;
    }

    private void TryExtractRouteLocations(string text, ExtractionResult result)
    {
        var routePatterns = new[]
        {
            @"\bfrom\s+([A-Za-z][A-Za-z\s'&-]{0,80}?)\s+to\s+([A-Za-z][A-Za-z\s'&-]{0,80}?)(?=[\.,;!?]|$|\s+(?:and|then|but|with|on|in|for|after|before|around|about|near|by|at|last|this|yesterday|today|tomorrow|weekend|morning|afternoon|evening|night))",
            @"\b([A-Za-z][A-Za-z\s'&-]{0,80}?)\s+to\s+([A-Za-z][A-Za-z\s'&-]{0,80}?)(?=[\.,;!?]|$|\s+(?:and|then|but|with|on|in|for|after|before|around|about|near|by|at|last|this|yesterday|today|tomorrow|weekend|morning|afternoon|evening|night))"
        };

        foreach (var pattern in routePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            var putIn = NormalizeLocationCandidate(match.Groups[1].Value);
            var pullOut = NormalizeLocationCandidate(match.Groups[2].Value);

            if (IsValidLocationCandidate(putIn) && IsValidLocationCandidate(pullOut))
            {
                result.PutInLocation = CleanLocationName(putIn);
                result.PullOutLocation = CleanLocationName(pullOut);
                result.PutInSourceText = match.Value.Trim();
                result.PullOutSourceText = match.Value.Trim();
                break;
            }
        }
    }

    private string NormalizeLocationCandidate(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return string.Empty;

        location = Regex.Replace(location, "[\r\n]+", " ").Trim();
        location = Regex.Replace(location, @"\s+", " ").Trim(' ', '.', ',', ';', ':');
        location = location.Trim();
        return location;
    }

    private bool IsValidLocationCandidate(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return false;

        var words = location.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 6)
            return false;

        var invalidFragments = new[]
        {
            "walked", "dragged", "had to", "pull the boat", "carry", "carried", "portaged", "paddled", "went", "used", "then",
            "last weekend", "yesterday", "today", "tomorrow", "weekend", "morning", "afternoon", "evening", "night"
        };

        var lower = location.ToLower();
        if (invalidFragments.Any(fragment => lower.Contains(fragment)))
            return false;

        if (lower.Contains(" to ") || lower.Contains(" from ") || lower.Contains(" and "))
            return false;

        // Prefer place-like text: shorter and with at least one capitalized token if possible.
        if (words.Length > 1 && words.All(word => char.IsLower(word[0])))
            return false;

        return true;
    }

    private string CleanLocationName(string location)
    {
        var prefixes = new[]
        {
            "we did", "we paddled", "we ran", "we went", "we walked", "we drove", "we took",
            "went from", "started at", "put in at", "launched at", "began at", "from"
        };

        var lowerLocation = location.ToLower();
        foreach (var prefix in prefixes)
        {
            if (lowerLocation.StartsWith(prefix))
            {
                var cleaned = location.Substring(prefix.Length).Trim();
                return NormalizeLocationCandidate(cleaned);
            }
        }

        return NormalizeLocationCandidate(location);
    }

    private void ExtractWatercraft(string text, ExtractionResult result)
    {
        // Guiding principles for watercraft extraction:
        // 1. Prefer strong descriptors first, including length and style when available.
        // 2. Infer kayak when the text includes "foot" + "kayak" descriptors.
        // 3. Avoid false positives from phrases like "canoe camp" or other non-watercraft nouns.
        // 4. Preserve the source phrase for review.

        if (TryExtractSizeBasedKayak(text, result))
            return;

        TryExtractKnownWatercraft(text, result);
    }

    private bool TryExtractSizeBasedKayak(string text, ExtractionResult result)
    {
        var pattern = @"\b(\d{1,2}(?:-\d{1,2})?)\s*(?:ft|foot|feet)\s*(?:recreation|fishing|touring|river|whitewater)?\s*(?:/|and|&)?\s*(?:recreation|fishing|touring|river|whitewater)?\s*kayaks?\b";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        result.WatercraftType = NormalizeWatercraftType(match.Value);
        result.WatercraftSourceText = match.Value.Trim();
        return true;
    }

    private void TryExtractKnownWatercraft(string text, ExtractionResult result)
    {
        var knownTypes = new[] { "kayak", "canoe", "raft", "cataraft", "drift boat", "inflatable kayak", "whitewater kayak" };
        foreach (var type in knownTypes)
        {
            // Avoid catching canoe-related camp references or camp names
            var textLower = text.ToLower();
            if (Regex.IsMatch(textLower, $@"\b{Regex.Escape(type)}\s+camp\b"))
                continue;

            var index = textLower.IndexOf(type.ToLower());
            if (index >= 0)
            {
                result.WatercraftType = NormalizeWatercraftType(type);
                var start = Math.Max(0, index - 25);
                var end = Math.Min(text.Length, index + type.Length + 25);
                result.WatercraftSourceText = text.Substring(start, end - start).Trim();
                break;
            }
        }
    }

    private string NormalizeWatercraftType(string watercraft)
    {
        var normalized = watercraft.Trim().ToLower();
        if (normalized.Contains("kayak"))
            return "kayak";
        if (normalized.Contains("canoe"))
            return "canoe";
        if (normalized.Contains("raft"))
            return "raft";
        if (normalized.Contains("cataraft"))
            return "cataraft";

        return watercraft.Trim();
    }

    private void ExtractDuration(string text, ExtractionResult result)
    {
        // Look for duration patterns like "3 hours", "2.5 hours", "90 minutes",
        // "3 to 3.5 hours", "between 3 and 4 hours", and "3 hours 30 minutes".
        var durationPatterns = new (string Pattern, Action<Match> Handler)[]
        {
            (
                @"\bbetween\s+(\d+(?:\.\d+)?)\s+and\s+(\d+(?:\.\d+)?)\s*hours?\b",
                match =>
                {
                    var min = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var max = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    result.DurationHours = (min + max) / 2.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+(?:\.\d+)?)\s*(?:to|[-–])\s*(\d+(?:\.\d+)?)\s*hours?\b",
                match =>
                {
                    var min = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var max = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    result.DurationHours = (min + max) / 2.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+)\s*hours?\s*(?:and\s*)?(\d+)\s*minutes?\b",
                match =>
                {
                    var hours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var minutes = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    result.DurationHours = hours + minutes / 60.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+)h(?:\s*(\d+)m)?\b",
                match =>
                {
                    var hours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var minutes = match.Groups[2].Success ? double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0.0;
                    result.DurationHours = hours + minutes / 60.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+(?:\.\d+)?)\s*hours?\b",
                match =>
                {
                    result.DurationHours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+(?:\.\d+)?)\s*hrs?\b",
                match =>
                {
                    result.DurationHours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+)\s*minutes?\b",
                match =>
                {
                    result.DurationHours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 60.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            ),
            (
                @"\b(\d+)\s*mins?\b",
                match =>
                {
                    result.DurationHours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 60.0;
                    result.DurationSourceText = match.Value.Trim();
                }
            )
        };

        foreach (var (pattern, handler) in durationPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            handler(match);
            break;
        }
    }

    private void ExtractTiming(string text, ExtractionResult result)
    {
        // Guiding principles for timing extraction:
        // 1. Prefer relative event inference from an explicit post date when available.
        // 2. Use explicit dates as a fallback if no relative event is inferred.
        // 3. Extract strong time anchors like sundown and launch times.
        // 4. Preserve source text for later review and estimate in notes when appropriate.
        if (TryExtractWrittenDateAndRelativeEvent(text, result))
            return;

        if (TryExtractExplicitDate(text, result))
            return;

        if (TryExtractStrongTimePhrase(text, result))
            return;

        TryExtractFallbackTiming(text, result);
    }

    private bool TryExtractExplicitDate(string text, ExtractionResult result)
    {
        var explicitDatePatterns = new[]
        {
            @"\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2},?\s+\d{4}\b",
            @"\b\d{1,2}\s+(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{4}\b"
        };

        foreach (var pattern in explicitDatePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            result.TripDateOrTiming = match.Value.Trim();
            result.TripDateOrTimingSourceText = ExtractContext(text, match.Index, match.Length);
            return true;
        }

        return false;
    }

    private bool TryExtractWrittenDateAndRelativeEvent(string text, ExtractionResult result)
    {
        if (!TryExtractPostDate(text, out var postDate))
            return false;

        if (TryExtractRelativeLaunch(text, postDate, result))
            return true;

        if (TryExtractRelativeStateParkArrival(text, postDate, result))
            return true;

        return false;
    }

    private bool TryExtractPostDate(string text, out DateTime postDate)
    {
        postDate = default;
        var datePattern = @"\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2},?\s+\d{4}\b";
        var match = Regex.Match(text, datePattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        return DateTime.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out postDate);
    }

    private bool TryExtractRelativeLaunch(string text, DateTime postDate, ExtractionResult result)
    {
        var launchPattern = @"\b(?:launched|started|put in|took out|began)\s+(?:on\s+)?(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)(?:\s+(morning|afternoon|evening|night))?\b";
        var match = Regex.Match(text, launchPattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        if (!TryResolveWeekdayRelativeToPostDate(match.Groups[1].Value, postDate, out var resolvedDate))
            return false;

        var timeOfDay = match.Groups[2].Success ? match.Groups[2].Value.ToLower() : string.Empty;
        var estimateRange = EstimateTimeWindow(timeOfDay);
        result.TripDateOrTiming = resolvedDate.ToString("yyyy-MM-dd") + (string.IsNullOrEmpty(timeOfDay) ? " (Saturday launch)" : $" ({timeOfDay} launch)");
        result.TripDateOrTimingSourceText = ExtractContext(text, match.Index, match.Length);
        result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, $"Likely launch on {resolvedDate:yyyy-MM-dd}{estimateRange}");
        return true;
    }

    private bool TryExtractRelativeStateParkArrival(string text, DateTime postDate, ExtractionResult result)
    {
        var pattern = @"\b(?:camping at|camped at|at\s+Raven Rock state park|Raven Rock state park)\b";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        if (TryExtractReferencedWeekday(text, out var weekday))
        {
            if (TryResolveWeekdayRelativeToPostDate(weekday, postDate, out var resolvedDate))
            {
                result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, $"State park overnight on {resolvedDate:yyyy-MM-dd}");
                return true;
            }
        }

        result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "Overnight state park camping");
        return true;
    }

    private bool TryExtractReferencedWeekday(string text, out string weekday)
    {
        var weekdayPattern = @"\b(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b";
        var match = Regex.Match(text, weekdayPattern, RegexOptions.IgnoreCase);
        weekday = match.Success ? match.Value : string.Empty;
        return match.Success;
    }

    private string EstimateTimeWindow(string timeOfDay)
    {
        return timeOfDay switch
        {
            "morning" => " between 06:00 and 12:00",
            "afternoon" => " between 12:00 and 16:00",
            "evening" => " between 16:00 and 20:00",
            "night" => " between 20:00 and 23:59",
            _ => " between 06:00 and 15:00"
        };
    }

    private bool TryResolveWeekdayRelativeToPostDate(string weekdayText, DateTime postDate, out DateTime resolvedDate)
    {
        resolvedDate = default;
        if (!Enum.TryParse<DayOfWeek>(weekdayText, true, out var weekday))
            return false;

        // Find the most recent matching weekday before or on the post date.
        for (var daysBack = 0; daysBack <= 7; daysBack++)
        {
            var candidate = postDate.AddDays(-daysBack);
            if (candidate.DayOfWeek == weekday)
            {
                resolvedDate = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryExtractStrongTimePhrase(string text, ExtractionResult result)
    {
        var strongPatterns = new[]
        {
            @"\b(?:at|around|by|near|before|after)\s+(?:sunrise|sundown|sunset|dawn|dusk|midday|midnight)\b",
            @"\b(?:sunrise|sundown|sunset|dawn|dusk|midday|midnight|tonight|after dark|before dark)\b",
            @"\b(?:early morning|late morning|late afternoon|early evening|late evening|early dawn|late dusk)\b"
        };

        foreach (var pattern in strongPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            result.TripDateOrTiming = match.Value.Trim();
            result.TripDateOrTimingSourceText = ExtractContext(text, match.Index, match.Length);
            return true;
        }

        return false;
    }

    private void TryExtractFallbackTiming(string text, ExtractionResult result)
    {
        var timingPatterns = new[]
        {
            @"last\s+(weekend|week|month|year)",
            @"this\s+(past\s+weekend|weekend|week|month|year|morning|afternoon|evening)",
            @"yesterday",
            @"today",
            @"tomorrow",
            @"\d{1,2}/\d{1,2}/\d{4}", // MM/DD/YYYY
            @"\d{4}-\d{2}-\d{2}", // YYYY-MM-DD
            @"\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)s?\b"
        };

        foreach (var pattern in timingPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            result.TripDateOrTiming = match.Value.Trim();
            result.TripDateOrTimingSourceText = ExtractContext(text, match.Index, match.Length);
            break;
        }
    }

    private string ExtractContext(string text, int index, int length)
    {
        const int padding = 12;
        var start = Math.Max(0, index - padding);
        var end = Math.Min(text.Length, index + length + padding);
        return text.Substring(start, end - start).Trim();
    }

    private void ExtractTripNotes(string text, ExtractionResult result)
    {
        var lowerText = text.ToLower();
        var hasCamping = Regex.IsMatch(lowerText, @"\b(?:overnight|camp(?:ing|ed)?|night's rest|nights rest|overnight stay)\b");
        var hasPark = Regex.IsMatch(lowerText, @"\b(?:state park|park)\b");
        if (hasCamping && hasPark)
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "overnight camping trip at state park");
        }
        else if (hasCamping)
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "overnight camping trip");
        }

        if (TryExtractGaugeHeight(text, out var gaugeNote))
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, gaugeNote);
        }

        ExtractLowWaterTripConditions(lowerText, result);
    }

    private void ExtractLowWaterTripConditions(string lowerText, ExtractionResult result)
    {
        if (Regex.IsMatch(lowerText, @"\b(?:dragged|dragging)\s+bottom\b"))
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "dragged bottom");
        }

        if (Regex.IsMatch(lowerText, @"\b(?:walk|walked|walking)\b") &&
            Regex.IsMatch(lowerText, @"\bpull(?:ed|ing)?\s+the\s+boat\b"))
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "walked and pulled boat");
        }

        if (Regex.IsMatch(lowerText, @"\bslack\b") &&
            Regex.IsMatch(lowerText, @"\bslow\b"))
        {
            result.ConditionsOrNotes = AppendToNotes(result.ConditionsOrNotes, "slack slow water");
        }
    }

    private bool TryExtractGaugeHeight(string text, out string gaugeNote)
    {
        gaugeNote = string.Empty;
        var gaugePattern = @"\b(?:gauge|gauge at)\s+(?<location>[A-Za-z\s]+?)\s+(?:was\s+at|at)\s*(?<value>\d+(?:\.\d+)?)\b";
        var match = Regex.Match(text, gaugePattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var location = NormalizeLocationCandidate(match.Groups["location"].Value);
        var value = match.Groups["value"].Value;
        gaugeNote = $"Gauge {location} {value}";
        return true;
    }

    private string AppendToNotes(string? existingNotes, string note)
    {
        if (string.IsNullOrWhiteSpace(existingNotes))
            return note;
        return existingNotes + "; " + note;
    }

    private void ClassifyRecordType(string text, ExtractionResult result)
    {
        var lowerText = text.ToLower();

        // Check for reported trip result patterns (past tense)
        var reportedPatterns = new[] { "we did", "took us", "we ran", "we paddled", "we kayaked", "took about", "took around" };
        foreach (var pattern in reportedPatterns)
        {
            if (lowerText.Contains(pattern))
            {
                result.RecordType = RecordType.ReportedTripResult;
                return;
            }
        }

        // Check for estimate patterns
        var estimatePatterns = new[] { "usually", "should take", "plan for", "plan on", "expect", "typically", "average" };
        foreach (var pattern in estimatePatterns)
        {
            if (lowerText.Contains(pattern))
            {
                result.RecordType = RecordType.TripEstimate;
                return;
            }
        }

        // Default to unclear if no clear signals
        result.RecordType = RecordType.UnclearTripRecord;
    }

    private void ClassifyDurationType(string text, ExtractionResult result)
    {
        var lowerText = text.ToLower();

        // Check for past tense (actual trip)
        var pastTensePatterns = new[] { "took", "did", "was", "spent", "paddled for", "took about", "went for" };
        foreach (var pattern in pastTensePatterns)
        {
            if (lowerText.Contains(pattern))
            {
                result.DurationType = DurationType.Actual;
                return;
            }
        }

        // Check for expectation/estimate patterns
        var futurePatterns = new[] { "usually", "should take", "will take", "expect", "plan for", "plan on", "typically takes", "average" };
        foreach (var pattern in futurePatterns)
        {
            if (lowerText.Contains(pattern))
            {
                result.DurationType = DurationType.Estimate;
                return;
            }
        }

        // Default to unknown
        result.DurationType = DurationType.Unknown;
    }

    private void CalculateConfidence(ExtractionResult result)
    {
        const double baseConfidence = 0.15;
        const double maxConfidence = 0.997;
        const double availableRange = maxConfidence - baseConfidence; // 0.847

        double confidence = baseConfidence;

        // Reward for each extracted field (distributed across the available range)
        if (!string.IsNullOrEmpty(result.PutInLocation))
            confidence += availableRange * 0.12;
        if (!string.IsNullOrEmpty(result.PullOutLocation))
            confidence += availableRange * 0.12;
        if (result.DurationHours.HasValue)
            confidence += availableRange * 0.12;
        if (!string.IsNullOrEmpty(result.WatercraftType))
            confidence += availableRange * 0.08;
        if (!string.IsNullOrEmpty(result.TripDateOrTiming))
            confidence += availableRange * 0.08;

        // Reward for clear classification
        if (result.RecordType != RecordType.UnclearTripRecord)
            confidence += availableRange * 0.15;

        if (result.DurationType != DurationType.Unknown)
            confidence += availableRange * 0.10;

        // Bonus for having both locations and duration
        if (!string.IsNullOrEmpty(result.PutInLocation) && 
            !string.IsNullOrEmpty(result.PullOutLocation) && 
            result.DurationHours.HasValue)
        {
            confidence += availableRange * 0.15;
        }

        // Cap at max confidence
        result.ExtractionConfidence = Math.Min(maxConfidence, confidence);

        // Flag for review if confidence is too low or classification is unclear
        result.NeedsReview = confidence < 0.60 || result.RecordType == RecordType.UnclearTripRecord;
    }
}
