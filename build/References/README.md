# AutoCAD 依赖库说明

此目录用于存放编译所需的 AutoCAD .NET 托管库，**这些文件不随仓库分发**。

## 需要的文件

| 文件 | 用途 |
|:---|:---|
| `acdbmgd.dll` | AutoCAD 数据库托管库 |
| `acmgd.dll` | AutoCAD 图形托管库 |
| `accoremgd.dll` | AutoCAD 核心托管库 |

## 获取方式

推荐使用仓库脚本同步：

```powershell
.\build\sync-autocad-references.ps1 -AutoCADDir "C:\Program Files\Autodesk\AutoCAD 2025"
```

也可以从本机已安装的 AutoCAD 目录手动复制：

```
C:\Program Files\Autodesk\AutoCAD <版本>\
```

## 版本兼容性

| AutoCAD 版本 | .NET 目标框架 | 兼容性 |
|:---|:---|:---|
| 2018 ~ 2025 | net8.0-windows | ✅ 兼容 |

## 合规说明

这三个 DLL 是 Autodesk AutoCAD 的组成部分，受 Autodesk 最终用户许可协议（EULA）约束：

- **不得**将这些文件提交到公开仓库或随项目分发
- **不得**在未获授权的环境中使用
- 仅可在持有合法 AutoCAD 许可证的机器上用于编译本项目

本仓库的 `.gitignore` 已将 `build/References/*.dll` 排除，此目录下的 DLL 文件**不会**被 git 追踪。
