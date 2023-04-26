using Bogus;
using SaleManeger.Models;
using System.Collections.ObjectModel;

namespace SaleManegerTests
{
    [TestFixture]
    public class Tests
    {
        public Faker faker = new();
        public DataBase dataBase;
        [SetUp]
        public void SetUp()
        {
            dataBase = new DataBase();
        }
        [Test]
        public void AddingAndReadingSalesToDB()
        {
            //Arrange
            ObservableCollection<Sale> salesAdded = new();
            dataBase.DeleteAllFromTable("Sales");
            for (int i = 0; i < 200; i++)
            {
                var saleName = faker.Random.Words(4);
                salesAdded.Add(new Sale(saleName));
                dataBase.AddToTable("Sales", ("saleName", saleName));
            }
            ObservableCollection<Sale> salesRead = dataBase.GetSalesList();

            //Act
            var matchingNames = salesAdded.Zip(salesRead, (s1, s2) => s1.SaleDate == s2.SaleDate);

            //Assert
            Assert.That(salesAdded.Count, Is.EqualTo(200));
            Assert.That(salesRead.Count, Is.EqualTo(200));
            Assert.IsTrue(matchingNames.All(x => x));
        }
        [Test]
        public void AddingAndReadingProductsToDB()
        {
            //Arrange
            dataBase.DeleteAllFromTable("Products");
            ObservableCollection<Product> products = new();
            List<string> codes = new();
            // Generate cods
            for (int i = 0; i < 200; i++)
            {
                string uniqueString;
                do
                {
                    uniqueString = faker.Random.AlphaNumeric(2);
                } while (codes.Contains(uniqueString));
                codes.Add(uniqueString);
            }

            for (int i = 0; i < 200; i++)
            {
                products.Add(new Product()
                {
                    Code = codes[i],
                    Name = faker.Random.Word(),
                    PricePerKg = faker.Random.Double(0, 2000),
                });
            }

            //Act
            dataBase.AddProductsToProductsTable(products);
            var readProducts = dataBase.GetProducts();

            //Assert
            Assert.That(products.Count == readProducts.Count);
        }

        [Test]
        public void DeletingClientFromDB()
        {
            //Arrange
            // Add sale
            dataBase.DeleteAllFromTable("Sales");
            dataBase.AddToTable("Sales", ("saleName", "sale1"));
            dataBase.DeleteAllFromTable("Clients");
            ObservableCollection<Client> clients = new();
            for (int i = 0; i < 200; i++)
            {
                clients.Add(new Client()
                {
                    ID = faker.Random.Guid().ToString(),
                    Name = faker.Random.Word(),
                    PhoneNumber = faker.Phone.PhoneNumber().ToString(),
                });
            }

            //Act
            foreach (var client in clients)
            {
                dataBase.UpdateOrCreateClient(client, "sale1");
            }
            var clientIDToDelete = clients[0].ID;
            dataBase.DeleteClient(clientIDToDelete, "sale1");
            var readClients = dataBase.GetClientsFromSale("sale1");

            //Assert
            Assert.That(readClients.Any(x => x.ID == clientIDToDelete) == false);
            Assert.That(clients.Count != readClients.Count);
            Assert.That(readClients.Count == 199);

        }
    }
}