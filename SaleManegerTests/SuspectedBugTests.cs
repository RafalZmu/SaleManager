using SaleManeger.Models;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>
/// Tests that describe the CORRECT behaviour for problems found while reading the code.
/// They currently fail against the app; they are tagged so they can be excluded from a green run with
/// <c>--filter "TestCategory!=SuspectedBug"</c> until the app is fixed.
/// </summary>
[Category("SuspectedBug")]
internal class SuspectedBugTests
{
    private TempDb _db = null!;

    [SetUp] public void Setup() => _db = new TempDb();
    [TearDown] public void TearDown() => _db.Dispose();

    /// <summary>The edit screen accepts "2,5" (it even replaces the comma for the sum), and existing data contains
    /// values like "2,5 komentarz", but the summary parses with en-US where "," is a thousands separator => 25.</summary>
    [Test]
    public void SaleSummary_CommaDecimalOrder_IsNotMultipliedByTen()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient(), apples, sale, "2,5", true);

        var vm = new SaleSummaryViewModel(_db.Repo, sale);

        Assert.That(double.Parse(vm.SummaryItems.Single().AllOrders, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(2.5));
    }

    [Test]
    public void SaleSummary_CommaDecimal_FromEditScreenText_RoundTrips()
    {
        var sale = _db.AddSale();
        _db.AddProduct("Apples", "00");
        var client = _db.AddClient();
        var vm = new ClientEditionViewModel(_db.Repo, client, sale);
        vm.Order = "Apples: 2,5";
        vm.OpenClientSelectionCommand.Execute().Subscribe();

        var summary = new SaleSummaryViewModel(_db.Repo, sale);

        Assert.That(double.Parse(summary.SummaryItems.Single().AllOrders, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(2.5));
    }

    /// <summary>DeleteClient removes the client but only the orders of the current sale, leaving orders of other
    /// sales pointing at a client that no longer exists. Opening such a sale then crashes in ClientSelectionViewModel.</summary>
    [Test]
    public void DeleteClient_DoesNotBreakOtherSalesThatHadOrdersFromThatClient()
    {
        var sale1 = _db.AddSale("one");
        var sale2 = _db.AddSale("two");
        var apples = _db.AddProduct("Apples", "00");
        var client = _db.AddClient();
        _db.AddOrder(client, apples, sale1, "1", true);
        _db.AddOrder(client, apples, sale2, "1", true);

        ClientDeletionConfirmationViewModel.DeleteClient(_db.Repo, client.ID, sale1);

        Assert.DoesNotThrow(() => new ClientSelectionViewModel(sale2, _db.Repo, null));
    }

    /// <summary>Removing a product on the product screen leaves orders that reference it; opening a sale
    /// with such an order then throws a NullReferenceException.</summary>
    [Test]
    public void RemovingProduct_DoesNotBreakClientSelectionOfSalesThatOrderedIt()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var beets = _db.AddProduct("Beets", "01");
        _db.AddOrder(_db.AddClient(), apples, sale, "1", true);

        var vm = new ProductEditionViewModel(_db.Repo);
        vm.SaveToDataBase(_db.Repo, new System.Collections.ObjectModel.ObservableCollection<Product> { beets });

        Assert.DoesNotThrow(() => new ClientSelectionViewModel(sale, _db.Repo, null));
    }

    /// <summary>Money for a sale with decimal values ("12.5") must not crash the all-sales summary.</summary>
    [Test]
    public void AllSalesSummary_DecimalSoldValue_DoesNotThrow()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient(), apples, sale, "12.5", false);

        Assert.DoesNotThrow(() => new AllSalesSummaryViewModel(_db.Repo));
    }

    /// <summary>The per-sale profit must only count that sale's own clients/products and each product once.</summary>
    [Test]
    public void AllSalesSummary_NumberOfClients_IsPerSale()
    {
        var sale1 = _db.AddSale("one");
        var sale2 = _db.AddSale("two");
        var apples = _db.AddProduct("Apples", "00");
        var a = _db.AddClient("A");
        var b = _db.AddClient("B");
        _db.AddOrder(a, apples, sale1, "10", false);
        _db.AddOrder(b, apples, sale2, "10", false);
        _db.AddOrder(a, apples, sale2, "10", false);

        var vm = new AllSalesSummaryViewModel(_db.Repo);

        // sale1 had 1 buying client, sale2 had 2: the number-of-clients series must reflect that.
        var points = vm.NumberOfCliensSeries.ItemsSource as IEnumerable<OxyPlot.DataPoint>;
        Assert.That(points!.Select(p => p.Y), Is.EqualTo(new[] { 1.0, 2.0 }));
    }
}
