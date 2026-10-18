using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.ViewModels;

namespace SaleManegerTests;

/// <summary>Client notes and tags ("po terminie"): text helpers, database upgrade, edit screen and list filter.</summary>
internal class ClientNotesAndTagsTests
{
    // ---------- ClientTags ----------

    [Test]
    public void Parse_SplitsTrimsAndRemovesDuplicatesIgnoringCase()
    {
        Assert.That(ClientTags.Parse(" po terminie ,stały; PO TERMINIE\n,"), Is.EqualTo(new[] { "po terminie", "stały" }));
        Assert.That(ClientTags.Parse(null), Is.Empty);
        Assert.That(ClientTags.Parse("  "), Is.Empty);
    }

    [Test]
    public void Join_WritesTheCanonicalText()
    {
        Assert.That(ClientTags.Join(new[] { "b", " a ", "B" }), Is.EqualTo("b, a"));
        Assert.That(ClientTags.Join(Array.Empty<string>()), Is.EqualTo(""));
    }

    [Test]
    public void AllKnown_AlwaysOffersPoTerminie_AndMergesUsedTags()
    {
        var clients = new[]
        {
            new Client { Tags = "stały" },
            new Client { Tags = "Stały, vip" },
            new Client { Tags = null },
        };

        Assert.That(ClientTags.AllKnown(clients), Is.EqualTo(new[] { "po terminie", "stały", "vip" }));
        Assert.That(ClientTags.AllKnown(Array.Empty<Client>()), Is.EqualTo(new[] { "po terminie" }));
    }

    [Test]
    public void Has_IgnoresCase()
    {
        Assert.That(ClientTags.Has(new Client { Tags = "Po Terminie" }, "po terminie"), Is.True);
        Assert.That(ClientTags.Has(new Client { Tags = "vip" }, "po terminie"), Is.False);
        Assert.That(ClientTags.Has(new Client(), "po terminie"), Is.False);
    }

    // ---------- database upgrade ----------

    [Test]
    public void OldDatabase_GetsNotesAndTagsColumns_AndKeepsClients()
    {
        var dir = Path.Combine(Path.GetTempPath(), "salemanager-tags-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            using (var connection = new SqliteConnection($"Data Source={Path.Combine(dir, "sale.db")};Pooling=false"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE ""Clients"" (""ID"" TEXT NOT NULL CONSTRAINT ""PK_Clients"" PRIMARY KEY, ""Color"" TEXT NULL, ""Name"" TEXT NULL, ""PhoneNumber"" TEXT NULL);
                    INSERT INTO ""Clients"" VALUES ('c1', 'Red', 'Jan Kowalski', '123456789');";
                cmd.ExecuteNonQuery();
            }

            var repo = new ProjectRepository(new SaleContext(dir));
            var client = repo.GetAll<Client>().Single();
            Assert.That(client.Name, Is.EqualTo("Jan Kowalski"));
            Assert.That(client.Notes, Is.Null);
            Assert.That(client.Tags, Is.Null);

            client.Notes = "odbiera w piątki";
            client.Tags = "po terminie";
            repo.Update(client);
            repo.Save();

            var reopened = new ProjectRepository(new SaleContext(dir));
            var again = reopened.GetAll<Client>().Single();
            Assert.That(again.Notes, Is.EqualTo("odbiera w piątki"));
            Assert.That(again.Tags, Is.EqualTo("po terminie"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
    }

    // ---------- edit screen ----------

    [Test]
    public void EditScreen_SavesNotesAndTags_AndHeaderSummarisesThem()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        db.AddProduct("Apples", "00");
        var client = db.AddClient("Jan", "123456789");

        var vm = new ClientEditionViewModel(db.Repo, client, sale);
        Assert.That(vm.NotesHeader, Is.EqualTo("Notatki i tagi"));
        Assert.That(vm.TagOptions.Select(x => x.Name), Is.EqualTo(new[] { "po terminie" }));

        vm.Notes = "odbiera w piątki";
        vm.TagOptions.Single().IsSelected = true;
        vm.NewTagText = "stały";
        vm.AddTagCommand.Execute().Subscribe();
        Assert.That(vm.NewTagText, Is.EqualTo(""));
        Assert.That(vm.NotesHeader, Is.EqualTo("Notatki i tagi (po terminie, stały, notatka)"));
        vm.OpenClientSelectionCommand.Execute().Subscribe();

        var saved = db.Repo.GetAll<Client>().Single();
        Assert.That(saved.Notes, Is.EqualTo("odbiera w piątki"));
        Assert.That(saved.Tags, Is.EqualTo("po terminie, stały"));

        // next client sees both tags as options, and untoggling removes the tag again
        var again = new ClientEditionViewModel(db.Repo, saved, sale);
        Assert.That(again.TagOptions.Select(x => x.Name), Is.EqualTo(new[] { "po terminie", "stały" }));
        Assert.That(again.TagOptions.All(x => x.IsSelected), Is.True);
        again.TagOptions.First(x => x.Name == "po terminie").IsSelected = false;
        again.Notes = "  ";
        again.OpenClientSelectionCommand.Execute().Subscribe();

        var after = db.Repo.GetAll<Client>().Single();
        Assert.That(after.Tags, Is.EqualTo("stały"));
        Assert.That(after.Notes, Is.Null);
    }

    [Test]
    public void EditScreen_AddingAnExistingTagTypedInOtherCase_SelectsItInsteadOfDuplicating()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        db.AddProduct("Apples", "00");
        var client = db.AddClient("Jan");
        var vm = new ClientEditionViewModel(db.Repo, client, sale);

        vm.NewTagText = "PO TERMINIE";
        vm.AddTagCommand.Execute().Subscribe();

        Assert.That(vm.TagOptions.Count, Is.EqualTo(1));
        Assert.That(vm.TagOptions.Single().IsSelected, Is.True);
    }

