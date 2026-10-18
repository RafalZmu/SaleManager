using SaleManeger.Models;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Autosave and the close guard of the client screen, plus the list shortcuts that live in the view model.</summary>
internal class AutoSaveAndShortcutTests
{
    private TempDb _db = null!;
    private string _sale = null!;
    private Client _client = null!;

    [SetUp]
    public void Setup()
    {
        _db = new TempDb();
        _sale = _db.AddSale();
        _db.AddProduct("Apples", "00", 2);
        _client = _db.AddClient("Jan", "123456789");
    }

    [TearDown] public void TearDown() => _db.Dispose();

    private ClientEditionViewModel Open() => new(_db.Repo, _client, _sale);

    private int OrderCount() => _db.Repo.GetAll<ClientOrder>().Count(x => x.SaleID == _sale);

    [Test]
    public void FreshScreen_IsNotDirty_AndAutoSaveDoesNothing()
    {
        var vm = Open();

        Assert.That(vm.IsDirty, Is.False);
        Assert.That(vm.AutoSave(), Is.True);
        Assert.That(vm.AutoSaveStatus, Is.Null);
        Assert.That(OrderCount(), Is.EqualTo(0));
    }

    [Test]
    public void AnyEdit_MarksDirty_AndAutoSaveWritesItAndCleansUp()
    {
        var vm = Open();

        vm.Order = "Apples: 3";
        Assert.That(vm.IsDirty, Is.True);
        Assert.That(vm.AutoSave(), Is.True);

        Assert.That(vm.IsDirty, Is.False);
        Assert.That(vm.AutoSaveStatus, Does.StartWith("Zapisano automatycznie"));
        var order = _db.Repo.GetAll<ClientOrder>().Single();
        Assert.That(order.Value, Is.EqualTo("3"));
        Assert.That(order.IsReserved, Is.True);
    }

    [TestCase("name")]
    [TestCase("number")]
    [TestCase("sms")]
    [TestCase("sale")]
    [TestCase("notes")]
    public void EachField_MarksDirty(string field)
    {
        var vm = Open();
        switch (field)
        {
            case "name": vm.Name = "Janek"; break;
            case "number": vm.Number = "555"; break;
            case "sms": vm.AddToSmsGroup = true; break;
            case "sale": vm.Sale = "Apples: 5"; break;
            case "notes": vm.Notes = "x"; break;
        }
        Assert.That(vm.IsDirty, Is.True);
    }

    [Test]
    public void SettingTheSameValueAgain_DoesNotMarkDirty()
    {
        var vm = Open();
        vm.Name = "Jan";
        vm.Number = "123456789";
        Assert.That(vm.IsDirty, Is.False);
    }

    [Test]
    public void AutoSave_RepeatedEdits_KeepOneSetOfOrders()
    {
        var vm = Open();
        vm.Order = "Apples: 3";
        vm.AutoSave();
        vm.Order = "Apples: 4";
        vm.AutoSave();

        Assert.That(_db.Repo.GetAll<ClientOrder>().Select(x => x.Value).ToList(), Is.EqualTo(new[] { "4" }));
    }

    [Test]
    public void AutoSave_HalfTypedOrder_IsHeldBackWithoutShowingAWarning()
    {
        var vm = Open();
        vm.Order = "Apples:";

        Assert.That(vm.AutoSave(), Is.False);

        Assert.That(vm.IsDirty, Is.True);
        Assert.That(vm.WarningMessage, Is.Null.Or.Empty);
        Assert.That(vm.AutoSaveStatus, Is.EqualTo("Autozapis czeka na poprawne dane"));
        Assert.That(OrderCount(), Is.EqualTo(0));
    }

    [Test]
    public void AutoSave_UnknownProductName_DoesNotThrowOrWrite()
    {
        var vm = Open();
        vm.Order = "Bananas: 3";

        Assert.DoesNotThrow(() => Assert.That(vm.AutoSave(), Is.False));
        Assert.That(OrderCount(), Is.EqualTo(0));
    }

    [Test]
    public void SaveNow_SavesAndStays_OrShowsTheError()
    {
        var vm = Open();
        vm.Order = "Apples:";
        Assert.That(vm.SaveNow(), Is.False);
        Assert.That(vm.WarningMessage, Does.Contain("Apples"));

        vm.Order = "Apples: 2";
        Assert.That(vm.SaveNow(), Is.True);
        Assert.That(vm.WarningMessage, Is.Null.Or.Empty);
        Assert.That(vm.AutoSaveStatus, Does.StartWith("Zapisano "));
        Assert.That(OrderCount(), Is.EqualTo(1));
    }

    [Test]
    public void CloseGuard_CleanScreen_ClosesAtOnce()
    {
        Assert.That(Open().CanCloseWindow(), Is.True);
    }

    [Test]
    public void CloseGuard_ValidChanges_AreSavedAndWindowCloses()
    {
        var vm = Open();
        vm.Name = "Janusz";

        Assert.That(vm.CanCloseWindow(), Is.True);
        Assert.That(_db.Repo.GetAll<Client>().Single().Name, Is.EqualTo("Janusz"));
    }

    [Test]
    public void CloseGuard_UnsavableChanges_WarnOnceThenAllowTheSecondTry()
    {
        var vm = Open();
        vm.Order = "Apples:";

        Assert.That(vm.CanCloseWindow(), Is.False);
        Assert.That(vm.WarningMessage, Does.Contain("ponownie"));
        Assert.That(vm.CanCloseWindow(), Is.True);

        // typing something new asks again
        vm.Order = "Apples: ";
        Assert.That(vm.CanCloseWindow(), Is.False);
    }

    [Test]
    public void LeavingWithCofnij_StillSaves_AndLeavesTheScreenClean()
    {
        var vm = Open();
        vm.Order = "Apples: 3";

        string? result = null;
        vm.OpenClientSelectionCommand.Execute().Subscribe(x => result = x);

        Assert.That(result, Is.EqualTo(_sale));
        Assert.That(vm.IsDirty, Is.False);
        Assert.That(OrderCount(), Is.EqualTo(1));
    }

    // ---------- client list shortcuts ----------

    [Test]
    public void ClientList_OpenFirstClient_OpensTheTopOfTheFilteredList()
    {
        _db.AddClient("Zofia", "999000111");
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        vm.ClientName = "zof";
        Client? opened = null;
        vm.OpenClientEditionCommand.Subscribe(x => opened = x);

        vm.OpenFirstClient();

        Assert.That(opened?.Name, Is.EqualTo("Zofia"));
    }

    [Test]
    public void ClientList_OpenFirstClient_WithNoMatch_DoesNothing()
    {
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        vm.ClientName = "nobody";
        Client? opened = null;
        vm.OpenClientEditionCommand.Subscribe(x => opened = x);

        vm.OpenFirstClient();

        Assert.That(opened, Is.Null);
    }

    [Test]
    public void ClientList_AddNewClient_OpensAnEmptyClient()
    {
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        Client? opened = null;
        vm.OpenClientEditionCommand.Subscribe(x => opened = x);

        vm.AddNewClient();

        Assert.That(opened, Is.Not.Null);
        Assert.That(opened!.Name, Is.EqualTo(""));
        Assert.That(opened.ID, Is.Not.EqualTo(_client.ID));
    }

    [Test]
    public void ClientList_GoBack_RaisesTheBackCommand()
    {
        var vm = new ClientSelectionViewModel(_sale, _db.Repo, null);
        var raised = false;
        vm.OpenProjectSelectionCommand.Subscribe(_ => raised = true);

        vm.GoBack();

        Assert.That(raised, Is.True);
    }
}
