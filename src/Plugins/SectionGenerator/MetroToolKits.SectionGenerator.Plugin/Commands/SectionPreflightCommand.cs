using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Plugin.UI;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin.Commands;

[CommandBinding(SectionGeneratorCommandNames.SectionPreflight)]
[CommandBinding(SectionGeneratorCommandNames.CheckBeforeGenerate, nameof(ExecuteAlias))]
public sealed class SectionPreflightCommand
{
    private readonly IGenerateSectionPreflightUseCase _preflightUseCase;
    private readonly ISectionPreflightReportAssembler _reportAssembler;
    private readonly FloorConfigCommand _floorConfigCommand;
    private readonly OpenLayerMappingCommand _openLayerMappingCommand;
    private readonly ILogger<SectionPreflightCommand> _logger;

    public SectionPreflightCommand(
        IGenerateSectionPreflightUseCase preflightUseCase,
        ISectionPreflightReportAssembler reportAssembler,
        FloorConfigCommand floorConfigCommand,
        OpenLayerMappingCommand openLayerMappingCommand,
        ILogger<SectionPreflightCommand> logger)
    {
        _preflightUseCase = preflightUseCase;
        _reportAssembler = reportAssembler;
        _floorConfigCommand = floorConfigCommand;
        _openLayerMappingCommand = openLayerMappingCommand;
        _logger = logger;
    }

    public void Execute() => RunDialogLoop();

    public void ExecuteAlias() => RunDialogLoop();

    private void RunDialogLoop()
    {
        if (Application.DocumentManager.MdiActiveDocument == null)
        {
            return;
        }

        while (true)
        {
            var report = _reportAssembler.Assemble(_preflightUseCase.Execute());
            var window = new SectionPreflightWindow(
                report,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionPreflightWindow>.Instance);

            _logger.LogDebug(
                "打开生成前检查窗口，CanGenerate={CanGenerate}, Blocking={Blocking}, Warning={Warning}",
                report.CanGenerate,
                report.BlockingCount,
                report.WarningCount);

            Application.ShowModalWindow(window);
            if (window.Tag is not SectionPreflightWindowAction action)
            {
                break;
            }

            switch (action)
            {
                case SectionPreflightWindowAction.Refresh:
                    continue;

                case SectionPreflightWindowAction.OpenFloorConfig:
                    _floorConfigCommand.Execute();
                    continue;

                case SectionPreflightWindowAction.OpenLayerMapping:
                    _openLayerMappingCommand.Execute();
                    continue;

                default:
                    break;
            }

            break;
        }
    }
}
