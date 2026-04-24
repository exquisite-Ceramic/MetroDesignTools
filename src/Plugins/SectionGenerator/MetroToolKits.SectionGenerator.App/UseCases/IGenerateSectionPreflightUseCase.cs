using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.UseCases;

public interface IGenerateSectionPreflightUseCase
{
    GenerateSectionPreflightResult Execute();
}

public sealed class GenerateSectionPreflightResult
{
    public LoadedSectionConfig ConfigDocument { get; init; } = new();

    public IReadOnlyList<OperationDiagnostic> MissingRequirements { get; init; } = Array.Empty<OperationDiagnostic>();

    public bool CanGenerate => MissingRequirements.Count == 0;

    public ExistingSectionStatusSummary ExistingSections { get; init; } = new();

    public GenerationReadinessSummary Readiness { get; init; } = new();
}

public sealed class ExistingSectionStatusSummary
{
    public int TotalCount { get; init; }

    public int UpToDateCount { get; init; }

    public int OutdatedCount { get; init; }

    public int UnknownCount { get; init; }

    public int PartialCount { get; init; }

    public CheckSectionUpdatesResult? CheckResult { get; init; }

    public string? ErrorMessage { get; init; }
}
