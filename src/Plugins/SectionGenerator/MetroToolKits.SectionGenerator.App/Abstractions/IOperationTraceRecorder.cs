using MetroToolKits.SectionGenerator.Contracts.OperationTrace;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IOperationTraceRecorder
{
    string StartSession(string drawingName);

    void Record(OperationTraceEventDto operationEvent);

    void EndSession(string sessionId);

    void Flush();
}
