namespace HeraldHelper.Application.Contracts;

public interface IOcrCaptureSnapshotSource
{
    byte[]? LastCapturePng { get; }
    string LastCaptureEngineName { get; }
}

public interface IOcrCaptureBatchDiagnostics
{
    void BeginCaptureBatch();
    void CompleteCaptureBatch();
}
