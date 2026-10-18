using Microsoft.Data.Sqlite;
using SaleManeger.Models;
using SaleManeger.Repositories;

namespace SaleManegerTests;

/// <summary>
/// An isolated, throw-away SQLite database in its own temp folder (never touches %LocalAppData%\sale.db).
/// </summary>
internal sealed class TempDb : IDisposable
{
    private readonly string _dir;

    public IProjectRepository Repo { get; }

    public TempDb()
    {
        _dir = Path.Combine(Path.GetTempPath(), "salemanager-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
        Repo = new ProjectRepository(new SaleContext(_dir));
    }

    public string AddSale(string name = "Sale")
    {
        var id = Guid.NewGuid().ToString();
        Repo.Add(new Sale { SaleID = id, SaleName = name });
        Repo.Save();
        return id;
    }

    public Product AddProduct(string name, string code, double price = 1)
    {
        var p = new Product { ID = Guid.NewGuid().ToString(), Name = name, Code = code, PricePerKg = price };
        Repo.Add(p);
        Repo.Save();
        return p;
    }

    public Client AddClient(string name = "Client", string phone = "100200300")
    {
        var c = new Client { ID = Guid.NewGuid().ToString(), Name = name, PhoneNumber = phone, Products = new() };
        Repo.Add(c);
        Repo.Save();
        return c;
    }

    public ClientOrder AddOrder(Client client, Product? product, string saleId, string value, bool reserved)
    {
        var o = new ClientOrder
        {
            ClientOrderID = Guid.NewGuid().ToString(),
            ClientID = client.ID,
            ProductID = product?.ID ?? "Comment",
            SaleID = saleId,
            Value = value,
            IsReserved = reserved,
            Date = DateTime.Now
        };
        Repo.Add(o);
        Repo.Save();
        return o;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }
}
