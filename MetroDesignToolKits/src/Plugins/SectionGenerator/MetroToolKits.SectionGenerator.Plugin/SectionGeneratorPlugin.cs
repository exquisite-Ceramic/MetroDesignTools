using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.CadAdapter;
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
    public string Version => "1.0.0";

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
        services.AddSingleton<MetroToolKits.SectionGenerator.Core.Sections.FloorGeometryHasher>();

        // 绘图服务
        services.AddSingleton<IDrawingService, CadDrawingService>();

        // 用例
        services.AddSingleton<IGenerateSectionUseCase, GenerateSectionUseCase>();

        // 命令
        services.AddTransient<GenSectionCommand>();
        services.AddTransient<ConvertRegionElementsCommand>();
        services.AddTransient<RevertElementConversionCommand>();
        services.AddTransient<OpenLayerMappingCommand>();
    }

    public void RegisterCommands(ICommandRegistry registry)
    {
        registry.RegisterCommand<GenSectionCommand>("GenSection");
        registry.RegisterCommand<ConvertRegionElementsCommand>("ConvertRegion");
        registry.RegisterCommand<RevertElementConversionCommand>("RevertConversion");
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
        _layerService = layerService;
        _backupService = backupService;
        _typeLoader = typeLoader;
    }

    [Autodesk.AutoCAD.Runtime.CommandMethod("LayerMapping")]
    public void Execute()
    {
        var window = new UI.LayerMappingManager(_layerService, _backupService, _typeLoader);
        Application.ShowModalWindow(window);
    }
}
