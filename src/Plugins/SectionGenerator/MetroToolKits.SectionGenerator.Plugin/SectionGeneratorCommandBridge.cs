using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.DependencyInjection;
using MetroToolKits.Bootstrap;
using MetroToolKits.SectionGenerator.Plugin.Commands;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(MetroToolKits.SectionGenerator.Plugin.SectionGeneratorCommandBridge))]

namespace MetroToolKits.SectionGenerator.Plugin;

/// <summary>
/// AutoCAD 命令桥接层。
/// 只负责把宿主命令转发到 DI 中的命令实现，避免 AutoCAD 直接实例化带依赖的命令类。
/// </summary>
public sealed class SectionGeneratorCommandBridge
{
    [Autodesk.AutoCAD.Runtime.CommandMethod("GenSection")]
    public void GenSection()
        => Execute<GenSectionCommand>(command => command.Execute(), "GenSection");

    [Autodesk.AutoCAD.Runtime.CommandMethod("FloorConfig")]
    public void FloorConfig()
        => Execute<FloorConfigCommand>(command => command.Execute(), "FloorConfig");

    [Autodesk.AutoCAD.Runtime.CommandMethod("CheckSectionUpdates")]
    public void CheckSectionUpdates()
        => Execute<CheckSectionUpdatesCommand>(command => command.Execute(), "CheckSectionUpdates");

    [Autodesk.AutoCAD.Runtime.CommandMethod("UpdateSection")]
    public void UpdateSection()
        => Execute<UpdateSectionCommand>(command => command.Execute(), "UpdateSection");

    [Autodesk.AutoCAD.Runtime.CommandMethod("LocateSourceElement")]
    public void LocateSourceElement()
        => Execute<LocateSourceElementCommand>(command => command.Execute(), "LocateSourceElement");

    [Autodesk.AutoCAD.Runtime.CommandMethod("FindRelatedSections")]
    public void FindRelatedSections()
        => Execute<FindRelatedSectionsCommand>(command => command.Execute(), "FindRelatedSections");

    [Autodesk.AutoCAD.Runtime.CommandMethod("ShowToolbox")]
    public void ShowToolbox()
        => Execute<ShowToolboxCommand>(command => command.Execute(), "ShowToolbox");

    [Autodesk.AutoCAD.Runtime.CommandMethod("ConvertRegion")]
    public void ConvertRegion()
        => Execute<ConvertRegionElementsCommand>(command => command.Execute(), "ConvertRegion");

    [Autodesk.AutoCAD.Runtime.CommandMethod("RevertConversion")]
    public void RevertConversion()
        => Execute<RevertElementConversionCommand>(command => command.Execute(), "RevertConversion");

    [Autodesk.AutoCAD.Runtime.CommandMethod("RevertAllConversions")]
    public void RevertAllConversions()
        => Execute<RevertElementConversionCommand>(command => command.RevertAll(), "RevertAllConversions");

    [Autodesk.AutoCAD.Runtime.CommandMethod("LayerMapping")]
    public void LayerMapping()
        => Execute<OpenLayerMappingCommand>(command => command.Execute(), "LayerMapping");

    [Autodesk.AutoCAD.Runtime.CommandMethod("SectionSelfTest")]
    public void SectionSelfTest()
        => Execute<SectionGeneratorSelfTestCommand>(command => command.Execute(), "SectionSelfTest");

    private static void Execute<TCommand>(Action<TCommand> action, string commandName)
        where TCommand : class
    {
        try
        {
            Startup.Initialize(typeof(SectionGeneratorPlugin).Assembly);

            var serviceProvider = Startup.ServiceProvider;
            if (serviceProvider == null)
            {
                WriteMessage($"\n[MetroToolKits] {commandName} 初始化失败：ServiceProvider 不可用。");
                return;
            }

            var command = serviceProvider.GetRequiredService<TCommand>();
            action(command);
        }
        catch (Exception ex)
        {
            WriteMessage($"\n[MetroToolKits] {commandName} 执行失败：{ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static void WriteMessage(string message)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage(message);
    }
}
