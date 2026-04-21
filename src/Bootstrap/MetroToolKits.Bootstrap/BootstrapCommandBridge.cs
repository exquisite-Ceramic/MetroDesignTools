using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(MetroToolKits.Bootstrap.BootstrapCommandBridge))]

namespace MetroToolKits.Bootstrap;

/// <summary>
/// AutoCAD 命令桥接层。
/// 命令由 Bootstrap 暴露，实际实现仍来自插件注册到 DI 的命令类。
/// </summary>
public sealed class BootstrapCommandBridge
{
    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.GenSection)]
    public void GenSection()
        => Execute(SectionGeneratorCommandNames.GenSection);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.FloorConfig)]
    public void FloorConfig()
        => Execute(SectionGeneratorCommandNames.FloorConfig);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.CheckSectionUpdates)]
    public void CheckSectionUpdates()
        => Execute(SectionGeneratorCommandNames.CheckSectionUpdates);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.UpdateSection)]
    public void UpdateSection()
        => Execute(SectionGeneratorCommandNames.UpdateSection);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.LocateSourceElement)]
    public void LocateSourceElement()
        => Execute(SectionGeneratorCommandNames.LocateSourceElement);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.FindRelatedSections)]
    public void FindRelatedSections()
        => Execute(SectionGeneratorCommandNames.FindRelatedSections);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.ShowToolbox)]
    public void ShowToolbox()
        => Execute(SectionGeneratorCommandNames.ShowToolbox);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.ConvertRegion)]
    public void ConvertRegion()
        => Execute(SectionGeneratorCommandNames.ConvertRegion);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.RevertConversion)]
    public void RevertConversion()
        => Execute(SectionGeneratorCommandNames.RevertConversion);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.RevertAllConversions)]
    public void RevertAllConversions()
        => Execute(SectionGeneratorCommandNames.RevertAllConversions);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.LayerMapping)]
    public void LayerMapping()
        => Execute(SectionGeneratorCommandNames.LayerMapping);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.SectionSelfTest)]
    public void SectionSelfTest()
        => Execute(SectionGeneratorCommandNames.SectionSelfTest);

    [Autodesk.AutoCAD.Runtime.CommandMethod(SectionGeneratorCommandNames.SectionHostAcceptance)]
    public void SectionHostAcceptance()
        => Execute(SectionGeneratorCommandNames.SectionHostAcceptance);

    private static void Execute(string commandName)
    {
        try
        {
            Startup.Initialize();

            if (!Startup.ExecuteRegisteredCommand(commandName, out var errorMessage))
            {
                WriteMessage($"\n[MetroToolKits] {commandName} 执行失败：{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            WriteMessage($"\n[MetroToolKits] {commandName} 执行失败：{ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static void WriteMessage(string message)
    {
        AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(message);
    }
}
