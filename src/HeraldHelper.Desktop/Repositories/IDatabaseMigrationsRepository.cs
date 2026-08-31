namespace HeraldHelper.Desktop.Repositories;

public interface IDatabaseMigrationsRepository
{
    int GetDatabaseMigrationVersion();
    void MigrateDatabase();
}