    // ---------- client list ----------

    private static void SetTags(TempDb db, Client client, string tags)
    {
        var tracked = db.Repo.GetAll<Client>().Single(x => x.ID == client.ID);
        tracked.Tags = tags;
        db.Repo.Update(tracked);
        db.Repo.Save();
    }

    [Test]
    public void ClientList_TagFilter_ShowsClientsWithAnySelectedTag_AndCountsAsActiveFilter()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        SetTags(db, db.AddClient("Late"), "po terminie");
        SetTags(db, db.AddClient("Regular"), "stały");
        db.AddClient("Plain");

        var vm = new ClientSelectionViewModel(sale, db.Repo, null);
        Assert.That(vm.TagFilterOptions.Select(x => x.Name), Is.EqualTo(new[] { "po terminie", "stały" }));
        Assert.That(vm.Clients.Count, Is.EqualTo(3));

        vm.TagFilterOptions.First(x => x.Name == "po terminie").IsSelected = true;
        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Late" }));
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(1));

        vm.TagFilterOptions.First(x => x.Name == "stały").IsSelected = true;
        Assert.That(vm.Clients.Select(x => x.Name).OrderBy(x => x), Is.EqualTo(new[] { "Late", "Regular" }));
        Assert.That(vm.ActiveFilterCount, Is.EqualTo(2));

        vm.TagFilterOptions.ToList().ForEach(x => x.IsSelected = false);
        Assert.That(vm.Clients.Count, Is.EqualTo(3));
        Assert.That(vm.HasActiveFilters, Is.False);
    }

    [Test]
    public void ClientList_TagFilter_IsCarriedToTheNextVisit_AndForgottenTagsAreDropped()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        var late = db.AddClient("Late");
        SetTags(db, late, "po terminie, vip");
        var first = new ClientSelectionViewModel(sale, db.Repo, null);
        first.TagFilterOptions.First(x => x.Name == "vip").IsSelected = true;

        var second = new ClientSelectionViewModel(sale, db.Repo, null, null, first.SelectedTags);
        Assert.That(second.SelectedTags, Is.EqualTo(new[] { "vip" }));
        Assert.That(second.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Late" }));

        SetTags(db, late, null!);
        var third = new ClientSelectionViewModel(sale, db.Repo, null, null, second.SelectedTags);
        Assert.That(third.SelectedTags, Is.Empty);
        Assert.That(third.Clients.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClientList_SearchBox_AlsoFindsTags()
    {
        using var db = new TempDb();
        var sale = db.AddSale();
        SetTags(db, db.AddClient("Late"), "po terminie");
        db.AddClient("Plain");

        var vm = new ClientSelectionViewModel(sale, db.Repo, null);
        vm.ClientName = "PO TERM";

        Assert.That(vm.Clients.Select(x => x.Name), Is.EqualTo(new[] { "Late" }));
    }
}
