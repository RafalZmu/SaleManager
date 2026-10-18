using SaleManeger.Models;
using SaleManeger.ViewModels;
using System.Collections.ObjectModel;

namespace SaleManegerTests;

/// <summary>Parsing/saving of the free-text order box and the client-edit view model rules.</summary>
internal class OrderTextTests
{
    private TempDb _db = null!;

    [SetUp] public void Setup() => _db = new TempDb();
    [TearDown] public void TearDown() => _db.Dispose();

    private static List<Product> Catalog() => new()
    {
        new Product { ID = "id-a", Name = "Apples", Code = "00", PricePerKg = 2 },
        new Product { ID = "id-b", Name = "Beets", Code = "01", PricePerKg = 3 },
    };

    // ---------- GetProductsFromText ----------

    [Test]
    public void GetProductsFromText_EmptyOrWhitespace_ReturnsNothing()
    {
        Assert.That(ClientEditionViewModel.GetProductsFromText(Catalog(), "", true), Is.Empty);
        Assert.That(ClientEditionViewModel.GetProductsFromText(Catalog(), "  \n ", true), Is.Empty);
        Assert.That(ClientEditionViewModel.GetProductsFromText(Catalog(), null!, true), Is.Empty);
    }

    [Test]
    public void GetProductsFromText_ParsesProductsCommentsAndReservedFlag()
    {
        var result = ClientEditionViewModel.GetProductsFromText(Catalog(), "Apples: 3\nbring a bag\nBeets: 1.5 kg", false);

        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0], Has.Property("ID").EqualTo("id-a").And.Property("Value").EqualTo("3").And.Property("IsReserved").False);
        Assert.That(result[1], Has.Property("ID").EqualTo("").And.Property("Value").EqualTo("bring a bag"));
        Assert.That(result[2], Has.Property("ID").EqualTo("id-b").And.Property("Value").EqualTo("1.5 kg"));
    }

    [Test]
    public void GetProductsFromText_NonNumericWeight_BecomesZero()
    {
        var result = ClientEditionViewModel.GetProductsFromText(Catalog(), "Apples: abc", true);
        Assert.That(result.Single().Value, Is.EqualTo("0"));
    }

    [Test]
    public void GetProductsFromText_UnknownProduct_Throws()
    {
        // Documents current behaviour: the UI relies on the user only typing known product names.
        Assert.Throws<InvalidOperationException>(() =>
            ClientEditionViewModel.GetProductsFromText(Catalog(), "Nonexistent: 3", true));
    }

    // ---------- SaveClientOrder ----------

    [Test]
    public void SaveClientOrder_NormalisesValuesAndMapsComments()
    {
        var sale = _db.AddSale();
        var client = _db.AddClient();
        client.Products = new ObservableCollection<Product>
        {
            new Product { ID = "id-a", Value = "  5kg", IsReserved = true },
            new Product { ID = "", Value = "call me", IsReserved = true },
        };

        ClientEditionViewModel.SaveClientOrder(_db.Repo, client, sale);

        var orders = _db.Repo.GetAll<ClientOrder>().Where(x => x.ClientID == client.ID).ToList();
        Assert.That(orders.Single(x => x.ProductID == "id-a").Value, Is.EqualTo("5 kg"));
        Assert.That(orders.Single(x => x.ProductID == "Comment").Value, Is.EqualTo("call me"));
    }

    [Test]
    public void SaveClientOrder_ReplacesOnlyOrdersOfThatSale()
    {
        var sale1 = _db.AddSale("one");
        var sale2 = _db.AddSale("two");
        var apples = _db.AddProduct("Apples", "00");
        var client = _db.AddClient();
        _db.AddOrder(client, apples, sale1, "1", true);
        _db.AddOrder(client, apples, sale2, "9", true);

        client.Products = new ObservableCollection<Product> { new Product { ID = apples.ID, Value = "2", IsReserved = true } };
        ClientEditionViewModel.SaveClientOrder(_db.Repo, client, sale1);

        var orders = _db.Repo.GetAll<ClientOrder>().ToList();
        Assert.That(orders.Where(x => x.SaleID == sale1).Select(x => x.Value), Is.EqualTo(new[] { "2" }));
        Assert.That(orders.Where(x => x.SaleID == sale2).Select(x => x.Value), Is.EqualTo(new[] { "9" }));
    }

    [Test]
    public void SaveClientOrder_EmptyProducts_RemovesAllOrdersOfClientInSale()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var client = _db.AddClient();
        _db.AddOrder(client, apples, sale, "1", true);

        client.Products = new ObservableCollection<Product>();
        ClientEditionViewModel.SaveClientOrder(_db.Repo, client, sale);

        Assert.That(_db.Repo.GetAll<ClientOrder>().Any(), Is.False);
    }

    // ---------- ClientEditionViewModel ----------

    private ClientEditionViewModel NewEditVm(out Client client, out string sale)
    {
        sale = _db.AddSale();
        _db.AddProduct("Apples", "00", 2);
        _db.AddProduct("Beets", "01", 3);
        client = _db.AddClient("Jan", "123");
        return new ClientEditionViewModel(_db.Repo, client, sale);
    }

    private static string? Close(ClientEditionViewModel vm)
    {
        string? result = null;
        vm.OpenClientSelectionCommand.Execute().Subscribe(r => result = r);
        return result;
    }

    [Test]
    public void EditVm_SaleSum_AddsUpLinesAndComputesChange()
    {
        var vm = NewEditVm(out _, out _);
        vm.Sale = "Apples: 10\nBeets: 15,5\n";

        Assert.That(vm.SaleSum, Does.Contain("Suma: 25.5"));
        Assert.That(vm.SaleSum, Does.Contain("Reszta z 100: 74.5"));
        Assert.That(vm.SaleSum, Does.Contain("Reszta z 50: 24.5"));
    }

    [Test]
    public void EditVm_SaleSum_CommentLineSplitsIntoSections()
    {
        var vm = NewEditVm(out _, out _);
        vm.Sale = "Apples: 10\nnext person\nBeets: 5";

        Assert.That(vm.SaleSum, Does.StartWith("10 + 5"));
        Assert.That(vm.SaleSum, Does.Contain("Suma: 15"));
    }

    [Test]
    public void EditVm_Constructor_SplitsClientProductsIntoOrderAndSaleText()
    {
        var sale = _db.AddSale();
        var client = _db.AddClient();
        client.Products = new ObservableCollection<Product>
        {
            new Product { Name = "Apples", Value = "3", IsReserved = true },
            new Product { Name = "Beets", Value = "4", IsReserved = false },
        };
        _db.AddProduct("Apples", "00");
        _db.AddProduct("Beets", "01");

        var vm = new ClientEditionViewModel(_db.Repo, client, sale);

        Assert.That(vm.Order, Does.Contain("Apples: 3"));
        Assert.That(vm.Sale, Does.Contain("Beets: 4"));
    }

    [Test]
    public void EditVm_Close_WithBadWeight_ShowsWarningAndDoesNotSave()
    {
        var vm = NewEditVm(out var client, out _);
        vm.Order = "Apples: abc";

        var result = Close(vm);

        Assert.That(result, Is.EqualTo(string.Empty));
        Assert.That(vm.WarningMessage, Does.Contain("abc"));
        Assert.That(_db.Repo.GetAll<ClientOrder>().Any(), Is.False);
    }

    [Test]
    public void EditVm_Close_WithMissingWeight_ShowsWarning()
    {
        var vm = NewEditVm(out _, out _);
        vm.Order = "Apples:   ";

        Assert.That(Close(vm), Is.EqualTo(string.Empty));
        Assert.That(vm.WarningMessage, Does.Contain("Apples"));
    }

    [Test]
    public void EditVm_Close_SavesClientOrdersAndFirstOrderTime()
    {
        var vm = NewEditVm(out var client, out var sale);
        vm.Order = "Apples: 3";
        vm.Sale = "Beets: 4";

        var result = Close(vm);

        Assert.That(result, Is.EqualTo(sale));
        var orders = _db.Repo.GetAll<ClientOrder>().Where(x => x.ClientID == client.ID).ToList();
        Assert.That(orders.Count, Is.EqualTo(2));
        Assert.That(orders.Count(x => x.IsReserved), Is.EqualTo(1));
        var info = _db.Repo.GetAll<ClientSaleInfo>().Single(x => x.ClientID == client.ID && x.SaleID == sale);
        Assert.That(info.FirstOrderTime, Is.Not.Null);
        Assert.That(info.FirstPurchaseTime, Is.Not.Null);
    }

    [Test]
    public void EditVm_Close_ReopenKeepsFirstOrderTime()
    {
        var vm = NewEditVm(out var client, out var sale);
        vm.Order = "Apples: 3";
        Close(vm);
        var first = _db.Repo.GetAll<ClientSaleInfo>().Single().FirstOrderTime;

        var again = new ClientEditionViewModel(_db.Repo, client, sale);
        again.Order = "Apples: 5";
        Close(again);

        var infos = _db.Repo.GetAll<ClientSaleInfo>().ToList();
        Assert.That(infos.Count, Is.EqualTo(1));
        Assert.That(infos[0].FirstOrderTime, Is.EqualTo(first));
    }

    [Test]
    public void EditVm_Close_SameArrivalTimeAsOtherClient_WarnsOnceThenSaves()
    {
        var vm = NewEditVm(out var client, out var sale);
        var arrival = new DateTime(2030, 1, 1, 10, 0, 0);
        var other = _db.AddClient("Other", "999");
        _db.Repo.Add(new ClientSaleInfo { ID = "x", ClientID = other.ID, SaleID = sale, ExpectedArrivalTime = arrival });
        _db.Repo.Save();

        vm.Order = "Apples: 3";
        vm.ExpectedArrivalDate = new DateTimeOffset(arrival.Date);
        vm.ExpectedArrivalTime = arrival.TimeOfDay;

        Assert.That(Close(vm), Is.EqualTo(string.Empty));
        Assert.That(vm.WarningMessage, Does.Contain("tę samą godzinę"));
        Assert.That(_db.Repo.GetAll<ClientOrder>().Any(), Is.False);

        Assert.That(Close(vm), Is.EqualTo(sale), "second click forces the save");
        Assert.That(_db.Repo.GetAll<ClientSaleInfo>().Single(x => x.ClientID == client.ID).ExpectedArrivalTime, Is.EqualTo(arrival));
    }

    [Test]
    public void EditVm_FreeStock_ReflectsAvailableMinusOrdered()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var other = _db.AddClient("Other");
        _db.AddOrder(other, apples, sale, "4", true);
        _db.Repo.Add(new SaleProduct { ID = "sp", ProductID = apples.ID, ProductCode = "00", ProductName = "Apples", SaleID = sale, Amount = 10, PricePerKg = 1 });
        _db.Repo.Save();

        var vm = new ClientEditionViewModel(_db.Repo, _db.AddClient("Me"), sale);

        Assert.That(vm.Codes, Does.Contain("00-Apples: Wolne = [6]"));
        Assert.That(vm.LowStockProductIDs, Is.Empty);

        vm.ToggleDisplayModeCommand.Execute().Subscribe();
        Assert.That(vm.Codes, Does.Contain("Zamówione = [4]"));
    }

    [Test]
    public void EditVm_FreeStock_FlagsLowStockWhenOverOrdered()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient("Other"), apples, sale, "12", true);
        _db.Repo.Add(new SaleProduct { ID = "sp", ProductID = apples.ID, ProductCode = "00", ProductName = "Apples", SaleID = sale, Amount = 10, PricePerKg = 1 });
        _db.Repo.Save();

        var vm = new ClientEditionViewModel(_db.Repo, _db.AddClient("Me"), sale);

        Assert.That(vm.LowStockProductIDs, Does.Contain(apples.ID));
        Assert.That(vm.Codes, Does.Contain("Wolne = [-2]"));
    }
}
