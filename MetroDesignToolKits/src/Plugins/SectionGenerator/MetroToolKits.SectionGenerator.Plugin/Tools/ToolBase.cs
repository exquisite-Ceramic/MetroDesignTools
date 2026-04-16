using System;
using System.Windows.Forms;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.EditorInput;

namespace SectionGenerator.Plugin.Tools;

/// <summary>
/// 工具基类 - 提供通用功能
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual System.Drawing.Icon? Icon => null;
    public abstract string Category { get; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 获取当前文档
    /// </summary>
    protected Autodesk.AutoCAD.ApplicationServices.Document? ActiveDocument => AcadApplication.DocumentManager.MdiActiveDocument;

    /// <summary>
    /// 获取编辑器
    /// </summary>
    protected Editor? Editor => ActiveDocument?.Editor;

    /// <summary>
    /// 执行工具（模板方法模式）
    /// </summary>
    public void Execute()
    {
        if (!IsEnabled)
        {
            ShowMessage("工具未启用");
            return;
        }

        try
        {
            OnBeforeExecute();
            OnExecute();
            OnAfterExecute();
        }
        catch (System.Exception ex)
        {
            OnError(ex);
        }
    }

    /// <summary>
    /// 执行前钩子
    /// </summary>
    protected virtual void OnBeforeExecute()
    {
    }

    /// <summary>
    /// 实际执行逻辑（子类实现）
    /// </summary>
    protected abstract void OnExecute();

    /// <summary>
    /// 执行后钩子
    /// </summary>
    protected virtual void OnAfterExecute()
    {
    }

    /// <summary>
    /// 错误处理
    /// </summary>
    protected virtual void OnError(System.Exception ex)
    {
        ShowMessage($"执行出错: {ex.Message}");
    }

    /// <summary>
    /// 显示消息
    /// </summary>
    protected void ShowMessage(string message)
    {
        Editor?.WriteMessage($"\n{message}");
    }

    /// <summary>
    /// 获取配置界面（默认返回null）
    /// </summary>
    public virtual Control? GetConfigUI() => null;

    /// <summary>
    /// 初始化
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// 关闭
    /// </summary>
    public virtual void Shutdown()
    {
    }
}
