using SaleManeger.Models;
using SaleManeger.Services;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Revenue per sale and product performance across sales (the Statystyki tabs).</summary>
internal class SalesAnalyticsTests
{
    private TempDb _db = null!;
    private Product _apples = null!;
    private Product _beets = null!;
    private Client _anna = null!;
    private Client _bartek = null!;

    [SetUp]
    public void Setup()
    {
        _db = new TempDb();
        _apples = _db.AddProduct("Apples", "00", 2);
        _beets = _db.AddProduct("Beets", "01", 4);
        _anna = _db.AddClient("Anna");
        _bartek = _db.AddClient("Bartek");
    }

    [TearDown] public void TearDown() => _db.Dispose();

    private void Order(Client client, Product? product, string sale, string value, bool reserved, DateTime date)
    {
        var order = _db.AddOrder(client, product, sale, value, reserved);
        order.Date = date;
        _db.Repo.Update(order);
        _db.Repo.Save();
    }

    private (string jan, string feb, string mar) ThreeSales()
    {
        var jan = _db.AddSale("Styczeń");
        var feb = _db.AddSale("Luty");
        var mar = _db.AddSale("Marzec");

        // Jan: Anna orders 4 kg apples, pays 10 (=5 kg); Bartek orders and never comes
        Order(_anna, _apples, jan, "4", true, new DateTime(2025, 1, 10));
        Order(_anna, _apples, jan, "10", false, new DateTime(2025, 1, 11));
        Order(_bartek, _beets, jan, "3", true, new DateTime(2025, 1, 10));
        // Feb: both buy; Anna 20 (=10 kg apples), Bartek 8 (=2 kg beets)
        Order(_anna, _apples, feb, "8", true, new DateTime(2025, 2, 10));
        Order(_anna, _apples, feb, "20", false, new DateTime(2025, 2, 11));
        Order(_bartek, _beets, feb, "2", true, new DateTime(2025, 2, 10));
        Order(_bartek, _beets, feb, "8", false, new DateTime(2025, 2, 11));
        // Mar: only Anna, 5 (=2.5 kg), plus a comment line that must be ignored
        Order(_anna, _apples, mar, "2", true, new DateTime(2025, 3, 10));
        Order(_anna, _apples, mar, "5", false, new DateTime(2025, 3, 11));
        Order(_anna, null, mar, "bring a bag", true, new DateTime(2025, 3, 10));
        return (jan, feb, mar);
    }

    // ---------- revenue and trends ----------

    [Test]
    public void SaleTrends_EmptyDatabase_IsEmpty() =>
        Assert.That(SalesAnalytics.BuildSaleTrends(_db.Repo), Is.Empty);

    [Test]
    public void SaleTrends_RevenueClientsAndChange()
    {
        ThreeSales();
        _db.AddSale("Empty sale without any orders");

        var rows = SalesAnalytics.BuildSaleTrends(_db.Repo);

        Assert.That(rows.Select(x => x.SaleName), Is.EqualTo(new[] { "Styczeń", "Luty", "Marzec" }), "oldest first, empty sale left out");
        var jan = rows[0];
        var feb = rows[1];
        var mar = rows[2];
        Assert.Multiple(() =>
        {
            Assert.That(jan.Revenue, Is.EqualTo(10));
            Assert.That(jan.ClientsOrdered, Is.EqualTo(2));
            Assert.That(jan.ClientsBought, Is.EqualTo(1));
            Assert.That(jan.ClientsMissed, Is.EqualTo(1));
            Assert.That(jan.SoldKg, Is.EqualTo(5).Within(1e-9));
            Assert.That(jan.ChangePercent, Is.Null);
            Assert.That(jan.RevenueText, Is.EqualTo("10.00 zł"));

            Assert.That(feb.Revenue, Is.EqualTo(28));
            Assert.That(feb.ClientsBought, Is.EqualTo(2));
            Assert.That(feb.AverageBasket, Is.EqualTo(14));
            Assert.That(feb.SoldKg, Is.EqualTo(12).Within(1e-9));
            Assert.That(feb.ChangePercent, Is.EqualTo(180).Within(1e-9));
            Assert.That(feb.ChangeText, Is.EqualTo("+180%"));

            Assert.That(mar.Revenue, Is.EqualTo(5));
            Assert.That(mar.ClientsOrdered, Is.EqualTo(1));
            Assert.That(mar.ChangeText, Is.EqualTo("-82%"));
        });
    }

    [Test]
    public void SaleTrends_SaleWithOrdersOnly_HasNoRevenueAndNoChangeAfterIt()
    {
        var first = _db.AddSale("First");
        var second = _db.AddSale("Second");
        Order(_anna, _apples, first, "4", true, new DateTime(2025, 1, 1));
        Order(_anna, _apples, second, "4", true, new DateTime(2025, 2, 1));
        Order(_anna, _apples, second, "8", false, new DateTime(2025, 2, 2));

        var rows = SalesAnalytics.BuildSaleTrends(_db.Repo);

        Assert.That(rows[0].Revenue, Is.EqualTo(0));
        Assert.That(rows[0].BasketText, Is.EqualTo("-"));
        Assert.That(rows[1].ChangePercent, Is.Null, "cannot compare with a sale that earned nothing");
        Assert.That(rows[1].ChangeText, Is.EqualTo("-"));
    }

