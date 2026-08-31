using System.IO;

namespace HeraldHelper.Desktop;

internal sealed class FileCatalogBackup : ICatalogBackup
{
    public void Backup(string root, string catalog, string backupRoot)
    {
        var source = Path.Combine(root, "data", catalog);
        if (Directory.Exists(source))
        {
            CopyDirectory(source, Path.Combine(backupRoot, catalog));
        }
    }

    public void Restore(string root, string catalog, string backupRoot)
    {
        var target = Path.Combine(root, "data", catalog);
        var backup = Path.Combine(backupRoot, catalog);
        if (!Directory.Exists(backup))
        {
            return;
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        Directory.Move(backup, target);
    }

    public void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
