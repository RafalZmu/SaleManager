using System.Diagnostics;
using SaleManeger.Models;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Statystyki must stay usable on a database with years of sales.</summary>
internal class StatisticsPerformanceTests
{
    private TempDb _db = null!;

    [SetUp] public void Setup() => _db = new TempDb();

    [TearDown] public void TearDown() => _db.Dispose();

    private int Fill(int sales, int clients, int products, int clientsPerSale, int productsPerClient)
    {
        var rnd = new Random(1);
        var productList = Enumerable.Range(0, products)
            .Select(i => new Product { ID = Guid.NewGuid().ToString(), Code = i.ToString("00"), Name = "Produkt " + i, PricePerKg = 2 + i % 7 })
            .ToList();
        var clientList = Enumerable.Range(0, clients)
            .Select(i => new Client { ID = Guid.NewGuid().ToString(), Name = "Klient " + i, PhoneNumber = "5000000" + i, Products = new() })
            .ToList();
        foreach (var p in productList) _db.Repo.Add(p);
        foreach (var c in clientList) _db.Repo.Add(c);

        int orders = 0;
        for (int s = 0; s < sales; s++)
        {
            var sale = new Sale { SaleID = Guid.NewGuid().ToString(), SaleName = "Sprzedaż " + s };
            _db.Repo.Add(sale);
            var day = new DateTime(2020, 1, 1).AddDays(s * 7);
            foreach (var c in clientList.OrderBy(_ => rnd.Next()).Take(clientsPerSale))
            {
                foreach (var p in productList.OrderBy(_ => rnd.Next()).Take(productsPerClient))
                {
                    _db.Repo.Add(new ClientOrder { ClientOrderID = Guid.NewGuid().ToString(), ClientID = c.ID, ProductID = p.ID, SaleID = sale.SaleID, Value = (1 + rnd.Next(8)).ToString(), IsReserved = true, Date = day });
                    _db.Repo.Add(new ClientOrder { ClientOrderID = Guid.NewGuid().ToString(), ClientID = c.ID, ProductID = p.ID, SaleID = sale.SaleID, Value = (5 + rnd.Next(40)).ToString(), IsReserved = false, Date = day.AddDays(1) });
                    orders += 2;
                }
            }
        }
        _db.Repo.Save();
        return orders;
    }

    [Test]
    public void OpeningStatisticsOnLargeDatabaseIsFast()
    {
        var orders = Fill(sales: 120, clients: 300, products: 40, clientsPerSale: 150, productsPerClient: 6);
        var timer = Stopwatch.StartNew();
        var vm = StatisticsHelper.Open(_db.Repo);
        var opening = timer.ElapsedMilliseconds;
        timer.Restart();
        vm.SelectedSaleScope = vm.SaleScopes[5];
        var switchSale = timer.ElapsedMilliseconds;
        timer.Restart();
        vm.SelectedPerformanceScope = vm.PerformanceScopes[3];
        var allSales = timer.ElapsedMilliseconds;

        TestContext.Out.WriteLine($"{orders} orders: open {opening} ms, switch sale {switchSale} ms, all sales {allSales} ms");
        Assert.That(vm.SaleTrends.Count, Is.EqualTo(120));
        Assert.That(opening, Is.LessThan(10000));
        Assert.That(switchSale, Is.LessThan(500));
        Assert.That(allSales, Is.LessThan(1500));
    }
}
