# MetroToolKits API 文档

## 项目概述

MetroToolKits 是一个基于插件化架构的 CAD 工具集，支持 AutoCAD 平台。采用分层架构设计，各工具之间保持独立，便于扩展和维护。

## 架构总览

```
┌─────────────────────────────────────────────────────────────────┐
│                     AutoCAD 宿主进程                             │
└─────────────────────────────────────────────────────────────────┘
                               │ NETLOAD
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MetroToolKits.Bootstrap.dll                     │
│                (插件加载器 / DI 容器 / 命令注册)                   │
└─────────────────────────────────────────────────────────────────┘
                               │ 扫描加载
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ SectionGenerator │  │ DoorWindow      │  │ Extras.Commands │
│   .Plugin.dll    │  │   .Plugin.dll   │  │     .dll        │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │                    │                    │
          └────────────────────┼────────────────────┘
                               │ 依赖
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     共享基础设施层 (Shared)                       │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ │
│  │ Foundation.Core  │ │  Foundation.Cad  │ │  Foundation.UI   │ │
│  │  (纯算法/几何)   │ │ (CAD API 封装)   │ │  (通用WPF控件)   │ │
│  └──────────────────┘ └──────────────────┘ └──────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Bootstrap 层（引导层）

### 1.1 IPlugin 接口

所有工具插件必须实现此接口。

```csharp
namespace MetroToolKits.Bootstrap;

public interface IPlugin
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 配置服务 - 注册插件所需的服务
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 注册命令
    /// </summary>
    void RegisterCommands(ICommandRegistry registry);
}
```

### 1.2 ICommandRegistry 接口

```csharp
namespace MetroToolKits.Bootstrap;

public interface ICommandRegistry
{
    /// <summary>
    /// 注册命令
    /// </summary>
    void RegisterCommand<T>(string commandName) where T : class;
}
```

### 1.3 PluginLoader 插件加载器

```csharp
namespace MetroToolKits.Bootstrap;

public class PluginLoader
{
    public PluginLoader(IServiceCollection services);
    
    /// <summary>
    /// 从指定目录加载插件
    /// </summary>
    public void LoadPlugins(string directory);
    
    /// <summary>
    /// 获取已加载的插件列表
    /// </summary>
    public IReadOnlyList<IPlugin> Plugins { get; }
}
```

### 1.4 Startup 启动类

```csharp
namespace MetroToolKits.Bootstrap;

public static class Startup
{
    /// <summary>
    /// 初始化应用程序
    /// </summary>
    public static void Initialize();
    
    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; }
}
```

---

## 2. Foundation 层（共享基础设施）

### 2.1 Foundation.Core（纯算法/几何）

#### Vec2 - 二维向量

```csharp
namespace MetroToolKits.Foundation.Core.Geometry;

public readonly struct Vec2
{
    public double X { get; }
    public double Y { get; }
    
    public Vec2(double x, double y);
    public double Length();
    public Vec2 Normalized();
    public static Vec2 operator +(Vec2 a, Vec2 b);
    public static Vec2 operator -(Vec2 a, Vec2 b);
    public static Vec2 operator *(Vec2 v, double scalar);
}
```

#### Polyline2 - 二维多段线

```csharp
namespace MetroToolKits.Foundation.Core.Geometry;

public sealed class Polyline2
{
    public IReadOnlyList<Vec2> Vertices { get; }
    public bool Closed { get; }
    
    public Polyline2(IEnumerable<Vec2> vertices, bool closed = false);
    public double GetLength();
}
```

### 2.2 Foundation.Cad（CAD API 封装）

#### IEditorService - 编辑器服务

```csharp
namespace MetroToolKits.Foundation.Cad.Services;

public interface IEditorService
{
    /// <summary>
    /// 获取点
    /// </summary>
    Point3d? GetPoint(string message);

    /// <summary>
    /// 选择实体
    /// </summary>
    PromptEntityResult? GetEntity(string message, Type allowedType);