    // ---------- product performance ----------

    [Test]
    public void ProductPerformance_TotalsAccuracyAndSuggestion()
    {
        ThreeSales();

        var rows = SalesAnalytics.BuildProductPerformance(_db.Repo, 0);

        Assert.That(rows.Select(x => x.Name), Is.EqualTo(new[] { "Apples", "Beets" }), "best seller first");
        var apples = rows[0];
        Assert.Multiple(() =>
        {
            Assert.That(apples.ActiveSales, Is.EqualTo(3));
            Assert.That(apples.OrderedKg, Is.EqualTo(14));
            Assert.That(apples.SoldKg, Is.EqualTo(17.5).Within(1e-9));
            Assert.That(apples.AccuracyPercent, Is.EqualTo(25).Within(1e-9));
            Assert.That(apples.AccuracyText, Is.EqualTo("+25.0%"));
            Assert.That(apples.LastSoldKg, Is.EqualTo(2.5).Within(1e-9));
            // per sale the larger of ordered/sold: 5, 10, 2.5 -> average 5.83 -> rounded up to 6
            Assert.That(apples.SuggestedKg, Is.EqualTo(6));
            Assert.That(apples.Trend, Is.EqualTo("maleje"), "2.5 kg after an average of 7.5");
        });

        var beets = rows[1];
        Assert.Multiple(() =>
        {
            Assert.That(beets.ActiveSales, Is.EqualTo(2));
            Assert.That(beets.OrderedKg, Is.EqualTo(5));
            Assert.That(beets.SoldKg, Is.EqualTo(2).Within(1e-9));
            Assert.That(beets.AccuracyPercent, Is.EqualTo(-60).Within(1e-9));
            Assert.That(beets.Trend, Is.EqualTo("rośnie"), "Jan sold nothing, Feb sold 2 kg");
        });
    }

    [Test]
    public void ProductPerformance_LastSalesLimitsTheRange()
    {
        ThreeSales();

        var rows = SalesAnalytics.BuildProductPerformance(_db.Repo, 2);

        var apples = rows.Single(x => x.Name == "Apples");
        Assert.That(apples.ActiveSales, Is.EqualTo(2));
        Assert.That(apples.SoldKg, Is.EqualTo(12.5).Within(1e-9));
        Assert.That(apples.OrderedKg, Is.EqualTo(10));
    }

    [Test]
    public void ProductPerformance_ProductNeverUsed_IsLeftOut_AndNothingOrderedHasNoAccuracy()
    {
        var sale = _db.AddSale("Only");
        Order(_anna, _apples, sale, "6", false, new DateTime(2025, 1, 1));

        var rows = SalesAnalytics.BuildProductPerformance(_db.Repo, 0);

        Assert.That(rows.Select(x => x.Name), Is.EqualTo(new[] { "Apples" }));
        Assert.That(rows[0].AccuracyPercent, Is.Null);
        Assert.That(rows[0].AccuracyText, Is.EqualTo("-"));
        Assert.That(rows[0].Trend, Is.EqualTo(""), "a single sale has no trend");
    }

    [Test]
    public void ProductPerformance_ProductWithoutPrice_DoesNotDivideByZero()
    {
        var free = _db.AddProduct("Free", "02", 0);
        var sale = _db.AddSale("Only");
        Order(_anna, free, sale, "3", true, new DateTime(2025, 1, 1));
        Order(_anna, free, sale, "5", false, new DateTime(2025, 1, 2));

        var row = SalesAnalytics.BuildProductPerformance(_db.Repo, 0).Single();

        Assert.That(row.SoldKg, Is.EqualTo(0));
        Assert.That(row.OrderedKg, Is.EqualTo(3));
    }

    // ---------- view model ----------

    [Test]
    public void StatisticsViewModel_FillsBothTabs()
    {
        ThreeSales();

        var vm = StatisticsHelper.Open(_db.Repo);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSaleTrends, Is.True);
            Assert.That(vm.TrendPlot, Is.Not.Null);
            Assert.That(vm.TrendSummary, Does.Contain("3 sprzedaży").And.Contain("43.00 zł").And.Contain("Luty"));
            Assert.That(vm.SelectedPerformanceScope.LastSales, Is.EqualTo(5));
            Assert.That(vm.HasProductPerformance, Is.True);
        });

        vm.SelectedPerformanceScope = vm.PerformanceScopes.Single(x => x.LastSales == 3);
        Assert.That(vm.ProductPerformance.Single(x => x.Name == "Apples").ActiveSales, Is.EqualTo(3));
        vm.SelectedPerformanceScope = vm.PerformanceScopes.Single(x => x.LastSales == 0);
        Assert.That(vm.ProductPerformance.Count, Is.EqualTo(2));
    }

    [Test]
    public void StatisticsViewModel_NoData_ShowsEmptyStates()
    {
        var vm = StatisticsHelper.Open(_db.Repo);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSaleTrends, Is.False);
            Assert.That(vm.TrendPlot, Is.Null);
            Assert.That(vm.HasProductPerformance, Is.False);
        });
    }
}
