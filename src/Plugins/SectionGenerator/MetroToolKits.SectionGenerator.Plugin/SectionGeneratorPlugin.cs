using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Infrastructure.Recognition;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;
using MetroToolKits.SectionGenerator.Infrastructure.Services;
using MetroToolKits.SectionGenerator.Plugin.Commands;
using MetroToolKits.SectionGenerator.Plugin.Services;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Plugin;

/// <summary>
/// SectionGenerator 插件入口
/// </summary>
public class SectionGeneratorPlugin : IPlugin
{
    public string Name    => "SectionGenerator";
    public string Version =>
        typeof(SectionGeneratorPlugin).Assembly
            .GetName().Version?.ToString(3) ?? "1.1.0";

    public void ConfigureServices(IServiceCollection services)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(SectionGeneratorPlugin).Assembly.Location) ?? "";

        // Foundation 服务
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<ILayerService, LayerService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IEditorService, EditorService>();

        // 构件类型服务
        var elementTypesPath = Path.Combine(assemblyDir, "ElementTypes.json");
        services.AddSingleton(new ElementTypeLoader(elementTypesPath));
        services.AddSingleton<ElementTypeService>();

        // 备份服务
        services.AddSingleton<ElementConversionBackupService>();

        // 楼层配置仓储
        var configPath = Path.Combine(assemblyDir, "SectionGeneratorConfig.json");
        services.AddSingleton<IFloorConfigRepository>(sp =>
            new JsonFloorConfigRepository(configPath,
                sp.GetRequiredService<ILogger<JsonFloorConfigRepository>>()));

        // 构件识别器
        services.AddSingleton<IElementRecognizer, LayerBasedElementRecognizer>();

        // Core 层
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.SectionComposer>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.MultiFloorSectionComposer>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>();

        // 快照仓储 + 块删除服务
        services.AddSingleton<ISectionSnapshotRepository, XDataSnapshotRepository>();
        services.AddSingleton<ISectionReferenceRepository, XDataSectionReferenceRepository>();
        services.AddSingleton<IBlockEraseService, CadBlockEraseService>();
        services.AddSingleton<IEntityNavigationService, CadNavigationService>();

        // 绘图服务（已从 CadAdapter 合并至 Infrastructure）
        services.AddSingleton<IDrawingService, CadDrawingService>();

        // 用例
        services.AddSingleton<IGenerateSectionUseCase, GenerateSectionUseCase>();
        services.AddSingleton<ICheckSectionUpdatesUseCase, CheckSectionUpdatesUseCase>();
        services.AddSingleton<IUpdateSectionUseCase, UpdateSectionUseCase>();
        services.AddSingleton<ILocateSourceElementUseCase, LocateSourceElementUseCase>();
        services.AddSingleton<IFindRelatedSectionsUseCase, FindRelatedSectionsUseCase>();

        // UI/工具箱
        services.AddSingleton<UI.SectionToolboxPaletteService>();

        // 命令
        services.AddTransient<GenSectionCommand>();
        services.AddTransient<FloorConfigCommand>();
        services.AddTransient<CheckSectionUpdatesCommand>();
        services.AddTransient<UpdateSectionCommand>();
        services.AddTransient<LocateSourceElementCommand>();
        services.AddTransient<FindRelatedSectionsCommand>();
        services.AddTransient<ShowToolboxCommand>();
        services.AddTransient<ConvertRegionElementsCommand>();
        services.AddTransient<RevertElementConversionCommand>();
        services.AddTransient<SectionGeneratorSelfTestCommand>();
        services.AddTransient<OpenLayerMappingCommand>();
    }

    public void RegisterCommands(ICommandRegistry registry)
    {
        registry.RegisterCommand<GenSectionCommand>("GenSection");
        registry.RegisterCommand<FloorConfigCommand>("FloorConfig");
        registry.RegisterCommand<CheckSectionUpdatesCommand>("CheckSectionUpdates");
        registry.RegisterCommand<UpdateSectionCommand>("UpdateSection");
        registry.RegisterCommand<LocateSourceElementCommand>("LocateSourceElement");
        registry.RegisterCommand<FindRelatedSectionsCommand>("FindRelatedSections");
        registry.RegisterCommand<ShowToolboxCommand>("ShowToolbox");
        registry.RegisterCommand<ConvertRegionElementsCommand>("ConvertRegion");
        registry.RegisterCommand<RevertElementConversionCommand>("RevertConversion");
        registry.RegisterCommand<RevertElementConversionCommand>("RevertAllConversions");
        registry.RegisterCommand<SectionGeneratorSelfTestCommand>("SectionSelfTest");
        registry.RegisterCommand<OpenLayerMappingCommand>("LayerMapping");
    }
}

/// <summary>
/// 打开图层映射管理器命令
/// </summary>
public class OpenLayerMappingCommand
{
    private readonly ILayerService _layerService;
    private readonly ElementConversionBackupService _backupService;
    private readonly ElementTypeLoader _typeLoader;

    public OpenLayerMappingCommand(
        ILayerService layerService,
        ElementConversionBackupService backupService,
        ElementTypeLoader typeLoader)
    {
        _layerService  = layerService;
        _backupService = backupService;
        _typeLoader    = typeLoader;
    }

    public void Execute()
    {
        var window = new UI.LayerMappingManager(_layerService, _backupService, _typeLoader);
        Application.ShowModalWindow(window);
    }
}
