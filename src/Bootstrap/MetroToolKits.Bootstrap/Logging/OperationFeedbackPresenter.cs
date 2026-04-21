using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Logging;

namespace MetroToolKits.Bootstrap.Logging;

/// <summary>
/// 统一操作结果反馈适配器。
/// 负责用户可见摘要和结构化日志落地。
/// </summary>
public sealed class OperationFeedbackPresenter
{
    private readonly IUserLogger _userLogger;
    private readonly ILogger<OperationFeedbackPresenter> _logger;

    public OperationFeedbackPresenter(
        IUserLogger userLogger,
        ILogger<OperationFeedbackPresenter> logger)
    {
        _userLogger = userLogger;
        _logger = logger;
    }

    public void PresentFailure(
        string commandName,
        OperationFailure failure,
        IReadOnlyList<OperationDiagnostic>? diagnostics = null,
        bool useSectionLineInvalid = false)
    {
        if (useSectionLineInvalid)
        {
            _userLogger.SectionLineInvalid();
        }
        else
        {
            _userLogger.CommandFailed(commandName, failure.UserMessage, BuildTechnicalSummary(failure));
        }

        _logger.LogError(
            failure.InnerException,
            "命令 {CommandName} 失败，Code={Code}，Category={Category}，Stage={Stage}，Module={Module}，Diagnostics={DiagnosticCount}",
            commandName,
            failure.Code,
            failure.Category,
            failure.Stage,
            failure.Module,
            diagnostics?.Count ?? 0);

        LogDiagnostics(commandName, diagnostics);
    }

    public void PresentPartialSuccess(
        string commandName,
        string userMessage,
        IReadOnlyList<OperationDiagnostic>? diagnostics = null)
    {
        _userLogger.CommandCompleted(commandName, userMessage);
        _logger.LogWarning(
            "命令 {CommandName} 部分成功，Diagnostics={DiagnosticCount}",
            commandName,
            diagnostics?.Count ?? 0);

        LogDiagnostics(commandName, diagnostics);
    }

    public void LogDiagnostics(string commandName, IReadOnlyList<OperationDiagnostic>? diagnostics)
    {
        if (diagnostics == null || diagnostics.Count == 0)
            return;

        foreach (var diagnostic in diagnostics)
        {
            var message =
                "命令 {CommandName} 诊断，Code={Code}，Stage={Stage}，Module={Module}，Target={TargetHandle}，Message={Message}，Suggestion={Suggestion}";

            switch (diagnostic.Level)
            {
                case DiagnosticLevel.Error:
                    _logger.LogError(
                        message,
                        commandName,
                        diagnostic.Code,
                        diagnostic.Stage,
                        diagnostic.Module,
                        diagnostic.TargetHandle ?? "-",
                        diagnostic.Message,
                        diagnostic.Suggestion ?? "-");
                    break;

                case DiagnosticLevel.Warning:
                    _logger.LogWarning(
                        message,
                        commandName,
                        diagnostic.Code,
                        diagnostic.Stage,
                        diagnostic.Module,
                        diagnostic.TargetHandle ?? "-",
                        diagnostic.Message,
                        diagnostic.Suggestion ?? "-");
                    break;

                default:
                    _logger.LogInformation(
                        message,
                        commandName,
                        diagnostic.Code,
                        diagnostic.Stage,
                        diagnostic.Module,
                        diagnostic.TargetHandle ?? "-",
                        diagnostic.Message,
                        diagnostic.Suggestion ?? "-");
                    break;
            }
        }
    }

    private static string BuildTechnicalSummary(OperationFailure failure)
    {
        return $"Code={failure.Code}; Stage={failure.Stage}; Module={failure.Module}; Detail={failure.TechnicalMessage ?? failure.UserMessage}";
    }
}
