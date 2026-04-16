using System.Collections.Generic;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 剖面生成结果
/// </summary>
public sealed record SectionResult(
    Polyline2 SectionPolyline,
    IReadOnlyList<LayerPosition> Layers
);
