using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.Services;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Local backups of sale.db: creating, pruning, the app-start rule and restoring.</summary>
internal class BackupTests
{
    private string _dir = null!;
    private string _backupDir = null!;
    private string _dbPath = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "salemanager-backup-" + Guid.NewGuid());
        _backupDir = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "sale.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private IProjectRepository OpenRepo() => new ProjectRepository(new SaleContext(_dir));

    private static void AddClient(IProjectRepository repo, string name)
    {
        repo.Add(new Client { ID = Guid.NewGuid().ToString(), Name = name, PhoneNumber = "100200300" });
        repo.Save();
    }

    private static int ClientCount(IProjectRepository repo) => repo.GetAll<Client>().Count();

    [Test]
    public void CreateBackup_CopiesTheDatabaseWhileItIsOpen()
    {
        var repo = OpenRepo();
        AddClient(repo, "Anna");

        var backup = BackupService.CreateBackup(_dbPath, _backupDir);

        Assert.That(File.Exists(backup.Path), Is.True);
        Assert.That(BackupService.IsValidBackup(backup.Path), Is.True);
        Assert.That(BackupService.List(_backupDir).Count, Is.EqualTo(1));
    }

    [Test]
    public void CreateBackup_TwoInTheSameSecond_BothKept()
    {
        OpenRepo();
        var now = new DateTime(2025, 5, 1, 10, 0, 0);

        BackupService.CreateBackup(_dbPath, _backupDir, now: now);
        BackupService.CreateBackup(_dbPath, _backupDir, now: now);

        Assert.That(BackupService.List(_backupDir).Count, Is.EqualTo(2));
    }

    [Test]
    public void CreateBackup_KeepsOnlyTheNewestOnes()
    {
        OpenRepo();
        var start = new DateTime(2025, 5, 1, 10, 0, 0);
        for (int i = 0; i < 5; i++)
            BackupService.CreateBackup(_dbPath, _backupDir, keep: 3, now: start.AddMinutes(i));

        var left = BackupService.List(_backupDir);

        Assert.That(left.Count, Is.EqualTo(3));
        Assert.That(left[0].Time, Is.EqualTo(start.AddMinutes(4)), "newest first");
        Assert.That(left[2].Time, Is.EqualTo(start.AddMinutes(2)));
    }

    [Test]
    public void TryAutoBackup_SkipsWhenARecentCopyExists_AndNeverThrows()
    {
        OpenRepo();
        var now = new DateTime(2025, 5, 1, 10, 0, 0);

        Assert.That(BackupService.TryAutoBackup(_dbPath, _backupDir, TimeSpan.FromHours(1), now: now), Is.Not.Null);
        Assert.That(BackupService.TryAutoBackup(_dbPath, _backupDir, TimeSpan.FromHours(1), now: now.AddMinutes(30)), Is.Null);
        Assert.That(BackupService.TryAutoBackup(_dbPath, _backupDir, TimeSpan.FromHours(1), now: now.AddMinutes(90)), Is.Not.Null);
        Assert.That(BackupService.List(_backupDir).Count, Is.EqualTo(2));

        Assert.DoesNotThrow(() => BackupService.TryAutoBackup(Path.Combine(_dir, "missing.db"), _backupDir, TimeSpan.Zero));
        File.WriteAllText(Path.Combine(_dir, "broken.db"), "not a database");
        Assert.DoesNotThrow(() => BackupService.TryAutoBackup(Path.Combine(_dir, "broken.db"), _backupDir, TimeSpan.Zero));
    }

    [Test]
    public void IsValidBackup_RejectsGarbageAndForeignDatabases()
    {
        var garbage = Path.Combine(_dir, "garbage.db");
        File.WriteAllText(garbage, "hello");
        Assert.That(BackupService.IsValidBackup(garbage), Is.False);
        Assert.That(BackupService.IsValidBackup(Path.Combine(_dir, "nope.db")), Is.False);

        var foreign = Path.Combine(_dir, "foreign.db");
        using (var connection = new SqliteConnection($"Data Source={foreign};Pooling=false"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Other (x INTEGER);";
            command.ExecuteNonQuery();
        }
        Assert.That(BackupService.IsValidBackup(foreign), Is.False);
    }

    [Test]
    public void Restore_BringsBackOldDataAndKeepsASafetyCopyOfTheCurrentOne()
    {
        var repo = OpenRepo();
        AddClient(repo, "Anna");
        var backup = BackupService.CreateBackup(_dbPath, _backupDir, now: new DateTime(2025, 5, 1, 10, 0, 0));
        AddClient(repo, "Bartek");
        AddClient(repo, "Celina");
        Assert.That(ClientCount(repo), Is.EqualTo(3));

        ((ProjectRepository)repo)._context.Dispose();
        BackupService.Restore(backup.Path, _dbPath, _backupDir, now: new DateTime(2025, 5, 1, 11, 0, 0));

        var reopened = OpenRepo();
        Assert.That(reopened.GetAll<Client>().Select(x => x.Name).ToList(), Is.EqualTo(new[] { "Anna" }));

        var safety = BackupService.List(_backupDir).Single(x => x.IsSafetyCopy);
        var restoredFromSafety = new ProjectRepository(new SaleContext(Path.GetDirectoryName(CopyAsSaleDb(safety.Path))!));
        Assert.That(ClientCount(restoredFromSafety), Is.EqualTo(3));
    }

    // A backup file is not named sale.db, so open it through a scratch folder.
    private string CopyAsSaleDb(string backupPath)
    {
        var scratch = Path.Combine(_dir, "scratch-" + Guid.NewGuid());
        Directory.CreateDirectory(scratch);
        var target = Path.Combine(scratch, "sale.db");
        File.Copy(backupPath, target);
        return target;
    }

    [Test]
    public void Restore_InvalidFile_ThrowsAndLeavesTheDatabaseAlone()
    {
        var repo = OpenRepo();
        AddClient(repo, "Anna");
        var garbage = Path.Combine(_dir, "garbage.db");
        File.WriteAllText(garbage, "hello");

        Assert.Throws<InvalidDataException>(() => BackupService.Restore(garbage, _dbPath, _backupDir));

        Assert.That(ClientCount(OpenRepo()), Is.EqualTo(1));
        Assert.That(BackupService.List(_backupDir), Is.Empty);
    }

    [Test]
    public void Restore_OldSchemaBackup_IsUpgradedWhenOpened()
    {
        var oldDb = Path.Combine(_dir, "old-source", "sale.db");
        Directory.CreateDirectory(Path.GetDirectoryName(oldDb)!);
        using (var connection = new SqliteConnection($"Data Source={oldDb};Pooling=false"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Clients (ID TEXT NOT NULL PRIMARY KEY, Name TEXT NULL, PhoneNumber TEXT NULL, Color TEXT NULL);" +
                                  "INSERT INTO Clients VALUES ('1', 'Stary', '500', NULL);";
            command.ExecuteNonQuery();
        }
        OpenRepo();

        BackupService.Restore(oldDb, _dbPath, _backupDir);

        var repo = OpenRepo();
        Assert.That(repo.GetAll<Client>().Single().Name, Is.EqualTo("Stary"));
    }

    // ---------- view model ----------

    [Test]
    public void BackupViewModel_CreateThenRestoreWithConfirmation()
    {
        var repo = OpenRepo();
        AddClient(repo, "Anna");
        var released = false;
        var vm = new BackupViewModel(_dbPath, _backupDir, beforeRestore: () =>
        {
            released = true;
            ((ProjectRepository)repo)._context.Dispose();
        });
        Assert.That(vm.HasBackups, Is.False);

        vm.CreateBackupCommand.Execute().Subscribe();
        Assert.That(vm.Backups.Count, Is.EqualTo(1));
        Assert.That(vm.StatusText, Does.StartWith("Zapisano kopię"));

        var fresh = OpenRepo();
        AddClient(fresh, "Bartek");

        vm.AskRestoreCommand.Execute(vm.Backups[0]).Subscribe();
        Assert.That(vm.IsRestoreConfirmationVisible, Is.True);
        vm.CancelRestoreCommand.Execute().Subscribe();
        Assert.That(vm.IsRestoreConfirmationVisible, Is.False);
        Assert.That(released, Is.False, "nothing touched until confirmed");

        vm.AskRestoreCommand.Execute(vm.Backups[0]).Subscribe();
        bool? result = null;
        vm.ConfirmRestoreCommand.Execute().Subscribe(x => result = x);

        Assert.That(result, Is.True);
        Assert.That(released, Is.True);
        Assert.That(ClientCount(OpenRepo()), Is.EqualTo(1));
        Assert.That(vm.Backups.Any(x => x.IsSafetyCopy), Is.True);
    }
}
