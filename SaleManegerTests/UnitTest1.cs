using Bogus;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SaleManeger.ViewModels;
using SaleManeger.Views;
using System.Collections.ObjectModel;

namespace SaleManegerTests;

internal class Tests
{
    #region Public Properties

    private IProjectRepository _dataBase { get; set; }
    private Faker _faker { get; set; }

    private TestHelpers _testHelpers { get; set; }

    #endregion Public Properties

    #region Public Methods

    [SetUp]
    public void Setup()
    {
        _dataBase = new ProjectRepository(new SaleContext(Environment.CurrentDirectory));
        _faker = new Faker();
        _testHelpers = new TestHelpers();
    }

    [Test]
    public void AddingNewClient()
    {
        //Arrange
        List<Client> createdClientsList = new List<Client>();
        foreach (var client in _dataBase.GetAll<Client>().ToList())
        {
            _dataBase.Delete(client);
        }

        for (int i = 0; i < 100; i++)
        {
            var client = new Client()
            {
                ID = Guid.NewGuid().ToString(),
                Name = _faker.Name.FullName(),
                PhoneNumber = _faker.Phone.PhoneNumber(),
                Products = new ObservableCollection<Product>(),
            };
            createdClientsList.Add(client);
            _dataBase.Add(client);
        }

        //Act
        _dataBase.Save();

        //Assert
        var result = _dataBase.GetAll<Client>().ToList();
        Assert.That(result.Count, Is.EqualTo(createdClientsList.Count));
        CollectionAssert.AreEquivalent(createdClientsList, result);
    }

    [Test]
    public void AddingNewProducts()
    {
        //Arrange
        foreach (var product in _dataBase.GetAll<Product>().ToList())
        {
            _dataBase.Delete(product);
        }
        _dataBase.Save();

        ObservableCollection<Product> products = new ObservableCollection<Product>
        {
            new Product()
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Product1",
                Code = "00",
                PricePerKg = -5,
            },
            new Product()
            {
                ID = Guid.NewGuid().ToString(),
                Name = "Product2",
                Code = "01",
                PricePerKg = 1,
            },
            new Product()
            {
                ID = Guid.NewGuid().ToString(),
                Name = "",
                Code = "",
                PricePerKg = 2,
            },
        };
        var productsEditionViewModel = new ProductEditionViewModel(_dataBase);

        //Act
        productsEditionViewModel.SaveToDataBase(_dataBase, products);

        //Assert
        var resultProducts = _dataBase.GetAll<Product>().OrderBy(x => x.Code).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(resultProducts, Has.Count.EqualTo(2));
            Assert.That(resultProducts[0].Name, Is.EqualTo("Product1"));
            Assert.That(resultProducts[0].Code, Is.EqualTo("00"));
            Assert.That(resultProducts[0].PricePerKg, Is.GreaterThan(0));
        });
    }

    [Test]
    public void ReadingProductsFromText()
    {
        //Arrange
        TestHelpers.DeleteAllSalesAndCreteNewOne(_dataBase);
        string clientOrderText = "Product1: 3\nProduct1: 2 inline comment\nComment in single line\nProduct1: 0.1\n Product1: b";
        var clientEditionViewModel = new ClientEditionViewModel(_dataBase, new Client()
        {
            ID = Guid.NewGuid().ToString(),
            Name = "Client1",
            PhoneNumber = "123456789",
            Products = new ObservableCollection<Product>(),
        }, "Sale1");

        //Act
        var productsFromText = ClientEditionViewModel.GetProductsFromText(_dataBase.GetAll<Product>().ToList(), clientOrderText, true);

        double sumOfProduct = 0;
        productsFromText.ForEach(x =>
        {
            double.TryParse(x.Value.Split(" ")[0], out double y);
            sumOfProduct += y;
        });

        //Assert
        Assert.That(productsFromText.Count, Is.EqualTo(4));
        Assert.That(productsFromText[1].Value, Is.EqualTo("2 inline comment"));
        Assert.That(productsFromText[2].Value, Is.EqualTo("Comment in single line"));
        Assert.That(sumOfProduct, Is.EqualTo(5.1));
    }

    [Test]
    public void AddingAndDeletingClientsOrders()
    {
        //Arrange
        foreach (var clientToDelete in _dataBase.GetAll<Client>().ToList())
        {
            _dataBase.Delete(clientToDelete);
        }
        var saleID = TestHelpers.DeleteAllSalesAndCreteNewOne(_dataBase);
        var clientID = Guid.NewGuid().ToString();
        var client = new Client()
        {
            ID = clientID,
            Name = "Client1",
            PhoneNumber = "123456789",
            Products = new ObservableCollection<Product>()
            {
                new Product() { ID = Guid.NewGuid().ToString(), Name = "Product1", Code = "00", PricePerKg = 1 , Value = "200", IsReserved = true} ,
                new Product() { ID = Guid.NewGuid().ToString(), Name = "Product2", Code = "01", PricePerKg = 2, Value = "30", IsReserved= true}
            },
        };
        _dataBase.Add(client);
        _dataBase.Save();

        //Act
        ClientEditionViewModel.SaveClientOrder(_dataBase, client, saleID);

        //Assert
        var orders = _dataBase.GetAll<ClientOrder>().Where(x => x.ClientID == client.ID);
        Assert.That(orders.Count, Is.EqualTo(2));

        //Act
        ClientDeletionConfirmationViewModel.DeleteClient(_dataBase, client.ID, saleID);

        //Assert
        Assert.That(_dataBase.GetAll<Client>().ToList(), Is.Empty);
    }

    #endregion Public Methods
}