namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// Resolves the floor set that must be used when re-checking or updating an existing section snapshot.
/// </summary>
public static class SectionSnapshotFloorScope
{
    public static IReadOnlyList<string> ResolveExecutionFloorNames(SectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.ExecutionFloorNames.Count > 0)
        {
            return snapshot.ExecutionFloorNames;
        }

        // Legacy full-building snapshots did not record the execution floor set.
        // Keep those unconstrained so the current drawing configuration can supply the full stack.
        if (string.IsNullOrWhiteSpace(snapshot.TargetFloorName))
        {
            return Array.Empty<string>();
        }

        return snapshot.GeneratedFloorNames.Count > 0
            ? snapshot.GeneratedFloorNames
            : snapshot.FloorSnapshots.Select(floor => floor.FloorName).ToList();
    }
}
