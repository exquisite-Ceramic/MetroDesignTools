using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;
using System.Text.Json;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionSnapshotTests
{
    // ── FloorSnapshot ─────────────────────────────────────────────────────────

    [Fact]
    public void FloorSnapshot_DefaultValues_AreEmpty()
    {
        var snap = new FloorSnapshot();
        snap.FloorName.Should().BeEmpty();
        snap.GeometryHash.Should().BeEmpty();
        snap.ElementCount.Should().Be(0);
        snap.SourceElementHandles.Should().NotBeNull();
        snap.SourceElementHandles.Should().BeEmpty();
        snap.SightLineGeometryHash.Should().BeNull();
        snap.SightLineElementCount.Should().BeNull();
        snap.SightLineSourceElementHandles.Should().BeNull();
        snap.SightLineHashVersion.Should().BeNull();
    }

    [Fact]
    public void FloorSnapshot_SetValues_RetainCorrectly()
    {
        var snap = new FloorSnapshot
        {
            FloorName    = "F1",
            GeometryHash = "abc123",
            ElementCount = 42,
            SourceElementHandles = new List<string> { "1A2B" }
        };
        snap.FloorName.Should().Be("F1");
        snap.GeometryHash.Should().Be("abc123");
        snap.ElementCount.Should().Be(42);
        snap.SourceElementHandles.Should().ContainSingle().Which.Should().Be("1A2B");
    }

    [Fact]
    public void FloorSnapshot_LegacyJsonMissingSightLineFields_DeserializesAsMissing()
    {
        const string json = """
        {
          "FloorName": "F1",
          "GeometryHash": "cut-hash",
          "ElementCount": 1,
          "SourceElementHandles": [ "CUT" ]
        }
        """;

        var snap = JsonSerializer.Deserialize<FloorSnapshot>(json);

        snap.Should().NotBeNull();
        snap!.FloorName.Should().Be("F1");
        snap.SightLineGeometryHash.Should().BeNull();
        snap.SightLineElementCount.Should().BeNull();
        snap.SightLineSourceElementHandles.Should().BeNull();
        snap.SightLineHashVersion.Should().BeNull();
    }

    [Fact]
    public void FloorSnapshot_NewSightLineFields_RoundTrip()
    {
        var snap = new FloorSnapshot
        {
            FloorName = "F1",
            GeometryHash = "cut-hash",
            ElementCount = 1,
            SourceElementHandles = new List<string> { "CUT" },
            SightLineGeometryHash = "sight-hash",
            SightLineElementCount = 0,
            SightLineSourceElementHandles = new List<string>(),
            SightLineHashVersion = SightLineGeometryHasher.CurrentHashVersion
        };

        var json = JsonSerializer.Serialize(snap);
        var roundTrip = JsonSerializer.Deserialize<FloorSnapshot>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.SightLineGeometryHash.Should().Be("sight-hash");
        roundTrip.SightLineElementCount.Should().Be(0);
        roundTrip.SightLineSourceElementHandles.Should().NotBeNull();
        roundTrip.SightLineSourceElementHandles.Should().BeEmpty();
        roundTrip.SightLineHashVersion.Should().Be(SightLineGeometryHasher.CurrentHashVersion);
    }

    // ── SectionSnapshot ───────────────────────────────────────────────────────

    [Fact]
    public void SectionSnapshot_DefaultSectionId_IsValidGuid()
    {
        var snap = new SectionSnapshot();
        Guid.TryParse(snap.SectionId, out _).Should().BeTrue("SectionId 应为有效 GUID");
    }

    [Fact]
    public void SectionSnapshot_TwoInstances_HaveDifferentSectionIds()
    {
        var s1 = new SectionSnapshot();
        var s2 = new SectionSnapshot();
        s1.SectionId.Should().NotBe(s2.SectionId);
    }

    [Fact]
    public void SectionSnapshot_FloorSnapshots_DefaultEmpty()
    {
        var snap = new SectionSnapshot();
        snap.FloorSnapshots.Should().NotBeNull();
        snap.FloorSnapshots.Should().BeEmpty();
    }

    [Fact]
    public void SectionSnapshot_StoresCutLinePoints()
    {
        var snap = new SectionSnapshot
        {
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd   = new Point3D(6000, 0, 0)
        };
        snap.CutLineStart.X.Should().Be(0);
        snap.CutLineEnd.X.Should().Be(6000);
    }

    [Fact]
    public void SectionSnapshotFloorScope_PrefersExecutionFloorNames()
    {
        var snap = new SectionSnapshot
        {
            TargetFloorName = null,
            ExecutionFloorNames = new List<string> { "F1", "F2", "F3" },
            GeneratedFloorNames = new List<string> { "F1", "F3" }
        };

        SectionSnapshotFloorScope.ResolveExecutionFloorNames(snap)
            .Should()
            .Equal("F1", "F2", "F3");
    }

    [Fact]
    public void SectionSnapshotFloorScope_LeavesLegacyGlobalSnapshotsUnconstrained()
    {
        var snap = new SectionSnapshot
        {
            TargetFloorName = null,
            GeneratedFloorNames = new List<string> { "F1", "F3" }
        };

        SectionSnapshotFloorScope.ResolveExecutionFloorNames(snap)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void SectionSnapshotFloorScope_FallsBackToGeneratedFloorsForLegacyTargetedSnapshots()
    {
        var snap = new SectionSnapshot
        {
            TargetFloorName = "F3",
            GeneratedFloorNames = new List<string> { "F3" },
            FloorSnapshots = new List<FloorSnapshot> { new() { FloorName = "F2" } }
        };

        SectionSnapshotFloorScope.ResolveExecutionFloorNames(snap)
            .Should()
            .Equal("F3");
    }

    // ── SectionUpdateStatus ───────────────────────────────────────────────────

    [Fact]
    public void SectionCheckResult_DefaultStatus_IsUpToDate()
    {
        var result = new SectionCheckResult();
        // 枚举默认值为 0 = UpToDate
        result.Status.Should().Be(SectionUpdateStatus.UpToDate);
    }

    [Fact]
    public void SectionCheckResult_OutdatedFloors_DefaultEmpty()
    {
        var result = new SectionCheckResult();
        result.OutdatedFloors.Should().NotBeNull();
        result.OutdatedFloors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SectionUpdateStatus.UpToDate)]
    [InlineData(SectionUpdateStatus.Outdated)]
    [InlineData(SectionUpdateStatus.Unknown)]
    public void SectionUpdateStatus_AllValuesExist(SectionUpdateStatus status)
    {
        // 确保枚举值存在
        Enum.IsDefined(typeof(SectionUpdateStatus), status).Should().BeTrue();
    }
}
