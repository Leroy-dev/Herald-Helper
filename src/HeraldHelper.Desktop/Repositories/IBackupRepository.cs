namespace HeraldHelper.Desktop.Repositories;

public interface IBackupRepository
{
    void ExportJson(string outputPath);
    void ImportJson(string inputPath, bool replaceExisting = true);
}
