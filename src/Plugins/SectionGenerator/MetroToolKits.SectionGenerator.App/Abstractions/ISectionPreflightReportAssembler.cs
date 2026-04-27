using MetroToolKits.SectionGenerator.Contracts.Preflight;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ISectionPreflightReportAssembler
{
    SectionPreflightReportDto Assemble(GenerateSectionPreflightResult result);
}
