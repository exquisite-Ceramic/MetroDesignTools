using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Cad.Layering.Services;
using MetroToolKits.Foundation.Core.Hosting;
using MetroToolKits.Foundation.Core.Runtime;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
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
        services.AddSingleton<DocumentService>();
        services.AddSingleton<IDocumentService>(sp => sp.GetRequiredService<DocumentService>());
        services.AddSingleton<ICadDatabaseAccessor>(sp => sp.GetRequiredService<DocumentService>());
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
        var wallTemplatesPath = Path.Combine(userDataDir, "WallAssemblyTemplates.json");
        var wallTemplatesTemplatePath = Path.Combine(assemblyDir, "WallAssemblyTemplates.json");
        services.AddSingleton<IWallAssemblyTemplateCatalog>(sp =>
            new JsonWallAssemblyTemplateCatalog(
                wallTemplatesPath,
                wallTemplatesTemplatePath,
                sp.GetRequiredService<ILogger<JsonWallAssemblyTemplateCatalog>>()));
        var slabTemplatesPath = Path.Combine(userDataDir, "SlabAssemblyTemplates.json");
        var slabTemplatesTemplatePath = Path.Combine(assemblyDir, "SlabAssemblyTemplates.json");
        services.AddSingleton<ISlabAssemblyTemplateCatalog>(sp =>
            new JsonSlabAssemblyTemplateCatalog(
                slabTemplatesPath,
                slabTemplatesTemplatePath,
                sp.GetRequiredService<ILogger<JsonSlabAssemblyTemplateCatalog>>()));

        // 备份服务
        services.AddSingleton<ElementConversionBackupService>();
        services.AddSingleton<IElementConversionService, CadElementConversionService>();
        services.AddSingleton<IGenerationReadinessInspector, GenerationReadinessInspector>();

        // 楼层配置仓储（DWG 内嵌配置）
        services.AddSingleton<IFloorConfigRepository, DwgFloorConfigRepository>();

        // 构件识别器
        services.AddSingleton<IElementRecognizer, LayerBasedElementRecognizer>();
        services.AddSingleton<ISectionLineResolver, CadSectionLineResolver>();

        // Core 层
        services.AddSingleton<ISlabAssemblyBuilder, SlabAssemblyBuilder>();
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder>(sp =>
            new MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder(
                sp.GetRequiredService<ISlabAssemblyTemplateCatalog>(),
                sp.GetRequiredService<ISlabAssemblyBuilder>(),
                sp.GetRequiredService<ILogger<MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder>>()));
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.SectionComposer>(sp =>
            new MetroToolKits.SectionGenerator.Core.Sections.SectionComposer(
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder>()));
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.MultiFloorSectionComposer>(sp =>
            new MetroToolKits.SectionGenerator.Core.Sections.MultiFloorSectionComposer(
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.SectionComposer>()));
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>();
        services.AddSingleton<IWallAssemblyBuilder, WallAssemblyBuilder>();
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
        services.AddSingleton<IGenerateSectionUseCase>(sp =>
            new GenerateSectionUseCase(
                sp.GetRequiredService<IFloorConfigRepository>(),
                sp.GetRequiredService<IElementRecognizer>(),
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.MultiFloorSectionComposer>(),
                sp.GetRequiredService<IDrawingService>(),
                sp.GetRequiredService<ISectionSnapshotRepository>(),
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>(),
                sp.GetRequiredService<ILogger<GenerateSectionUseCase>>(),
                sp.GetRequiredService<MetroToolKits.Foundation.Core.Logging.IUserLogger>()));
        services.AddSingleton<ICheckSectionUpdatesUseCase>(sp =>
            new CheckSectionUpdatesUseCase(
                sp.GetRequiredService<ISectionSnapshotRepository>(),
                sp.GetRequiredService<ISectionBlockQueryService>(),
                sp.GetRequiredService<ISectionLineResolver>(),
                sp.GetRequiredService<IElementRecognizer>(),
                sp.GetRequiredService<IFloorConfigRepository>(),
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>(),
                sp.GetRequiredService<ISlabAssemblyTemplateCatalog>(),
                sp.GetRequiredService<MetroToolKits.SectionGenerator.Core.Sections.FloorVerticalProfileBuilder>(),
                sp.GetRequiredService<ILogger<CheckSectionUpdatesUseCase>>()));
        services.AddSingleton<IGenerateSectionPreflightUseCase, GenerateSectionPreflightUseCase>();
        services.AddSingleton<IWorkbenchSnapshotAssembler, WorkbenchSnapshotAssembler>();
        services.AddSingleton<ILayerMappingWorkspaceAssembler, LayerMappingWorkspaceAssembler>();
        services.AddSingleton<ILayerMappingApplyRequestMapper, LayerMappingApplyRequestMapper>();
        services.AddSingleton<IFloorConfigDocumentAssembler, FloorConfigDocumentAssembler>();
        services.AddSingleton<IFloorConfigSaveRequestMapper, FloorConfigSaveRequestMapper>();
        services.AddSingleton<ISectionOutputConfigMapper, SectionOutputConfigMapper>();
        services.AddSingleton<IUpdateSectionUseCase, UpdateSectionUseCase>();
        services.AddSingleton<ILocateSourceElementUseCase, LocateSourceElementUseCase>();
        services.AddSingleton<IFindRelatedSectionsUseCase, FindRelatedSectionsUseCase>();
        services.AddSingleton<IFloorConfigUseCase, FloorConfigUseCase>();
        services.AddSingleton<IElementConversionUseCase, ElementConversionUseCase>();

        // UI/工具箱
        services.AddSingleton<UI.FloorConfigPaletteController>();
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
    private readonly ILayerMappingWorkspaceAssembler _workspaceAssembler;
    private readonly ILayerMappingApplyRequestMapper _applyRequestMapper;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly IElementConversionUseCase _elementConversionUseCase;

    public OpenLayerMappingCommand(
        ILayerMappingWorkspaceAssembler workspaceAssembler,
        ILayerMappingApplyRequestMapper applyRequestMapper,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        IElementConversionUseCase elementConversionUseCase)
    {
        _workspaceAssembler = workspaceAssembler;
        _applyRequestMapper = applyRequestMapper;
        _wallTemplateCatalog = wallTemplateCatalog;
        _slabTemplateCatalog = slabTemplateCatalog;
        _elementConversionUseCase = elementConversionUseCase;
    }

    public void Execute()
    {
        var window = new UI.LayerMappingManager(
            _workspaceAssembler,
            _applyRequestMapper,
            _wallTemplateCatalog,
            _slabTemplateCatalog,
            _elementConversionUseCase);
        Application.ShowModalWindow(window);
    }
}
