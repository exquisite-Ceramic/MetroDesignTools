using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(MetroToolKits.Bootstrap.BootstrapCommandBridge))]

namespace MetroToolKits.Bootstrap;

/// <summary>
/// AutoCAD 命令桥接层。
/// 命令由 Bootstrap 暴露，实际实现仍来自插件注册到 DI 的命令类。
/// </summary>
public sealed class BootstrapCommandBridge
{
    [Autodesk.AutoCAD.Runtime.CommandMethod("GenSection")]
    public void GenSection()
        => Execute("GenSection");

    [Autodesk.AutoCAD.Runtime.CommandMethod("FloorConfig")]
    public void FloorConfig()
        => Execute("FloorConfig");

    [Autodesk.AutoCAD.Runtime.CommandMethod("CheckSectionUpdates")]
    public void CheckSectionUpdates()
        => Execute("CheckSectionUpdates");

    [Autodesk.AutoCAD.Runtime.CommandMethod("UpdateSection")]
    public void UpdateSection()
        => Execute("UpdateSection");

    [Autodesk.AutoCAD.Runtime.CommandMethod("LocateSourceElement")]
    public void LocateSourceElement()
        => Execute("LocateSourceElement");

    [Autodesk.AutoCAD.Runtime.CommandMethod("FindRelatedSections")]
    public void FindRelatedSections()
        => Execute("FindRelatedSections");

    [Autodesk.AutoCAD.Runtime.CommandMethod("ShowToolbox")]
    public void ShowToolbox()
        => Execute("ShowToolbox");

    [Autodesk.AutoCAD.Runtime.CommandMethod("ConvertRegion")]
    public void ConvertRegion()
        => Execute("ConvertRegion");

    [Autodesk.AutoCAD.Runtime.CommandMethod("RevertConversion")]
    public void RevertConversion()
        => Execute("RevertConversion");

    [Autodesk.AutoCAD.Runtime.CommandMethod("RevertAllConversions")]
    public void RevertAllConversions()
        => Execute("RevertAllConversions", "RevertAll");

    [Autodesk.AutoCAD.Runtime.CommandMethod("LayerMapping")]
    public void LayerMapping()
        => Execute("LayerMapping");

    [Autodesk.AutoCAD.Runtime.CommandMethod("SectionSelfTest")]
    public void SectionSelfTest()
        => Execute("SectionSelfTest");

    private static void Execute(string commandName, string methodName = "Execute")
    {
        try
        {
            Startup.Initialize();

            if (!Startup.ExecuteRegisteredCommand(commandName, methodName, out var errorMessage))
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
