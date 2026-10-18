using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>The "Dodać do wysyłki SMS" mark: database upgrade, edit screen and client list filter.</summary>
internal class SmsGroupFlagTests
{
    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "salemanager-smsflag-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(_dir, "sale.db")}");
        connection.Open();
        return connection;
    }

    private void CreateOldSchemaDatabase()
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ""Clients"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_Clients"" PRIMARY KEY, ""Color"" TEXT NULL, ""Name"" TEXT NULL, ""PhoneNumber"" TEXT NULL);
            INSERT INTO ""Clients"" VALUES ('c1', 'Red', 'Jan Kowalski', '123456789');
            INSERT INTO ""Clients"" VALUES ('c2', 'Blue', 'Anna Nowak', '987654321');";
        cmd.ExecuteNonQuery();
    }

    private List<string> ClientColumns()
    {
        using var connection = Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(\"Clients\")";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(1));
        return names;
    }

    // ---------- database upgrade ----------

    [Test]
    public void OldDatabase_GetsColumn_KeepsClients_AndNobodyIsMarked()
    {
        CreateOldSchemaDatabase();
        Assert.That(ClientColumns(), Does.Not.Contain("AddToSmsGroup"));

        var db = new ProjectRepository(new SaleContext(_dir));

        Assert.That(ClientColumns(), Does.Contain("AddToSmsGroup"));
        var clients = db.GetAll<Client>().OrderBy(x => x.ID).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(clients.Select(x => x.Name), Is.EqualTo(new[] { "Jan Kowalski", "Anna Nowak" }));
            Assert.That(clients.Select(x => x.PhoneNumber), Is.EqualTo(new[] { "123456789", "987654321" }));
            Assert.That(clients.Select(x => x.Color), Is.EqualTo(new[] { "Red", "Blue" }));
            Assert.That(clients.Any(x => x.AddToSmsGroup), Is.False);
        });
    }

    [Test]
    public void OldDatabase_FlagIsSavedAndSurvivesReopen()
    {
        CreateOldSchemaDatabase();
        var db = new ProjectRepository(new SaleContext(_dir));
        var client = db.GetAll<Client>().Single(x => x.ID == "c1");
        client.AddToSmsGroup = true;
        db.Update(client);
        db.Save();

        var reopened = new ProjectRepository(new SaleContext(_dir));

        Assert.That(reopened.GetAll<Client>().Single(x => x.ID == "c1").AddToSmsGroup, Is.True);
        Assert.That(reopened.GetAll<Client>().Single(x => x.ID == "c2").AddToSmsGroup, Is.False);
    }

    [Test]
    public void OpeningTwice_DoesNotFailOrDuplicateTheColumn()
    {
        CreateOldSchemaDatabase();
        new ProjectRepository(new SaleContext(_dir));

        Assert.DoesNotThrow(() => new ProjectRepository(new SaleContext(_dir)));
        Assert.That(ClientColumns().Count(x => x == "AddToSmsGroup"), Is.EqualTo(1));
    }

    [Test]
    public void BrandNewDatabase_HasColumn()
    {
        var db = new ProjectRepository(new SaleContext(_dir));

        Assert.That(ClientColumns().Count(x => x == "AddToSmsGroup"), Is.EqualTo(1));
        Assert.That(db.GetAll<Client>(), Is.Empty);
    }

    // ---------- edit screen ----------

    [Test]
    public void EditScreen_TickedBoxIsSaved_AndUntouchedBoxStaysOff()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        db.AddProduct("Apples", "00");
        var client = db.AddClient("Jan", "123456789");

        var vm = new ClientEditionViewModel(db.Repo, client, sale);
        Assert.That(vm.AddToSmsGroup, Is.False);
        vm.AddToSmsGroup = true;
        vm.OpenClientSelectionCommand.Execute().Subscribe();

        Assert.That(db.Repo.GetAll<Client>().Single().AddToSmsGroup, Is.True);

        // editing the name later must not reset the mark
        var again = new ClientEditionViewModel(db.Repo, db.Repo.GetAll<Client>().Single(), sale);
        Assert.That(again.AddToSmsGroup, Is.True);
        again.Name = "Jan Nowy";
        again.OpenClientSelectionCommand.Execute().Subscribe();
        Assert.That(db.Repo.GetAll<Client>().Single().AddToSmsGroup, Is.True);

        // and it can be switched off again
        var third = new ClientEditionViewModel(db.Repo, db.Repo.GetAll<Client>().Single(), sale);
        third.AddToSmsGroup = false;
        third.OpenClientSelectionCommand.Execute().Subscribe();
        Assert.That(db.Repo.GetAll<Client>().Single().AddToSmsGroup, Is.False);
    }

    // ---------- client list ----------

    private static void Tick(TempDb db, Client client)
    {
        var tracked = db.Repo.GetAll<Client>().Single(x => x.ID == client.ID);
        tracked.AddToSmsGroup = true;
        db.Repo.Update(tracked);
        db.Repo.Save();
    }

    [Test]
    public void ClientList_Filter_ShowsOnlyMarkedClientsWithPhone_AndCountsAsActiveFilter()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        var marked = db.AddClient("Marked", "111222333");
        var markedNoPhone = db.AddClient("NoPhone", "");
        db.AddClient("Unmarked", "444555666");
        Tick(db, marked);
        Tick(db, markedNoPhone);

        var vm = new ClientSelectionViewModel(sale, db.Repo, null);
        Assert.That(vm.Clients.Count, Is.EqualTo(3));
        Assert.That(vm.HasActiveFilters, Is.False);

        vm.AreSmsPendingShowing = true;
        vm.UpdateClientsCommand.Execute().Subscribe();

        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Marked" }));
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(1));
    }

    [Test]
    public void ClientList_FilterState_IsRestoredFromSavedSelection_AndOldSelectionsStillWork()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        db.AddProduct("Apples", "00");
        var first = new ClientSelectionViewModel(sale, db.Repo, null);
        first.AreSmsPendingShowing = true;
        List<bool>? saved = null;
        first.UpdateClientsCommand.Execute().Subscribe(x => saved = x);
        Assert.That(saved, Is.Not.Null);

        var reopened = new ClientSelectionViewModel(sale, db.Repo, saved!);
        Assert.That(reopened.AreSmsPendingShowing, Is.True);

        // a selection saved by the previous version has no entry for the new filter
        var old = saved!.Take(saved.Count - 1).ToList();
        var fromOld = new ClientSelectionViewModel(sale, db.Repo, old);
        Assert.That(fromOld.AreSmsPendingShowing, Is.False);
    }
}
