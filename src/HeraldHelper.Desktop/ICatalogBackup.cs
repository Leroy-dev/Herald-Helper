namespace HeraldHelper.Desktop;

internal interface ICatalogBackup
{
    void Backup(string root, string catalog, string backupRoot);

    void Restore(string root, string catalog, string backupRoot);

    void Delete(string path);
}