    /// <summary>
    /// 选择多个实体
    /// </summary>
    PromptSelectionResult? GetSelection(string message, SelectionFilter? filter = null);

    /// <summary>
    /// 输出消息
    /// </summary>
    void WriteMessage(string message);

    /// <summary>
    /// 获取关键字
    /// </summary>
    string? GetKeyword(string message, string[] keywords, string defaultKeyword);

    /// <summary>
    /// 获取双精度数值
    /// </summary>
    double? GetDouble(string message, double defaultValue);
}
```

#### IDocumentService - 文档服务

```csharp
namespace MetroToolKits.Foundation.Cad.Services;

public interface IDocumentService
{
    /// <summary>
    /// 获取当前数据库
    /// </summary>
    Database? GetCurrentDatabase();

    /// <summary>
    /// 启动事务
    /// </summary>
    Transaction StartTransaction();

    /// <summary>
    /// 锁定文档
    /// </summary>
    IDisposable? LockDocument();
}
```

---

## 3. SectionGenerator 插件

### 3.1 领域层 (Core)

#### ISectionGenerator 接口

```csharp
namespace MetroToolKits.SectionGenerator.Core;

public interface ISectionGenerator
{
    /// <summary>
    /// 生成剖面
    /// </summary>
    SectionResult Generate(SectionDefinition def, SectionSettings settings);
}
```

#### SectionGenerator 实现

```csharp
namespace MetroToolKits.SectionGenerator.Core;

public sealed class SectionGenerator : ISectionGenerator
{
    public SectionResult Generate(SectionDefinition def, SectionSettings settings);
    public IReadOnlyList<LayerPosition> CalculateLayerPositions(SectionSettings settings, double length);
}
```

#### 数据模型

```csharp
namespace MetroToolKits.SectionGenerator.Core;

// 剖面定义
public sealed record SectionDefinition(
    Vec2 Origin,      // 起点坐标
    Vec2 Direction,   // 方向向量
    double SampleStep // 采样步长/长度
);

// 剖面设置
public sealed class SectionSettings
{
    public double BottomSlabThickness { get; set; } = 800.0;   // 底板厚度 (mm)
    public double TopSlabThickness { get; set; } = 600.0;     // 顶板厚度 (mm)
    public double FinishThickness { get; set; } = 120.0;      // 装修面层厚度 (mm)
    public bool HasSlope { get; set; } = true;                // 是否有坡度
    public double SlopeValue { get; set; } = 0.002;           // 坡度值
    public double FloorHeight { get; set; } = 5200.0;         // 层高 (mm)
}

// 剖面生成结果
public sealed record SectionResult(
    Polyline2 SectionPolyline,           // 剖面轮廓多段线
    IReadOnlyList<LayerPosition> Layers  // 各层位置信息
);

// 层位置
public sealed record LayerPosition(
    string Name,      // 层名称
    double BottomStart,  // 起点底部Y坐标
    double BottomEnd,    // 终点底部Y坐标
    double TopStart,     // 起点顶部Y坐标
    double TopEnd,       // 终点顶部Y坐标
    string HatchPattern  // 填充图案名称
);
```

### 3.2 应用层 (App)

#### IConfigService 配置服务接口

```csharp
namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IConfigService
{
    T LoadOrCreate<T>(string path, T defaultValue) where T : class, new();
    void Save<T>(string path, T value) where T : class;
}
```

#### ISectionDrawingService 剖面绘制服务接口

```csharp
namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ISectionDrawingService
{
    void DrawSection(SectionResult result, IReadOnlyList<double> intersectionParams);
}
```

### 3.3 基础设施层 (Infrastructure)

#### JsonConfigManager JSON配置管理器

```csharp
namespace MetroToolKits.SectionGenerator.Infrastructure.Config;

