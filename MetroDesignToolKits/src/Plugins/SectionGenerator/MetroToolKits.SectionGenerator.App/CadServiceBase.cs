using SectionGenerator.App.Abstractions;

namespace SectionGenerator.App;

public abstract class CadServiceBase
{
    protected CadServiceBase(ICadSession session, ITransactionRunner tr)
    {
        Session = session;
        Tr = tr;
    }

    protected ICadSession Session { get; }
    protected ITransactionRunner Tr { get; }
}

