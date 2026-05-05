using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class ProvisionalAccessChoiceSignalService : IProvisionalAccessChoiceSignalService
{
    private static readonly DateTime SeedSelectedAtUtc = new(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<ProvisionalAccessChoiceSignal> SeedChoiceSignals = new List<ProvisionalAccessChoiceSignal>
    {
        new()
        {
            Id = new Guid("c1000000-0000-0000-0000-000000000001"),
            RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000002"),
            PossibleAccessKey = "raw-candidate:richardson-bridge-boat-ramp",
            SignalSource = ProvisionalAccessChoiceSignalSource.StewardSelected,
            SelectedAtUtc = SeedSelectedAtUtc,
            SourceName = "Internal steward review",
            SourceReference = "provisional-access-choice-signal-v1",
            Note = "Selection signal only; does not verify or promote the raw candidate."
        },
        new()
        {
            Id = new Guid("c1000000-0000-0000-0000-000000000002"),
            RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000008"),
            PossibleAccessKey = "possible-access:anderson-point-park",
            SignalSource = ProvisionalAccessChoiceSignalSource.UserSelected,
            SelectedAtUtc = SeedSelectedAtUtc,
            SourceName = "User planning selection",
            SourceReference = "seeded-choice-signal",
            Note = "Practical selection signal retained separately from review state and identifiers."
        }
    };

    private readonly List<ProvisionalAccessChoiceSignal> _choiceSignals;

    public ProvisionalAccessChoiceSignalService()
    {
        _choiceSignals = SeedChoiceSignals.Select(CloneSignal).ToList();
    }

    public IReadOnlyList<ProvisionalAccessChoiceSignal> GetProvisionalAccessChoiceSignals()
    {
        return _choiceSignals
            .Select(CloneSignal)
            .ToList();
    }

    public ProvisionalAccessChoiceSignal RecordChoiceSignal(ProvisionalAccessChoiceSignal signal)
    {
        if (signal.RawAccessPointCandidateId == null && string.IsNullOrWhiteSpace(signal.PossibleAccessKey))
            throw new ArgumentException("A choice signal must reference a raw candidate or possible access key.", nameof(signal));

        if (string.IsNullOrWhiteSpace(signal.SourceName))
            throw new ArgumentException("A choice signal source name is required.", nameof(signal));

        if (signal.SelectedAtUtc == default)
            throw new ArgumentException("A choice signal selection timestamp is required.", nameof(signal));

        var stored = CloneSignal(signal);
        stored.Id = stored.Id == Guid.Empty ? Guid.NewGuid() : stored.Id;
        stored.PossibleAccessKey = string.IsNullOrWhiteSpace(stored.PossibleAccessKey)
            ? null
            : stored.PossibleAccessKey.Trim();
        stored.SourceName = stored.SourceName.Trim();
        stored.SourceReference = string.IsNullOrWhiteSpace(stored.SourceReference)
            ? null
            : stored.SourceReference.Trim();
        stored.Note = string.IsNullOrWhiteSpace(stored.Note)
            ? null
            : stored.Note.Trim();
        _choiceSignals.Add(stored);

        return CloneSignal(stored);
    }

    private static ProvisionalAccessChoiceSignal CloneSignal(ProvisionalAccessChoiceSignal signal)
    {
        return new ProvisionalAccessChoiceSignal
        {
            Id = signal.Id,
            RawAccessPointCandidateId = signal.RawAccessPointCandidateId,
            PossibleAccessKey = signal.PossibleAccessKey,
            SignalSource = signal.SignalSource,
            SelectedAtUtc = signal.SelectedAtUtc,
            SourceName = signal.SourceName,
            SourceReference = signal.SourceReference,
            Note = signal.Note
        };
    }
}
