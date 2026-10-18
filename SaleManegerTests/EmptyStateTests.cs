using SaleManeger.Models;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Empty states and the "Wyczyść filtry" button.</summary>
internal class EmptyStateTests
{
    private TempDb _db = null!;
    private string _sale = null!;

    [SetUp]
    public void Setup()
    {
        _db = new TempDb();
        _sale = _db.AddSale();
    }

    [TearDown] public void TearDown() => _db.Dispose();

    [Test]
    public void ClientList_NoClientsAtAll_ExplainsHowToAddOne_WithoutAClearButton()
    {
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsListEmpty, Is.True);
            Assert.That(vm.EmptyListText, Does.Contain("Nie ma jeszcze żadnych klientów"));
            Assert.That(vm.CanClearFilters, Is.False);
        });
    }

    [Test]
    public void ClientList_FiltersHideEveryone_SaysSoAndOffersToClear()
    {
        _db.AddClient("Anna");
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        Assert.That(vm.IsListEmpty, Is.False);

        vm.ClientName = "zzz";

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsListEmpty, Is.True);
            Assert.That(vm.EmptyListText, Does.Contain("Żaden klient nie pasuje"));
            Assert.That(vm.CanClearFilters, Is.True);
        });
    }

    [Test]
    public void ClearFilters_ResetsSearchProductsFlagsRulesAndTags()
    {
        var apples = _db.AddProduct("Apples", "00");
        var anna = _db.AddClient("Anna", "111222333");
        var tracked = _db.Repo.GetAll<Client>().Single();
        tracked.Tags = "po terminie";
        _db.Repo.Update(tracked);
        _db.Repo.Save();
        _db.AddClient("Bartek", "444555666");

        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        vm.ClientName = "Anna";
        vm.Products.Single().IsReserved = true;
        vm.AreAllClientsShowing = false;
        vm.AreSmsPendingShowing = true;
        vm.AdvancedFilters.Add(new ClientFilterRule { ThresholdText = "5" });
        vm.TagFilterOptions.First(x => x.Name == "po terminie").IsSelected = true;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.IsListEmpty, Is.True);
        Assert.That(vm.CanClearFilters, Is.True);
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.ClearFiltersCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(vm.ClientName, Is.Null.Or.Empty);
            Assert.That(vm.Products.Any(x => x.IsReserved), Is.False);
            Assert.That(vm.AreAllClientsShowing, Is.True);
            Assert.That(vm.AreClientsWithOrderShowing, Is.True);
            Assert.That(vm.AreClientsWithSaleShowing, Is.False);
            Assert.That(vm.AreSmsPendingShowing, Is.False);
            Assert.That(vm.AdvancedFilters, Is.Empty);
            Assert.That(vm.SelectedTags, Is.Empty);
            Assert.That(vm.TagFilterOptions.Any(x => x.IsSelected), Is.False);
            Assert.That(vm.HasActiveFilters, Is.False);
            Assert.That(vm.CanClearFilters, Is.False);
            Assert.That(vm.Clients.Count, Is.EqualTo(2));
            Assert.That(vm.IsListEmpty, Is.False);
            Assert.That(changed, Does.Contain(nameof(ClientSelectionViewModel.AreAllClientsShowing)).And.Contain(nameof(ClientSelectionViewModel.ClientName)),
                "the flyout checkboxes and search box have to be told");
        });
    }

    [Test]
    public void Products_EmptyListShowsHint_AndHintGoesWhenOneIsAdded()
    {
        var vm = new ProductEditionViewModel(_db.Repo);
        Assert.That(vm.HasNoProducts, Is.True);

        vm.AddProduct();
        Assert.That(vm.HasNoProducts, Is.False);

        vm.Products.Clear();
        Assert.That(vm.HasNoProducts, Is.True);
    }

    [Test]
    public void SaleList_NoSalesShowsHint()
    {
        using var empty = new TempDb();
        Assert.That(new SaleSelectionViewModel(empty.Repo).HasNoSales, Is.True);
        Assert.That(new SaleSelectionViewModel(_db.Repo).HasNoSales, Is.False);
    }
}
