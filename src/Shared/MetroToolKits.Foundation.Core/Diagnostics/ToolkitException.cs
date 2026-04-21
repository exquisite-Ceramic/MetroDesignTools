namespace MetroToolKits.Foundation.Core.Diagnostics;

/// <summary>
/// 工具包统一异常基类。
/// </summary>
public class ToolkitException : Exception
{
    public ToolkitException(OperationFailure failure)
        : base(failure.TechnicalMessage ?? failure.UserMessage, failure.InnerException)
    {
        Failure = failure;
    }

    public OperationFailure Failure { get; }
}

public sealed class UserInputException : ToolkitException
{
    public UserInputException(OperationFailure failure)
        : base(failure)
    {
    }
}

public sealed class ApplicationFlowException : ToolkitException
{
    public ApplicationFlowException(OperationFailure failure)
        : base(failure)
    {
    }
}

public sealed class DomainRuleException : ToolkitException
{
    public DomainRuleException(OperationFailure failure)
        : base(failure)
    {
    }
}

public sealed class InfrastructureException : ToolkitException
{
    public InfrastructureException(OperationFailure failure)
        : base(failure)
    {
    }
}

public sealed class SystemInternalException : ToolkitException
{
    public SystemInternalException(OperationFailure failure)
        : base(failure)
    {
    }
}
