using SaleManeger.Models;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>The per-client history screen: sales, money and favourite products.</summary>
internal class ClientHistoryTests
{
    private TempDb _db = null!;

    [SetUp] public void Setup() => _db = new TempDb();
    [TearDown] public void TearDown() => _db.Dispose();

    private void Order(Client client, Product? product, string sale, string value, bool reserved, DateTime date)
    {
        var order = _db.AddOrder(client, product, sale, value, reserved);
        order.Date = date;
        _db.Repo.Update(order);
        _db.Repo.Save();
    }

    [Test]
    public void NewClient_HasEmptyHistory()
    {
        var sale = _db.AddSale("Current");
        var client = _db.AddClient("Anna", "500600700");

        var vm = new ClientHistoryViewModel(_db.Repo, client.ID, sale);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasHistory, Is.False);
            Assert.That(vm.ClientName, Is.EqualTo("Anna"));
            Assert.That(vm.TotalSpent, Is.EqualTo(0));
            Assert.That(vm.AverageSpentText, Is.EqualTo("-"));
            Assert.That(vm.LastVisitText, Is.EqualTo("-"));
            Assert.That(vm.FavouriteProducts, Is.Empty);
        });
    }

    [Test]
    public void History_SummarisesSalesMoneyMissedOrdersAndFavourites()
    {
        var apples = _db.AddProduct("Apples", "00", 2);
        var beets = _db.AddProduct("Beets", "01", 3);
        var old = _db.AddSale("Old");
        var middle = _db.AddSale("Middle");
        var missed = _db.AddSale("Missed");
        var current = _db.AddSale("Current");
        var client = _db.AddClient("Anna", "500600700");
        var other = _db.AddClient("Other");

        // Old: ordered 3 kg apples (=6 zl), bought for 7,5
        Order(client, apples, old, "3", true, new DateTime(2025, 1, 10));
        Order(client, apples, old, "7,5", false, new DateTime(2025, 1, 11));
        // Middle: ordered apples and beets, bought apples for 10
        Order(client, apples, middle, "2 kg", true, new DateTime(2025, 3, 1));
        Order(client, beets, middle, "1.5", true, new DateTime(2025, 3, 1));
        Order(client, apples, middle, "10", false, new DateTime(2025, 3, 2));
        // Missed: ordered, never bought
        Order(client, beets, missed, "4", true, new DateTime(2025, 4, 1));
        // Current: ordered, still waiting; plus a comment that must not count
        Order(client, beets, current, "2", true, new DateTime(2025, 5, 1));
        Order(client, null, current, "bring a bag", true, new DateTime(2025, 5, 1));
        // someone else's data must not leak in
        Order(other, apples, old, "99", false, new DateTime(2025, 1, 10));

        var vm = new ClientHistoryViewModel(_db.Repo, client.ID, current);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Sales.Select(x => x.SaleName), Is.EqualTo(new[] { "Current", "Missed", "Middle", "Old" }), "newest first");
            Assert.That(vm.SalesWithPurchase, Is.EqualTo(2));
            Assert.That(vm.SalesWithOrder, Is.EqualTo(4));
            Assert.That(vm.TotalSpent, Is.EqualTo(17.5));
            Assert.That(vm.AverageSpent, Is.EqualTo(8.75));
            Assert.That(vm.TotalSpentText, Is.EqualTo("17.50 zł"));
            Assert.That(vm.VisitsText, Is.EqualTo("2 z 4"));
            Assert.That(vm.MissedSales, Is.EqualTo(1), "the sale in progress is not a missed one");
            Assert.That(vm.LastVisit, Is.EqualTo(new DateTime(2025, 3, 2)));
        });

        var byName = vm.Sales.ToDictionary(x => x.SaleName);
        Assert.Multiple(() =>
        {
            Assert.That(byName["Old"].Status, Is.EqualTo("Odebrano"));
            Assert.That(byName["Old"].OrderedText, Is.EqualTo("3 kg (ok. 6.00 zł)"));
            Assert.That(byName["Old"].BoughtText, Is.EqualTo("7.50 zł"));
            Assert.That(byName["Middle"].OrderedText, Is.EqualTo("3.5 kg (ok. 8.50 zł)"));
            Assert.That(byName["Missed"].Status, Is.EqualTo("Nie odebrał"));
            Assert.That(byName["Missed"].BoughtText, Is.EqualTo("-"));
            Assert.That(byName["Current"].Status, Is.EqualTo("Zamówienie czeka"));
        });

        var favourites = vm.FavouriteProducts;
        Assert.Multiple(() =>
        {
            Assert.That(favourites.Select(x => x.Name), Is.EqualTo(new[] { "Apples", "Beets" }).Or.EqualTo(new[] { "Beets", "Apples" }));
            var a = favourites.Single(x => x.Name == "Apples");
            Assert.That(a.Sales, Is.EqualTo(2));
            Assert.That(a.OrderedKg, Is.EqualTo(5));
            Assert.That(a.Spent, Is.EqualTo(17.5));
            var b = favourites.Single(x => x.Name == "Beets");
            Assert.That(b.Sales, Is.EqualTo(3));
            Assert.That(b.OrderedKg, Is.EqualTo(7.5));
        });
        Assert.That(favourites[0].Name, Is.EqualTo("Beets"), "most sales first");
    }

    [Test]
    public void History_IgnoresOrdersOfDeletedSalesAndProducts()
    {
        var apples = _db.AddProduct("Apples", "00", 2);
        var ghost = _db.AddProduct("Ghost", "01", 1);
        var sale = _db.AddSale("Sale");
        var client = _db.AddClient("Anna");
        Order(client, apples, sale, "1", true, DateTime.Now);
        Order(client, ghost, sale, "1", true, DateTime.Now);
        Order(client, apples, "deleted-sale", "5", false, DateTime.Now);
        _db.Repo.Delete(_db.Repo.GetAll<Product>().Single(x => x.ID == ghost.ID));
        _db.Repo.Save();

        var vm = new ClientHistoryViewModel(_db.Repo, client.ID, sale);

        Assert.That(vm.Sales.Select(x => x.SaleName), Is.EqualTo(new[] { "Sale" }));
        Assert.That(vm.FavouriteProducts.Select(x => x.Name), Is.EqualTo(new[] { "Apples" }));
        Assert.That(vm.TotalSpent, Is.EqualTo(0));
    }

    [Test]
    public void ReturnCommand_GoesBackToTheCurrentSale()
    {
        var sale = _db.AddSale("Current");
        var client = _db.AddClient("Anna");
        var vm = new ClientHistoryViewModel(_db.Repo, client.ID, sale);
        string? result = null;

        vm.ReturnCommand.Execute().Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(sale));
    }

    [Test]
    public void ClientList_HistoryCommand_EmitsTheClientId()
    {
        var sale = _db.AddSale("Current");
        var client = _db.AddClient("Anna");
        var list = new ClientSelectionViewModel(sale, _db.Repo, null);
        string? result = null;

        list.OpenClientHistoryCommand.Execute(client.ID).Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(client.ID));
    }

    [Test]
    public void EditScreen_HistoryCommand_SavesFirstAndEmitsTheClientId()
    {
        var sale = _db.AddSale("Current");
        _db.AddProduct("Apples", "00", 2);
        var client = _db.AddClient("Anna");
        var edit = new ClientEditionViewModel(_db.Repo, client, sale);
        edit.Order = "Apples: 3";
        string? result = null;

        edit.OpenHistoryCommand.Execute().Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(client.ID));
        Assert.That(_db.Repo.GetAll<ClientOrder>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void EditScreen_HistoryCommand_StaysWhenTheOrderIsInvalid()
    {
        var sale = _db.AddSale("Current");
        _db.AddProduct("Apples", "00", 2);
        var client = _db.AddClient("Anna");
        var edit = new ClientEditionViewModel(_db.Repo, client, sale);
        edit.Order = "Apples:";
        string? result = null;

        edit.OpenHistoryCommand.Execute().Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(string.Empty));
    }
}
