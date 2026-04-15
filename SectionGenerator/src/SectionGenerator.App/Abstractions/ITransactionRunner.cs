namespace SectionGenerator.App.Abstractions;

using System;

public interface ITransactionRunner
{
    T Run<T>(Func<T> action);
    void Run(Action action);

    T RunReadOnly<T>(Func<T> action);
    void RunReadOnly(Action action);
}

