using MetroToolKits.SectionGenerator.Contracts.Workbench;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IWorkbenchSnapshotAssembler
{
    SectionWorkbenchSnapshotDto Assemble(GenerateSectionPreflightResult result);
}
