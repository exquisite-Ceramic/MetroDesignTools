using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using SectionGenerator.App.Abstractions;
using System;

namespace SectionGenerator.CadAdapter;

public sealed class AcadTransactionRunner : ITransactionRunner
{
    private readonly Document _doc;

    public AcadTransactionRunner(Document doc)
    {
        _doc = doc;
    }

    public T Run<T>(Func<T> action)
    {
        using Transaction tr = _doc.TransactionManager.StartTransaction();
        T result = action();
        tr.Commit();
        return result;
    }

    public void Run(Action action)
    {
        using Transaction tr = _doc.TransactionManager.StartTransaction();
        action();
        tr.Commit();
    }

    public T RunReadOnly<T>(Func<T> action)
    {
        using Transaction tr = _doc.TransactionManager.StartTransaction();
        return action();
    }

    public void RunReadOnly(Action action)
    {
        using Transaction tr = _doc.TransactionManager.StartTransaction();
        action();
    }
}