public sealed class JsonConfigManager : IConfigService
{
    public T LoadOrCreate<T>(string path, T defaultValue) where T : class, new();
    public void Save<T>(string path, T value) where T : class;
}
```

### 3.4 表示层 (Plugin)

#### SectionGeneratorPlugin 插件入口

```csharp
namespace MetroToolKits.SectionGenerator.Plugin;

public class SectionGeneratorPlugin : IPlugin
{
    public string Name => "SectionGenerator";
    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services);
    public void RegisterCommands(ICommandRegistry registry);
}
```

#### GenSectionCommand 生成剖面命令

```csharp
namespace MetroToolKits.SectionGenerator.Plugin;

public class GenSectionCommand
{
    [CommandMethod("GenSection")]
    public void Execute();
}
```

---

## 4. 使用示例

### 4.1 创建新插件

```csharp
using MetroToolKits.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Autodesk.AutoCAD.Runtime;

namespace MetroToolKits.MyTool.Plugin;

public class MyToolPlugin : IPlugin
{
    public string Name => "MyTool";
    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services)
    {
        // 注册服务
        services.AddSingleton<IMyService, MyService>();
    }

    public void RegisterCommands(ICommandRegistry registry)
    {
        // 注册命令
        registry.RegisterCommand<MyCommand>("MyCommand");
    }
}

public class MyCommand
{
    [CommandMethod("MyCommand")]
    public void Execute()
    {
        // 命令实现
    }
}
```

### 4.2 使用 Foundation.Cad 服务

```csharp
using MetroToolKits.Foundation.Cad.Services;

public class MyService
{
    private readonly IEditorService _editor;
    private readonly IDocumentService _document;

    public MyService(IEditorService editor, IDocumentService document)
    {
        _editor = editor;
        _document = document;
    }

    public void DoWork()
    {
        // 获取点
        var point = _editor.GetPoint("指定点: ");
        
        // 输出消息
        _editor.WriteMessage($"选择了点: {point}");
        
        // 使用事务
        using (var tr = _document.StartTransaction())
        {
            // 数据库操作
            tr.Commit();
        }
    }
}
```

### 4.3 直接使用领域层

```csharp
using MetroToolKits.SectionGenerator.Core;
using MetroToolKits.Foundation.Core.Geometry;

// 创建剖面定义
var definition = new SectionDefinition(
    new Vec2(0, 0),
    new Vec2(1, 0),
    10000.0
);

// 创建设置
var settings = new SectionSettings
{
    BottomSlabThickness = 800,
    TopSlabThickness = 600,
    FinishThickness = 120,
    FloorHeight = 5200,
    HasSlope = true,
    SlopeValue = 0.002
};

// 生成剖面
var generator = new SectionGenerator();
var result = generator.Generate(definition, settings);

// 使用结果
foreach (var layer in result.Layers)
{
    Console.WriteLine($"{layer.Name}: {layer.BottomStart} ~ {layer.TopEnd}");
}
```

---

## 5. 配置文件

### 5.1 默认配置格式

```json
{
  "BottomSlabThickness": 800.0,
  "TopSlabThickness": 600.0,
  "FinishThickness": 120.0,
  "HasSlope": true,
  "SlopeValue": 0.002,
  "FloorHeight": 5200.0
}
```

---

## 6. 依赖关系

### 6.1 层间依赖

```
表示层 (Presentation)
    ↓ 依赖
应用层 (Application)
    ↓ 依赖
领域层 (Domain)
    ↓ 依赖
通用工具层 (Foundation) ← 基础设施层 (Infrastructure) 也可依赖
```

### 6.2 项目引用关系

```
MetroToolKits.Bootstrap
    ├── MetroToolKits.Foundation.Cad
    └── 所有 MetroToolKits.*.Plugin

MetroToolKits.SectionGenerator.Plugin
    ├── MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.SectionGenerator.Core
    └── MetroToolKits.Bootstrap (IPlugin)

MetroToolKits.SectionGenerator.App
    ├── MetroToolKits.SectionGenerator.Core
    └── MetroToolKits.Foundation.Core

