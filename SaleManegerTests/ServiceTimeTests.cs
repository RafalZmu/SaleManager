using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.Services;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Timing how long each client takes, the last closed client and the "Czas obsługi" statistics.</summary>
internal class ServiceTimeTests
{
    private TempDb _db = null!;
    private DateTime _now;

    [SetUp]
    public void Setup()
    {
        _db = new TempDb();
        _now = new DateTime(2026, 11, 20, 10, 0, 0);
    }

    [TearDown] public void TearDown() => _db.Dispose();

    private ClientEditionViewModel Open(Client client, string sale, LastClosedClient? last = null) =>
        new(_db.Repo, client, sale, last) { Clock = () => _now };

    private (Client client, string sale) Prepare(string name = "Jan")
    {
        var sale = _db.AddSale();
        if (!_db.Repo.GetAll<Product>().Any())
        {
            _db.AddProduct("Apples", "00", 2);
            _db.AddProduct("Beets", "01", 3);
        }
        return (_db.AddClient(name, "123"), sale);
    }

    private static string? Close(ClientEditionViewModel vm)
    {
        string? result = null;
        vm.OpenClientSelectionCommand.Execute().Subscribe(r => result = r);
        return result;
    }

    private ClientSaleInfo Info(Client client) => _db.Repo.GetAll<ClientSaleInfo>().Single(x => x.ClientID == client.ID);

    // ---------- ServiceTimer ----------

    [Test]
    public void Timer_CountsFromFirstActivityToCheckpoint()
    {
        var timer = new ServiceTimer();
        timer.Activity(_now);
        timer.Activity(_now.AddSeconds(30));
        timer.Checkpoint(_now.AddSeconds(90));

        Assert.That(timer.Seconds, Is.EqualTo(90));
        Assert.That(timer.Started, Is.EqualTo(_now));
    }

    [Test]
    public void Timer_CutsLongIdleGaps()
    {
        var timer = new ServiceTimer();
        timer.Activity(_now);
        timer.Activity(_now.AddHours(2));

        Assert.That(timer.Seconds, Is.EqualTo(ServiceTimer.IdleCap.TotalSeconds));
    }

    [Test]
    public void Timer_WithoutActivity_StaysAtZero()
    {
        var timer = new ServiceTimer();
        timer.Checkpoint(_now.AddMinutes(1));

        Assert.That(timer.IsRunning, Is.False);
        Assert.That(timer.Seconds, Is.Zero);
    }

    // ---------- Client screen ----------

