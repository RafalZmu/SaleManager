using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;

namespace SaleManegerTests;

/// <summary>
/// Simulates an existing production database (schema from before the 2026 changes) and
/// verifies that starting the current app code keeps the data and adds the new tables.
/// </summary>
internal class UpgradeCompatibilityTests
{
    private string _dir;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "salemanager-upgrade-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private void CreateOldSchemaDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(_dir, "sale.db")}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ""Clients"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_Clients"" PRIMARY KEY, ""Color"" TEXT NULL, ""Name"" TEXT NULL, ""PhoneNumber"" TEXT NULL);
            CREATE TABLE ""ClientsOrders"" (""ClientOrderID"" TEXT NOT NULL CONSTRAINT ""PK_ClientsOrders"" PRIMARY KEY, ""ClientID"" TEXT NULL, ""Date"" TEXT NOT NULL, ""IsReserved"" INTEGER NOT NULL, ""ProductID"" TEXT NULL, ""SaleID"" TEXT NULL, ""Value"" TEXT NULL);
            CREATE TABLE ""Products"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_Products"" PRIMARY KEY, ""Code"" TEXT NULL, ""IsReserved"" INTEGER NOT NULL, ""Name"" TEXT NULL, ""PricePerKg"" REAL NOT NULL, ""Value"" TEXT NULL);
            CREATE TABLE ""Sales"" (""SaleID"" TEXT NOT NULL CONSTRAINT ""PK_Sales"" PRIMARY KEY, ""SaleName"" TEXT NULL);
            CREATE TABLE ""SalesProducts"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_SalesProducts"" PRIMARY KEY, ""Amount"" REAL NOT NULL, ""PricePerKg"" REAL NOT NULL, ""ProductCode"" TEXT NULL, ""ProductID"" TEXT NULL, ""ProductName"" TEXT NULL, ""SaleID"" TEXT NULL);
            INSERT INTO ""Clients"" VALUES ('c1', 'Red', 'Jan Kowalski', '123456789');
            INSERT INTO ""Sales"" VALUES ('s1', 'Sale 2024');
            INSERT INTO ""Products"" VALUES ('p1', '00', 0, 'Jablka', 3.5, NULL);
            INSERT INTO ""ClientsOrders"" VALUES ('o1', 'c1', '2024-11-01 10:00:00', 1, 'p1', 's1', '2,5 komentarz');
            INSERT INTO ""SalesProducts"" VALUES ('sp1', 12.5, 3.5, '00', 'p1', 'Jablka', 's1');";
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(string dir, string table)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(dir, "sale.db")}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.AddWithValue("$n", table);
        return (long)cmd.ExecuteScalar()! == 1;
    }

    [Test]
    public void OldDatabase_KeepsAllExistingData()
    {
        CreateOldSchemaDatabase();

        var db = new ProjectRepository(new SaleContext(_dir));

        Assert.Multiple(() =>
        {
            Assert.That(db.GetAll<Client>().Single().Name, Is.EqualTo("Jan Kowalski"));
            Assert.That(db.GetAll<Client>().Single().Color, Is.EqualTo("Red"));
            Assert.That(db.GetAll<Sale>().Single().SaleName, Is.EqualTo("Sale 2024"));
            Assert.That(db.GetAll<Product>().Single().PricePerKg, Is.EqualTo(3.5));
            Assert.That(db.GetAll<ClientOrder>().Single().Value, Is.EqualTo("2,5 komentarz"));
            var saleProduct = db.GetAll<SaleProduct>().Single();
            Assert.That(saleProduct.Amount, Is.EqualTo(12.5));
            Assert.That(saleProduct.ProductCode, Is.EqualTo("00"));
        });
    }

    [Test]
    public void OldDatabase_GetsNewTables_AndTheyAreUsable()
    {
        CreateOldSchemaDatabase();
        Assert.That(TableExists(_dir, "ClientSaleInfos"), Is.False);
        Assert.That(TableExists(_dir, "SaleDayPlans"), Is.False);

        var db = new ProjectRepository(new SaleContext(_dir));

        Assert.That(TableExists(_dir, "ClientSaleInfos"), Is.True);
        Assert.That(TableExists(_dir, "SaleDayPlans"), Is.True);

        var arrival = new DateTime(2026, 4, 1, 14, 30, 0);
        db.Add(new ClientSaleInfo { ID = "i1", ClientID = "c1", SaleID = "s1", ExpectedArrivalTime = arrival });
        db.Add(new SaleDayPlan { ID = "d1", SaleID = "s1", StartTime = arrival, EndTime = arrival.AddHours(4) });
        db.Save();

        // Re-open with a fresh context to prove the rows were really persisted.
        var reopened = new ProjectRepository(new SaleContext(_dir));
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetAll<ClientSaleInfo>().Single().ExpectedArrivalTime, Is.EqualTo(arrival));
            Assert.That(reopened.GetAll<ClientSaleInfo>().Single().FirstOrderTime, Is.Null);
            Assert.That(reopened.GetAll<SaleDayPlan>().Single().EndTime, Is.EqualTo(arrival.AddHours(4)));
        });
    }

    [Test]
    public void StartingTwice_IsIdempotent_AndDoesNotLoseNewData()
    {
        CreateOldSchemaDatabase();
        var first = new ProjectRepository(new SaleContext(_dir));
        first.Add(new ClientSaleInfo { ID = "i1", ClientID = "c1", SaleID = "s1" });
        first.Save();

        var second = new ProjectRepository(new SaleContext(_dir));

        Assert.That(second.GetAll<ClientSaleInfo>().Count(), Is.EqualTo(1));
        Assert.That(second.GetAll<Client>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void BrandNewDatabase_HasAllTables()
    {
        var db = new ProjectRepository(new SaleContext(_dir));

        foreach (var table in new[] { "Clients", "ClientsOrders", "Products", "Sales", "SalesProducts", "ClientSaleInfos", "SaleDayPlans" })
            Assert.That(TableExists(_dir, table), Is.True, table);
        Assert.That(db.GetAll<Client>(), Is.Empty);
    }
}
