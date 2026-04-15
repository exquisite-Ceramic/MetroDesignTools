namespace SectionGenerator.Core.Sections;

public interface ISectionGenerator
{
    SectionResult Generate(SectionDefinition def);
}

