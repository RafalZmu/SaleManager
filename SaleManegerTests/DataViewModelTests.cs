using SaleManeger.Models;
using SaleManeger.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SaleManegerTests;

/// <summary>Product editing, sale stock, summary, statistics, client list and planning logic.</summary>
internal class DataViewModelTests
{
    private TempDb _db = null!;

    [SetUp] public void Setup() => _db = new TempDb();
    [TearDown] public void TearDown() => _db.Dispose();

    // ---------- ProductEditionViewModel ----------

    [Test]
    public void ProductEdition_Save_DropsBlankRowsAndFixesPrice()
    {
        var vm = new ProductEditionViewModel(_db.Repo);
        var list = new ObservableCollection<Product>
        {
            new Product { ID = "1", Name = "Apples", Code = "00", PricePerKg = 0 },
            new Product { ID = "2", Name = "", Code = "01", PricePerKg = 5 },
            new Product { ID = "3", Name = "NoCode", Code = " ", PricePerKg = 5 },
            new Product { ID = "4", Name = "Beets", Code = "02", PricePerKg = -3 },
        };

        vm.SaveToDataBase(_db.Repo, list);

        var saved = _db.Repo.GetAll<Product>().OrderBy(x => x.Code).ToList();
        Assert.That(saved.Select(x => x.Name), Is.EqualTo(new[] { "Apples", "Beets" }));
        Assert.That(saved.All(x => x.PricePerKg == 1), Is.True);
    }

    [Test]
    public void ProductEdition_Save_DuplicateCodesGetUniqueCodes()
    {
        var vm = new ProductEditionViewModel(_db.Repo);
        var list = new ObservableCollection<Product>
        {
            new Product { ID = "1", Name = "Apples", Code = "00", PricePerKg = 1 },
            new Product { ID = "2", Name = "Beets", Code = "00", PricePerKg = 1 },
        };

        vm.SaveToDataBase(_db.Repo, list);

        var codes = _db.Repo.GetAll<Product>().Select(x => x.Code).ToList();
        Assert.That(codes.Count, Is.EqualTo(2));
        Assert.That(codes.Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void ProductEdition_Save_ReplacesPreviousProducts()
    {
        _db.AddProduct("Old", "99");
        var vm = new ProductEditionViewModel(_db.Repo);
        vm.AddProduct();
        Assert.That(vm.Products.Count, Is.EqualTo(2));

        vm.SaveToDataBase(_db.Repo, new ObservableCollection<Product> { new Product { ID = "n", Name = "New", Code = "01", PricePerKg = 2 } });

        Assert.That(_db.Repo.GetAll<Product>().Select(x => x.Name), Is.EqualTo(new[] { "New" }));
    }

    // ---------- CurrentProductStateViewModel ----------

    [Test]
    public void CreateSaleItems_CreatesOneZeroItemPerProduct_AndIsIdempotent()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00", 2);
        _db.AddProduct("Beets", "01", 3);

        var vm = new CurrentProductStateViewModel(_db.Repo, sale);
        Assert.That(vm.saleProducts.Count, Is.EqualTo(2));
        Assert.That(vm.saleProducts.All(x => x.Amount == 0 && x.SaleID == sale), Is.True);
        Assert.That(vm.saleProducts.First(x => x.ProductID == apples.ID).PricePerKg, Is.EqualTo(2));

        var again = new CurrentProductStateViewModel(_db.Repo, sale);
        Assert.That(again.saleProducts.Count, Is.EqualTo(2));
        Assert.That(_db.Repo.GetAll<SaleProduct>().Count(), Is.EqualTo(2));
    }

    [Test]
    public void CreateSaleItems_NewProductAddedLater_GetsItsOwnSaleItem()
    {
        var sale = _db.AddSale();
        _db.AddProduct("Apples", "00");
        _ = new CurrentProductStateViewModel(_db.Repo, sale);

        _db.AddProduct("Beets", "01");
        var vm = new CurrentProductStateViewModel(_db.Repo, sale);

        Assert.That(vm.saleProducts.Select(x => x.ProductCode), Is.EqualTo(new[] { "00", "01" }));
    }

    [Test]
    public void CurrentProductState_SavingKeepsEnteredAmounts()
    {
        var sale = _db.AddSale();
        _db.AddProduct("Apples", "00");
        var vm = new CurrentProductStateViewModel(_db.Repo, sale);
        vm.saleProducts[0].Amount = 42;

        vm.OpenMoreSettingsCommand.Execute().Subscribe();

        Assert.That(_db.Repo.GetAll<SaleProduct>().Single().Amount, Is.EqualTo(42));
    }

    // ---------- Deletion ----------

    [Test]
    public void DeleteClient_RemovesClientAndOrdersOfThatSale()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var keep = _db.AddClient("Keep");
        var gone = _db.AddClient("Gone");
        _db.AddOrder(keep, apples, sale, "1", true);
        _db.AddOrder(gone, apples, sale, "2", true);

        ClientDeletionConfirmationViewModel.DeleteClient(_db.Repo, gone.ID, sale);

        Assert.That(_db.Repo.GetAll<Client>().Select(x => x.Name), Is.EqualTo(new[] { "Keep" }));
        Assert.That(_db.Repo.GetAll<ClientOrder>().Select(x => x.ClientID), Is.EqualTo(new[] { keep.ID }));
    }

