using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.Foundation.Core.Runtime;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Infrastructure.Recognition;
using MetroToolKits.SectionGenerator.Infrastructure.Repositories;
using MetroToolKits.SectionGenerator.Infrastructure.Services;
using MetroToolKits.SectionGenerator.Plugin.Commands;
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
        var userDataDir = GetUserDataDirectory();

        // Foundation 服务
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<ILayerService, LayerService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IEditorService, EditorService>();

        // 构件类型服务
        var elementTypesPath = Path.Combine(userDataDir, "ElementTypes.json");
        var elementTypesTemplatePath = Path.Combine(assemblyDir, "ElementTypes.json");
        services.AddSingleton<IElementTypeCatalog>(sp =>
            new JsonElementTypeCatalog(
                elementTypesPath,
                elementTypesTemplatePath,
                sp.GetRequiredService<ILogger<JsonElementTypeCatalog>>()));

        // 备份服务
        services.AddSingleton<ElementConversionBackupService>();
        services.AddSingleton<IElementConversionService, CadElementConversionService>();

        // 楼层配置仓储（DWG 内嵌配置）
        services.AddSingleton<IFloorConfigRepository, DwgFloorConfigRepository>();

        // 构件识别器
        services.AddSingleton<IElementRecognizer, LayerBasedElementRecognizer>();
        services.AddSingleton<ISectionLineResolver, CadSectionLineResolver>();

        // Core 层
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.SectionComposer>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.MultiFloorSectionComposer>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>();
        services.AddSingleton<OperationFeedbackPresenter>();

        // 快照仓储 + 块删除服务
        services.AddSingleton<ISectionSnapshotRepository, XDataSnapshotRepository>();
        services.AddSingleton<ISectionBlockQueryService, XDataSnapshotRepository>();
        services.AddSingleton<ISectionGeometryRecoveryService, XDataSnapshotRepository>();
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
        services.AddSingleton<IFloorConfigUseCase, FloorConfigUseCase>();
        services.AddSingleton<IElementConversionUseCase, ElementConversionUseCase>();

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
        services.AddTransient<SectionGeneratorHostAcceptanceCommand>();
        services.AddTransient<OpenLayerMappingCommand>();
    }

    public void RegisterCommands(ICommandRegistry registry)
        => CommandRegistrationScanner.RegisterAttributedCommands(registry, typeof(SectionGeneratorPlugin).Assembly);

    private static string GetUserDataDirectory()
        => RuntimePathResolver.GetPackageScopedUserPath(
            typeof(SectionGeneratorPlugin).Assembly.Location,
            "SectionGenerator");
}

/// <summary>
/// 打开图层映射管理器命令
/// </summary>
[CommandBinding(SectionGeneratorCommandNames.LayerMapping)]
public class OpenLayerMappingCommand
{
    private readonly ILayerService _layerService;
    private readonly IElementTypeCatalog _typeCatalog;
    private readonly IElementConversionUseCase _elementConversionUseCase;

    public OpenLayerMappingCommand(
        ILayerService layerService,
        IElementTypeCatalog typeCatalog,
        IElementConversionUseCase elementConversionUseCase)
    {
        _layerService  = layerService;
        _typeCatalog = typeCatalog;
        _elementConversionUseCase = elementConversionUseCase;
    }

    public void Execute()
    {
        var window = new UI.LayerMappingManager(_layerService, _typeCatalog, _elementConversionUseCase);
        Application.ShowModalWindow(window);
    }
}