MetroToolKits.SectionGenerator.Core
    └── MetroToolKits.Foundation.Core

MetroToolKits.Foundation.Cad
    └── MetroToolKits.Foundation.Core
```

---

## 7. 扩展指南

### 7.1 添加新工具插件

1. 在 `src/Plugins/` 下创建新文件夹
2. 创建 Plugin 项目并实现 `IPlugin` 接口
3. 实现命令类并使用 `[CommandMethod]` 特性
4. 编译后 DLL 放入同一目录即可自动加载

```csharp
// 项目结构
MyTool/
├── MetroToolKits.MyTool.Core/
├── MetroToolKits.MyTool.App/
├── MetroToolKits.MyTool.Infrastructure/
└── MetroToolKits.MyTool.Plugin/
    └── MyToolPlugin.cs
```

### 7.2 添加小型脚本

直接在 `src/Plugins/Extras/MetroToolKits.Extras.Commands/` 中添加 Command 类：

```csharp
namespace MetroToolKits.Extras.Commands;

public class LayerCommands
{
    [CommandMethod("LayerLock")]
    public void LockLayer()
    {
        // 实现逻辑
    }
}
```

### 7.3 自定义配置服务

实现 `IConfigService` 接口：

```csharp
public class XmlConfigService : IConfigService
{
    public T LoadOrCreate<T>(string path, T defaultValue) where T : class, new()
    {
        // XML 实现
    }
    
    public void Save<T>(string path, T value) where T : class
    {
        // XML 实现
    }
}
```

---

## 8. 文件结构

```
MetroToolKits/
├── build/                                # 编译辅助文件（不发布）
│   └── References/
│       ├── accoremgd.dll
│       ├── acdbmgd.dll
│       └── acmgd.dll
│
├── src/                                  # 所有源码
│   ├── Shared/                           # 跨工具共享代码
│   │   ├── MetroToolKits.Foundation.Core/
│   │   │   ├── Geometry/
│   │   │   │   ├── Vec2.cs
│   │   │   │   └── Polyline2.cs
│   │   │   └── *.csproj
│   │   │
│   │   └── MetroToolKits.Foundation.Cad/
│   │       ├── Services/
│   │       │   ├── IEditorService.cs
│   │       │   ├── EditorService.cs
│   │       │   ├── IDocumentService.cs
│   │       │   └── DocumentService.cs
│   │       └── *.csproj
│   │
│   ├── Bootstrap/                        # 插件加载器
│   │   └── MetroToolKits.Bootstrap/
│   │       ├── IPlugin.cs
│   │       ├── PluginLoader.cs
│   │       ├── CommandRegistry.cs
│   │       ├── Startup.cs
│   │       └── *.csproj
│   │
│   └── Plugins/                          # 功能插件
│       ├── SectionGenerator/
│       │   ├── MetroToolKits.SectionGenerator.Core/
│       │   │   ├── SectionGenerator.cs
│       │   │   ├── SectionDefinition.cs
│       │   │   ├── SectionSettings.cs
│       │   │   └── *.csproj
│       │   ├── MetroToolKits.SectionGenerator.App/
│       │   │   ├── Abstractions/
│       │   │   └── *.csproj
│       │   ├── MetroToolKits.SectionGenerator.Infrastructure/
│       │   │   └── *.csproj
│       │   ├── MetroToolKits.SectionGenerator.Plugin/
│       │   │   ├── SectionGeneratorPlugin.cs
│       │   │   └── *.csproj
│       │   └── Resources/
│       │       └── SectionGeneratorConfig.json
│       │
│       └── Extras/                       # 小脚本收容所
│           └── MetroToolKits.Extras.Commands/
│
├── API.md                                # 本文档
└── README.md                             # 项目说明
```

---

## 9. 版本信息

- **版本**: 1.0.0
- **最后更新**: 2026-04-16
- **兼容平台**: AutoCAD 2018+
- **目标框架**: .NET 8.0