    // ---------- SaleSummaryViewModel ----------

    [Test]
    public void GetSumOfOrdersLeft_IgnoresClientsWhoAlreadyBoughtAndComments()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var waiting = _db.AddClient("Waiting");
        var bought = _db.AddClient("Bought");
        _db.AddOrder(waiting, apples, sale, "3 kg", true);
        _db.AddOrder(waiting, null, sale, "ring the bell", true);
        _db.AddOrder(bought, apples, sale, "5", true);
        _db.AddOrder(bought, apples, sale, "5", false);

        var left = SaleSummaryViewModel.GetSumOfOrdersLeft(_db.Repo, sale);

        Assert.That(left.Single(x => x.ID == apples.ID).Value, Is.EqualTo("3"));
    }

    [Test]
    public void GetSumOfOrdersLeft_OtherSalesAreNotCounted()
    {
        var sale = _db.AddSale();
        var other = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient(), apples, other, "7", true);

        Assert.That(SaleSummaryViewModel.GetSumOfOrdersLeft(_db.Repo, sale).Single().Value, Is.EqualTo("0"));
    }

    [Test]
    public void SaleSummary_ZeroValueOrder_IsReportedAsErrorAndExcluded()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var client = _db.AddClient("Zero Guy", "555");
        _db.AddOrder(client, apples, sale, "0", true);
        _db.AddOrder(client, apples, sale, "4", true);

        var vm = new SaleSummaryViewModel(_db.Repo, sale);

        Assert.That(vm.IsError, Is.True);
        Assert.That(vm.Errors.Single(), Does.Contain("Zero Guy"));
        Assert.That(vm.SummaryItems.Single().AllOrders, Is.EqualTo("4"));
    }

    [Test]
    public void SaleSummary_ClientsLeft_CountsClientsWithOnlyReservedOrders()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient("A"), apples, sale, "1", true);
        _db.AddOrder(_db.AddClient("B"), apples, sale, "1", true);
        var done = _db.AddClient("C");
        _db.AddOrder(done, apples, sale, "1", true);
        _db.AddOrder(done, apples, sale, "1", false);
        _db.AddOrder(_db.AddClient("D"), null, sale, "just a comment", true);

        Assert.That(new SaleSummaryViewModel(_db.Repo, sale).ClientsLeft, Is.EqualTo(2));
    }

    [Test]
    public void SaleSummary_SearchFiltersByNameOrCode_AndSortsByCode()
    {
        var sale = _db.AddSale();
        _db.AddProduct("Zucchini", "02");
        _db.AddProduct("Apples", "01");
        _db.AddProduct("Beets", "00");

        var vm = new SaleSummaryViewModel(_db.Repo, sale);
        Assert.That(vm.FilteredSummaryItems.Select(x => x.Code), Is.EqualTo(new[] { "00", "01", "02" }));

        vm.SearchText = "app";
        Assert.That(vm.FilteredSummaryItems.Select(x => x.Name), Is.EqualTo(new[] { "Apples" }));

        vm.SearchText = "02";
        Assert.That(vm.FilteredSummaryItems.Select(x => x.Name), Is.EqualTo(new[] { "Zucchini" }));

        vm.SearchText = "";
        Assert.That(vm.FilteredSummaryItems.Count, Is.EqualTo(3));
    }

    // ---------- StatisticsViewModel ----------

    private static OrderAccuracyMetric Metric(StatisticsViewModel vm, string product) =>
        vm.AccuracyMetrics.Single(x => x.ProductName == product);

    [Test]
    public void Statistics_PercentageIsSoldWeightVersusOrdered()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00", price: 2);
        var c = _db.AddClient();
        _db.AddOrder(c, apples, sale, "10", true);
        // sold value is money: 30 / 2 per kg = 15 kg vs 10 kg ordered => +50%
        _db.AddOrder(c, apples, sale, "30", false);

        var vm = StatisticsHelper.Open(_db.Repo);

        Assert.That(Metric(vm, "Apples").PercentageDifference, Is.EqualTo(50).Within(0.001));
        Assert.That(Metric(vm, "Apples").PercentageString, Is.EqualTo("+50.0%"));
    }

    [Test]
    public void Statistics_SoldWithoutOrder_IsInfiniteAndShownAsNoData()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        _db.AddOrder(_db.AddClient(), apples, sale, "5", false);

        var vm = StatisticsHelper.Open(_db.Repo);

        Assert.That(double.IsPositiveInfinity(Metric(vm, "Apples").PercentageDifference), Is.True);
        Assert.That(Metric(vm, "Apples").PercentageString, Is.EqualTo("Brak danych"));
    }

    [Test]
    public void Statistics_UntouchedProductsAreSkipped_AndScopeSwitchRecalculates()
    {
        var sale1 = _db.AddSale("one");
        var sale2 = _db.AddSale("two");
        var apples = _db.AddProduct("Apples", "00");
        _db.AddProduct("Beets", "01");
        var c = _db.AddClient();
        _db.AddOrder(c, apples, sale1, "10", true);
        _db.AddOrder(c, apples, sale1, "5", false);
        _db.AddOrder(c, apples, sale2, "10", true);
        _db.AddOrder(c, apples, sale2, "20", false);

        var vm = StatisticsHelper.Open(_db.Repo);
        Assert.That(vm.SaleScopes.Count, Is.EqualTo(3));
        Assert.That(vm.AccuracyMetrics.Select(x => x.ProductName), Is.EqualTo(new[] { "Apples" }));
        Assert.That(Metric(vm, "Apples").PercentageDifference, Is.EqualTo(25).Within(0.001)); // (25-20)/20

        vm.SelectedSaleScope = vm.SaleScopes.Single(x => x.SaleID == sale1);
        Assert.That(Metric(vm, "Apples").PercentageDifference, Is.EqualTo(-50).Within(0.001));
    }

    // ---------- ClientSelectionViewModel ----------

    [Test]
    public void ClientSelection_ColoursAndOrdersClientsByState()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var empty = _db.AddClient("Empty", "1");
        var ordered = _db.AddClient("Ordered", "2");
        var bought = _db.AddClient("Bought", "3");
        _db.AddOrder(ordered, apples, sale, "1", true);
        _db.AddOrder(bought, apples, sale, "1", false);

        var vm = new ClientSelectionViewModel(sale, _db.Repo, null);

        Assert.That(vm.AllClients.Single(x => x.ID == empty.ID).Color, Is.EqualTo("White"));
        Assert.That(vm.AllClients.Single(x => x.ID == ordered.ID).Color, Is.EqualTo("Red"));
        Assert.That(vm.AllClients.Single(x => x.ID == bought.ID).Color, Is.EqualTo("Green"));
        // waiting (ordered only) first, then those who bought, then clients without orders
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Ordered", "Bought", "Empty" }));
    }

    [Test]
    public void ClientSelection_NameOrPhoneSearch()
    {
        var sale = _db.AddSale();
        _db.AddClient("Anna Nowak", "111222333");
        _db.AddClient("Jan Kowalski", "444555666");

        var vm = new ClientSelectionViewModel(sale, _db.Repo, null);

        vm.ClientName = "kowal";
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Jan Kowalski" }));
        vm.ClientName = "111222";
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Anna Nowak" }));
        vm.ClientName = "";
        Assert.That(vm.Clients.Count, Is.EqualTo(2));
    }

    [Test]
    public void ClientSelection_OrderAndSaleFilters()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var onlyOrder = _db.AddClient("OnlyOrder");
        var onlySale = _db.AddClient("OnlySale");
        var both = _db.AddClient("Both");
        _db.AddClient("Nothing");
        _db.AddOrder(onlyOrder, apples, sale, "1", true);
        _db.AddOrder(onlySale, apples, sale, "1", false);
        _db.AddOrder(both, apples, sale, "1", true);
        _db.AddOrder(both, apples, sale, "1", false);

        var vm = new ClientSelectionViewModel(sale, _db.Repo, null);
        vm.AreAllClientsShowing = false;

        vm.AreClientsWithOrderShowing = true; vm.AreClientsWithSaleShowing = false;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "OnlyOrder" }));

        vm.AreClientsWithOrderShowing = false; vm.AreClientsWithSaleShowing = true;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "OnlySale" }));

        vm.AreClientsWithOrderShowing = true; vm.AreClientsWithSaleShowing = true;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Both" }));

        vm.AreClientsWithOrderShowing = false; vm.AreClientsWithSaleShowing = false;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.Clients, Is.Empty);
    }

    [Test]
    public void ClientSelection_ProductFilterKeepsOnlyClientsWithThatProduct()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var beets = _db.AddProduct("Beets", "01");
        var a = _db.AddClient("WantsApples");
        var b = _db.AddClient("WantsBeets");
        _db.AddOrder(a, apples, sale, "1", true);
        _db.AddOrder(b, beets, sale, "1", true);

        var vm = new ClientSelectionViewModel(sale, _db.Repo, null);
        vm.Products.Single(x => x.Code == "00").IsReserved = true;
        vm.UpdateClientsCommand.Execute().Subscribe();

        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "WantsApples" }));
    }

    private (ClientSelectionViewModel vm, string sale, Product apples, Product beets) AdvancedFilterSetup()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00", price: 4);
        var beets = _db.AddProduct("Beets", "01", price: 10);
        var small = _db.AddClient("Small");
        var big = _db.AddClient("Big");
        var split = _db.AddClient("Split");
        var bought = _db.AddClient("Bought");
        _db.AddClient("Nothing");
        _db.AddOrder(small, apples, sale, "2", true);
        _db.AddOrder(big, apples, sale, "8,5", true);
        _db.AddOrder(big, beets, sale, "1", true);
        _db.AddOrder(split, apples, sale, "3 kg", true);
        _db.AddOrder(split, apples, sale, "3", true);
        _db.AddOrder(bought, apples, sale, "20", false);
        _db.AddOrder(bought, null, sale, "999", true); // comment line, must be ignored
        return (new ClientSelectionViewModel(sale, _db.Repo, null), sale, apples, beets);
    }

    private static string[] Names(ClientSelectionViewModel vm) => vm.Clients.Select(x => x.Name).OrderBy(x => x).ToArray();

    [Test]
    public void ClientSelection_AdvancedFilter_OrderedAmountAboveThreshold()
    {
        var (vm, _, _, _) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        var rule = vm.AdvancedFilters.Single();
        rule.ThresholdText = "5";

        // all products, ordered only, total amount > 5: Big 9.5, Split 6; Small 2 and Bought (no ordered lines) are out
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big", "Split" }));

        rule.Comparison = FilterComparison.Below;
        Assert.That(Names(vm), Is.EqualTo(new[] { "Small" }));
    }

    [Test]
    public void ClientSelection_AdvancedFilter_CostUsesPricePerKg()
    {
        var (vm, _, apples, _) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        var rule = vm.AdvancedFilters.Single();
        rule.Metric = FilterMetric.Cost;
        rule.ThresholdText = "40";

        // Big: 8.5*4 + 1*10 = 44; Split: 6*4 = 24; Small: 8
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big" }));

        rule.SelectedProduct = vm.FilterProducts.Single(x => x.ID == apples.ID);
        // apples only: Big 34
        Assert.That(Names(vm), Is.Empty);
    }

    [Test]
    public void ClientSelection_AdvancedFilter_PerProductAndSingleOrder()
    {
        var (vm, _, apples, beets) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        var rule = vm.AdvancedFilters.Single();
        rule.SelectedProduct = vm.FilterProducts.Single(x => x.ID == apples.ID);
        rule.ThresholdText = "5";

        Assert.That(Names(vm), Is.EqualTo(new[] { "Big", "Split" }));

        // Split ordered 3 + 3 in separate orders, so no single order is above 5
        rule.Aggregation = FilterAggregation.SingleOrder;
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big" }));

        rule.SelectedProduct = vm.FilterProducts.Single(x => x.ID == beets.ID);
        rule.ThresholdText = "0,5"; // comma decimals are accepted
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big" }));
    }

    [Test]
    public void ClientSelection_AdvancedFilter_StatusSelectsOrderedOrBoughtLines()
    {
        var (vm, _, _, _) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        var rule = vm.AdvancedFilters.Single();
        rule.ThresholdText = "10";

        rule.Status = FilterStatus.Bought;
        Assert.That(Names(vm), Is.EqualTo(new[] { "Bought" }));

        rule.Status = FilterStatus.Ordered;
        Assert.That(Names(vm), Is.Empty);

        // Any: Big 9.5 is still below 10, Bought has 20
        rule.Status = FilterStatus.Any;
        Assert.That(Names(vm), Is.EqualTo(new[] { "Bought" }));
    }

    [Test]
    public void ClientSelection_AdvancedFilter_RulesAreCombinedAndBlankThresholdIsIgnored()
    {
        var (vm, _, apples, beets) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        vm.AddFilterCommand.Execute().Subscribe();
        var first = vm.AdvancedFilters[0];
        var second = vm.AdvancedFilters[1];
        Assert.That(vm.Clients.Count, Is.EqualTo(5)); // empty thresholds filter nothing

        first.SelectedProduct = vm.FilterProducts.Single(x => x.ID == apples.ID);
        first.ThresholdText = "1";
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big", "Small", "Split" }));

        second.SelectedProduct = vm.FilterProducts.Single(x => x.ID == beets.ID);
        second.ThresholdText = "0";
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big" }));

        second.ThresholdText = "abc";
        Assert.That(Names(vm), Is.EqualTo(new[] { "Big", "Small", "Split" }));

        vm.RemoveFilterCommand.Execute(first).Subscribe();
        Assert.That(vm.AdvancedFilters.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClientSelection_AdvancedFilter_CombinesWithOtherFiltersAndSurvivesReopen()
    {
        var (vm, sale, apples, _) = AdvancedFilterSetup();
        vm.AddFilterCommand.Execute().Subscribe();
        var rule = vm.AdvancedFilters.Single();
        rule.SelectedProduct = vm.FilterProducts.Single(x => x.ID == apples.ID);
        rule.ThresholdText = "5";
        vm.ClientName = "sp";
        Assert.That(Names(vm), Is.EqualTo(new[] { "Split" }));

        var reopened = new ClientSelectionViewModel(sale, _db.Repo, null, vm.AdvancedFilters);

        var copy = reopened.AdvancedFilters.Single();
        Assert.That(copy, Is.Not.SameAs(rule));
        Assert.That(copy.SelectedProduct, Is.SameAs(reopened.FilterProducts.Single(x => x.ID == apples.ID)));
        Assert.That(Names(reopened), Is.EqualTo(new[] { "Big", "Split" }));
    }

    [Test]
    public void ClientSelection_ActiveFilterCount_CountsEverythingSetInTheFlyout()
    {
        var (vm, _, _, _) = AdvancedFilterSetup();
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(0));
        Assert.That(vm.HasActiveFilters, Is.False);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Products.First().IsReserved = true;
        vm.AreAllClientsShowing = false;
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(2));
        Assert.That(vm.HasActiveFilters, Is.True);
        Assert.That(raised, Does.Contain(nameof(ClientSelectionViewModel.HasActiveFilters)));

        vm.AddFilterCommand.Execute().Subscribe();
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(2)); // no threshold yet, rule is ignored
        vm.AdvancedFilters.Single().ThresholdText = "5";
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(3));

        vm.Products.First().IsReserved = false;
        vm.AreAllClientsShowing = true;
        vm.AdvancedFilters.Clear();
        vm.UpdateClientsCommand.Execute().Subscribe();
        Assert.That(vm.HasActiveFilters, Is.False);
    }

    [Test]
    public void ClientSelection_NewClientCommand_ForNonGuidCreatesBlankClient()
    {
        var vm = new ClientSelectionViewModel(_db.AddSale(), _db.Repo, null);
        Client? created = null;

        vm.OpenClientEditionCommand.Execute("").Subscribe(c => created = c);

        Assert.That(created, Is.Not.Null);
        Assert.That(Guid.TryParse(created!.ID, out _), Is.True);
        Assert.That(created.Name, Is.Empty);
    }

    // ---------- SalePlanningViewModel ----------

    private static void Run(SalePlanningViewModel vm) => vm.AutoScheduleCommand.Execute().Subscribe();

    private void AddPlan(string sale, DateTime start, DateTime end)
    {
        _db.Repo.Add(new SaleDayPlan { ID = Guid.NewGuid().ToString(), SaleID = sale, StartTime = start, EndTime = end });
        _db.Repo.Save();
    }

    [Test]
    public void Planning_AddAndRemoveDayPlan_Persists()
    {
        var sale = _db.AddSale();
        var vm = new SalePlanningViewModel(_db.Repo, sale);

        vm.AddDayPlanCommand.Execute().Subscribe();
        Assert.That(_db.Repo.GetAll<SaleDayPlan>().Count(x => x.SaleID == sale), Is.EqualTo(1));

        vm.RemoveDayPlanCommand.Execute(vm.DayPlans[0]).Subscribe();
        Assert.That(vm.DayPlans, Is.Empty);
        Assert.That(_db.Repo.GetAll<SaleDayPlan>().Any(), Is.False);
    }

    [Test]
    public void Planning_AutoSchedule_AssignsSequentialSlotsInFirstOrderOrder()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var day = new DateTime(2030, 5, 1);
        AddPlan(sale, day.AddHours(8), day.AddHours(16));

        var first = _db.AddClient("First");
        var second = _db.AddClient("Second");
        var bought = _db.AddClient("AlreadyBought");
        _db.AddOrder(first, apples, sale, "10", true);   // 10 kg at 5 kg/min => 2 minutes
        _db.AddOrder(second, apples, sale, "10", true);
        _db.AddOrder(bought, apples, sale, "10", false);
        _db.Repo.Add(new ClientSaleInfo { ID = "1", ClientID = first.ID, SaleID = sale, FirstOrderTime = day.AddHours(-3) });
        _db.Repo.Add(new ClientSaleInfo { ID = "2", ClientID = second.ID, SaleID = sale, FirstOrderTime = day.AddHours(-2) });
        _db.Repo.Save();

        Run(new SalePlanningViewModel(_db.Repo, sale));

        var infos = _db.Repo.GetAll<ClientSaleInfo>().ToList();
        var t1 = infos.Single(x => x.ClientID == first.ID).ExpectedArrivalTime;
        var t2 = infos.Single(x => x.ClientID == second.ID).ExpectedArrivalTime;
        Assert.That(t1, Is.EqualTo(day.AddHours(8)));
        Assert.That(t2, Is.EqualTo(day.AddHours(8).AddMinutes(5)), "2 minutes rounds up to the next 5-minute mark");
        Assert.That(infos.Any(x => x.ClientID == bought.ID && x.ExpectedArrivalTime != null), Is.False);
    }

    [Test]
    public void Planning_AutoSchedule_TooLittleTime_WarnsAndSkipsRemainingClients()
    {
        var sale = _db.AddSale();
        var apples = _db.AddProduct("Apples", "00");
        var day = new DateTime(2030, 5, 1);
        AddPlan(sale, day.AddHours(8), day.AddHours(8).AddMinutes(5)); // room for one client only
        var a = _db.AddClient("A");
        var b = _db.AddClient("B");
        _db.AddOrder(a, apples, sale, "10", true);
        _db.AddOrder(b, apples, sale, "10", true);

        var vm = new SalePlanningViewModel(_db.Repo, sale);
        Run(vm);

        Assert.That(vm.WarningMessage, Is.Not.Empty);
        Assert.That(_db.Repo.GetAll<ClientSaleInfo>().Count(x => x.ExpectedArrivalTime != null), Is.EqualTo(1));
    }

    [Test]
    public void Planning_AutoSchedule_WithoutDayPlansOrOrders_DoesNothing()
    {
        var sale = _db.AddSale();
        var vm = new SalePlanningViewModel(_db.Repo, sale);
        Run(vm);
        Assert.That(_db.Repo.GetAll<ClientSaleInfo>().Any(), Is.False);

        AddPlan(sale, DateTime.Today.AddHours(8), DateTime.Today.AddHours(16));
        Run(new SalePlanningViewModel(_db.Repo, sale));
        Assert.That(_db.Repo.GetAll<ClientSaleInfo>().Any(), Is.False);
    }
}