    [Test]
    public void EditVm_TimesFromFirstBoughtItemToClose()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);

        vm.Order = "Apples: 3";            // entering the order does not start the timer
        _now = _now.AddMinutes(10);
        vm.Sale = "Apples: 6";             // first bought item: the clock starts
        _now = _now.AddSeconds(60);
        vm.Sale = "Apples: 6\nBeets: 9";
        _now = _now.AddSeconds(30);
        Close(vm);

        var info = Info(client);
        Assert.That(info.ServiceSeconds, Is.EqualTo(90));
        Assert.That(info.ServiceStartTime, Is.EqualTo(new DateTime(2026, 11, 20, 10, 10, 0)));
        Assert.That(info.ServiceEndTime, Is.EqualTo(_now));
    }

    [Test]
    public void EditVm_OrderOnly_IsNotTimed()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);
        vm.Order = "Apples: 3";
        _now = _now.AddMinutes(5);
        Close(vm);

        Assert.That(Info(client).ServiceStartTime, Is.Null);
        Assert.That(Info(client).ServiceSeconds, Is.Zero);
    }

    [Test]
    public void EditVm_EmptyBoughtLine_DoesNotStartTheTimer()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);
        vm.Sale = "Apples: ";               // what the arrow button adds: no weight yet
        _now = _now.AddMinutes(2);
        vm.Sale = "Apples: 2";              // the weight is typed: now it starts
        _now = _now.AddSeconds(20);
        Close(vm);

        Assert.That(Info(client).ServiceSeconds, Is.EqualTo(20));
    }

    [Test]
    public void EditVm_ReopenedClient_AddsUpVisits_AndIgnoresTimeBetweenVisits()
    {
        var (client, sale) = Prepare();
        var first = Open(client, sale);
        first.Sale = "Apples: 2";
        _now = _now.AddSeconds(40);
        Close(first);
        var started = Info(client).ServiceStartTime;

        _now = _now.AddHours(3);            // the client comes back much later
        var second = Open(client, sale);
        second.Sale = "Apples: 2\nBeets: 3";
        _now = _now.AddSeconds(25);
        Close(second);

        var info = Info(client);
        Assert.That(info.ServiceSeconds, Is.EqualTo(65));
        Assert.That(info.ServiceStartTime, Is.EqualTo(started));
    }

    [Test]
    public void EditVm_OpeningWithoutEditing_KeepsTheSavedTime()
    {
        var (client, sale) = Prepare();
        var first = Open(client, sale);
        first.Sale = "Apples: 2";
        _now = _now.AddSeconds(40);
        Close(first);

        _now = _now.AddMinutes(30);
        var peek = Open(client, sale);
        _now = _now.AddMinutes(30);
        Close(peek);

        Assert.That(Info(client).ServiceSeconds, Is.EqualTo(40));
    }

    [Test]
    public void EditVm_AutosaveWhileServing_PersistsSecondsSoFar()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);
        vm.Sale = "Apples: 2";
        _now = _now.AddSeconds(20);
        Assert.That(vm.AutoSave(), Is.True);

        Assert.That(Info(client).ServiceSeconds, Is.EqualTo(20));
    }

    [Test]
    public void EditVm_AllBoughtItemsRemoved_ClearsTheTime()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);
        vm.Order = "Beets: 1";
        vm.Sale = "Apples: 2";
        _now = _now.AddSeconds(30);
        Close(vm);
        Assert.That(Info(client).ServiceSeconds, Is.EqualTo(30));

        var again = Open(client, sale);
        again.Sale = "";
        _now = _now.AddSeconds(30);
        Close(again);

        Assert.That(Info(client).ServiceSeconds, Is.Zero);
        Assert.That(Info(client).ServiceStartTime, Is.Null);
    }

    [Test]
    public void EditVm_LongIdleBeforeClosing_IsCapped()
    {
        var (client, sale) = Prepare();
        var vm = Open(client, sale);
        vm.Sale = "Apples: 2";
        _now = _now.AddHours(1);
        Close(vm);

        Assert.That(Info(client).ServiceSeconds, Is.EqualTo(ServiceTimer.IdleCap.TotalSeconds));
    }

    // ---------- Last closed client ----------

    [Test]
    public void EditVm_ClosedSummary_HasNameNumberAndBoughtValue()
    {
        var (client, sale) = Prepare("Anna");
        var vm = Open(client, sale);
        vm.Sale = "Apples: 12,5\nBeets: 20";

        var summary = vm.ClosedSummary()!;

        Assert.That(summary.Name, Is.EqualTo("Anna"));
        Assert.That(summary.Number, Is.EqualTo("123"));
        Assert.That(summary.BoughtValue, Is.EqualTo(32.5));
        Assert.That(summary.Text, Does.Contain("Anna").And.Contain("123").And.Contain("32.50"));
    }

    [Test]
    public void EditVm_ShowsLastClosedClient_ButNotForTheSameClient()
    {
        var (client, sale) = Prepare("Anna");
        var other = _db.AddClient("Beata", "999");
        var last = new LastClosedClient { ClientID = other.ID, Name = "Beata", Number = "999", BoughtValue = 10 };

        Assert.That(Open(client, sale, last).LastClosedText, Does.Contain("Beata"));
        Assert.That(Open(other, sale, last).LastClosedText, Is.Null);
        Assert.That(Open(client, sale).LastClosedText, Is.Null);
    }

    [Test]
    public void SelectionVm_ShowsLastClosed_FromMemoryOrFromTheDatabase()
    {
        var (client, sale) = Prepare("Anna");
        var vm = Open(client, sale);
        vm.Sale = "Apples: 10";
        _now = _now.AddSeconds(10);
        Close(vm);

        var fromDb = LastClosedClient.FromDatabase(_db.Repo, sale);
        Assert.That(fromDb, Is.Not.Null);
        Assert.That(fromDb!.Name, Is.EqualTo("Anna"));
        Assert.That(fromDb.BoughtValue, Is.EqualTo(10));

        var selection = new ClientSelectionViewModel(sale, _db.Repo, null, null, null, fromDb);
        Assert.That(selection.HasLastClosed, Is.True);
        Assert.That(selection.LastClosedText, Does.Contain("Anna"));
        Assert.That(new ClientSelectionViewModel(sale, _db.Repo, null).HasLastClosed, Is.False);
    }

    // ---------- Statistics ----------

    private static ServiceRecord Rec(string client, double seconds, double kg, double value, int items, string sale = "s1", int hour = 10) => new()
    {
        SaleID = sale, SaleName = sale, ClientID = client, ClientName = client,
        Start = new DateTime(2026, 11, 20, hour, 0, 0), Seconds = seconds, Kg = kg, Value = value, Items = items,
        ByProduct = new() { ["p"] = (items, kg) }
    };

    [Test]
    public void Summary_ComputesAverageMedianAndRates()
    {
        var records = new[] { Rec("a", 60, 1, 10, 1), Rec("b", 120, 2, 20, 2), Rec("c", 600, 3, 30, 3) };

        var s = ServiceAnalytics.Summarize(records);

        Assert.That(s.Count, Is.EqualTo(3));
        Assert.That(s.AverageSeconds, Is.EqualTo(260));
        Assert.That(s.MedianSeconds, Is.EqualTo(120));
        Assert.That(s.FastestSeconds, Is.EqualTo(60));
        Assert.That(s.SlowestSeconds, Is.EqualTo(600));
        Assert.That(s.SecondsPerKg, Is.EqualTo(130));
        Assert.That(s.ClientsPerHour, Is.EqualTo(3 / (780 / 3600.0)).Within(1e-9));
    }

    [Test]
    public void Summary_OfNothing_IsEmpty()
    {
        var s = ServiceAnalytics.Summarize(Array.Empty<ServiceRecord>());
        Assert.That(s.Count, Is.Zero);
        Assert.That(s.SecondsPerKg, Is.Null);
        Assert.That(ServiceAnalytics.Distribution(Array.Empty<ServiceRecord>()), Is.Empty);
    }

    [Test]
    public void Correlation_PerfectLine_HasRofOneAndRightSlope()
    {
        // 30 seconds per kilogram plus 20 seconds of fixed time
        var records = Enumerable.Range(1, 5).Select(i => Rec("c" + i, 20 + 30 * i, i, i * 10, 1)).ToList();

        var c = ServiceAnalytics.Correlate(records, ServiceMetric.Kg);

        Assert.That(c.R, Is.EqualTo(1).Within(1e-9));
        Assert.That(c.Slope, Is.EqualTo(30).Within(1e-9));
        Assert.That(c.Intercept, Is.EqualTo(20).Within(1e-9));
        Assert.That(c.Description, Does.Contain("silna"));
    }

    [Test]
    public void Correlation_NeedsThreeClientsAndSomeVariation()
    {
        Assert.That(ServiceAnalytics.Correlate(new[] { Rec("a", 60, 1, 1, 1), Rec("b", 90, 2, 1, 1) }, ServiceMetric.Kg).R, Is.Null);
        var sameKg = new[] { Rec("a", 60, 2, 1, 1), Rec("b", 90, 2, 1, 1), Rec("c", 30, 2, 1, 1) };
        Assert.That(ServiceAnalytics.Correlate(sameKg, ServiceMetric.Kg).R, Is.Null);
    }

    [Test]
    public void Distribution_PutsEveryClientInABucket()
    {
        var records = Enumerable.Range(1, 40).Select(i => Rec("c" + i, i * 20, 1, 1, 1)).Append(Rec("slow", 7200, 1, 1, 1)).ToList();

        var buckets = ServiceAnalytics.Distribution(records);

        Assert.That(buckets.Sum(x => x.Count), Is.EqualTo(records.Count));
        Assert.That(buckets.Count, Is.InRange(2, 12));
    }

    [Test]
    public void ByHour_AndByClient_GroupCorrectly()
    {
        var records = new[]
        {
            Rec("a", 60, 1, 10, 1, hour: 9), Rec("a", 120, 1, 30, 1, sale: "s2", hour: 9), Rec("b", 300, 1, 5, 1, hour: 11)
        };

        var hours = ServiceAnalytics.ByHour(records);
        Assert.That(hours.Select(x => x.Hour), Is.EqualTo(new[] { 9, 11 }));
        Assert.That(hours[0].AverageSeconds, Is.EqualTo(90));

        var clients = ServiceAnalytics.ByClient(records);
        Assert.That(clients[0].Name, Is.EqualTo("b"), "slowest first");
        Assert.That(clients.Single(x => x.Name == "a").Visits, Is.EqualTo(2));
        Assert.That(clients.Single(x => x.Name == "a").AverageValue, Is.EqualTo(20));
    }

    [Test]
    public void Duration_IsReadable()
    {
        Assert.That(ServiceAnalytics.Duration(45), Is.EqualTo("45 s"));
        Assert.That(ServiceAnalytics.Duration(312), Is.EqualTo("5 min 12 s"));
        Assert.That(ServiceAnalytics.Duration(3900), Is.EqualTo("1 h 05 min"));
    }

    [Test]
    public void Statistics_ServiceTab_UsesRealTimedClients()
    {
        var sale = _db.AddSale("Piątek");
        var apples = _db.AddProduct("Apples", "00", 2);
        var beets = _db.AddProduct("Beets", "01", 3);
        var times = new[] { 60.0, 120.0, 180.0 };
        for (int i = 0; i < 3; i++)
        {
            var client = _db.AddClient("Client" + i, "10" + i);
            _db.AddOrder(client, apples, sale, (10 * (i + 1)).ToString(), false);   // 10, 20, 30 zł => 5, 10, 15 kg
            _db.AddOrder(client, beets, sale, "6", false);
            _db.Repo.Add(new ClientSaleInfo { ID = "i" + i, ClientID = client.ID, SaleID = sale, ServiceStartTime = _now.AddHours(i), ServiceEndTime = _now.AddHours(i), ServiceSeconds = times[i] });
        }
        // Not timed (old data or nothing bought): must not appear.
        var untimed = _db.AddClient("Untimed", "000");
        _db.AddOrder(untimed, apples, sale, "10", false);
        _db.Repo.Save();

        var vm = StatisticsHelper.Open(_db.Repo);
        var service = vm.Service;

        Assert.That(service.HasData, Is.True);
        Assert.That(service.CountText, Is.EqualTo("3"));
        Assert.That(service.AverageText, Is.EqualTo("2 min 00 s"));
        Assert.That(service.ProductRows.Select(x => x.Name), Is.EquivalentTo(new[] { "Apples", "Beets" }));
        Assert.That(service.SaleRows.Single().Clients, Is.EqualTo(3));
        Assert.That(service.CorrelationPlot, Is.Not.Null);

        service.SelectedMetric = service.MetricOptions[2];
        Assert.That(service.CorrelationText, Does.Contain("r ="));
        service.SelectedScope = service.Scopes[0];
        Assert.That(service.CountText, Is.EqualTo("3"));
    }

    [Test]
    public void Statistics_ServiceTab_WithoutTimings_ShowsEmptyState()
    {
        var sale = _db.AddSale();
        var product = _db.AddProduct("Apples", "00", 2);
        _db.AddOrder(_db.AddClient(), product, sale, "10", false);

        var service = StatisticsHelper.Open(_db.Repo).Service;

        Assert.That(service.HasData, Is.False);
        Assert.That(service.HasNoData, Is.True);
        Assert.That(service.AverageText, Is.EqualTo("-"));
    }

    // ---------- Old databases ----------

    [Test]
    public void OldClientSaleInfosTable_GetsTheTimingColumns()
    {
        var dir = Path.Combine(Path.GetTempPath(), "salemanager-old-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            using (var connection = new SqliteConnection($"Data Source={Path.Combine(dir, "sale.db")}"))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE ""Clients"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_Clients"" PRIMARY KEY, ""Color"" TEXT NULL, ""Name"" TEXT NULL, ""PhoneNumber"" TEXT NULL);
                    CREATE TABLE ""ClientSaleInfos"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_ClientSaleInfos"" PRIMARY KEY, ""ClientID"" TEXT NULL, ""SaleID"" TEXT NULL, ""FirstPurchaseTime"" TEXT NULL, ""FirstOrderTime"" TEXT NULL, ""ExpectedArrivalTime"" TEXT NULL);
                    INSERT INTO ""ClientSaleInfos"" (ID, ClientID, SaleID) VALUES ('i1', 'c1', 's1');";
                cmd.ExecuteNonQuery();
            }

            var repo = new ProjectRepository(new SaleContext(dir));
            var info = repo.GetAll<ClientSaleInfo>().Single();
            Assert.That(info.ServiceSeconds, Is.Zero);
            Assert.That(info.ServiceStartTime, Is.Null);

            info.ServiceSeconds = 42;
            repo.Update(info);
            repo.Save();
            Assert.That(new ProjectRepository(new SaleContext(dir)).GetAll<ClientSaleInfo>().Single().ServiceSeconds, Is.EqualTo(42));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }
}
