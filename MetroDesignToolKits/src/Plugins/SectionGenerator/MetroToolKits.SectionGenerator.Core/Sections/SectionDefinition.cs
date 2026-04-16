using SectionGenerator.Core.Geometry;

namespace SectionGenerator.Core.Sections;

public sealed record SectionDefinition(
    Vec2 Origin,
    Vec2 Direction,
    double SampleStep
);

