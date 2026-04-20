using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(MetroToolKits.Bootstrap.BootstrapExtensionApplication))]

namespace MetroToolKits.Bootstrap;

/// <summary>
/// AutoCAD 宿主入口，负责在 NETLOAD 后初始化 Bootstrap。
/// </summary>
public sealed class BootstrapExtensionApplication : IExtensionApplication
{
    public void Initialize()
    {
        Startup.Initialize();
    }

    public void Terminate()
    {
    }
}
