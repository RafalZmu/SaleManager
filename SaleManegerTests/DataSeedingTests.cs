using Bogus;
using NUnit.Framework;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaleManegerTests
{
    internal class DataSeedingTests
    {
        private IProjectRepository _dataBase { get; set; }
        private Faker _faker { get; set; }

        [SetUp]
        public void Setup()
        {
            // Use the default SaleContext constructor so it maps to LocalAppData, like the actual application
            _dataBase = new ProjectRepository(new SaleContext());
            _faker = new Faker();
        }

        // We explicitly omit [TearDown] deleting the database here so the data will be preserved.

        [Test]
        public void SeedDatabaseWith10000ClientsAndOrders()
        {
            // 1. Create a new Sale
            string saleID = Guid.NewGuid().ToString();
            string saleName = "Test_10000_Clients_" + DateTime.Now.ToString("HHmmss");
            
            _dataBase.Add(new Sale()
            {
                SaleID = saleID,
                SaleName = saleName
            });
            _dataBase.Save();

            // 2. Fetch or create a couple of products so we have things to order
            var products = _dataBase.GetAll<Product>().ToList();
            if (!products.Any())
            {
                var prod1 = new Product() { ID = Guid.NewGuid().ToString(), Name = "Truskawki", Code = "T1", PricePerKg = 15 };
                var prod2 = new Product() { ID = Guid.NewGuid().ToString(), Name = "Maliny", Code = "M1", PricePerKg = 25 };
                
                products.Add(prod1);
                products.Add(prod2);
                
                _dataBase.Add(prod1);
                _dataBase.Add(prod2);
                _dataBase.Save();
            }

            // 3. Generate 10000 clients with random orders
            for (int i = 0; i < 10000; i++)
            {
                var client = new Client()
                {
                    ID = Guid.NewGuid().ToString(),
                    Name = _faker.Name.FullName(),
                    PhoneNumber = _faker.Phone.PhoneNumber(),
                    Products = new ObservableCollection<Product>(),
                };
                
                _dataBase.Add(client);

                // Assign 1 to 3 random orders for this client
                int numberOfOrders = _faker.Random.Int(1, 3);
                for(int j = 0; j < numberOfOrders; j++)
                {
                    var randomProduct = _faker.PickRandom(products);
                    
                    var order = new ClientOrder()
                    {
                        ClientOrderID = Guid.NewGuid().ToString(),
                        ClientID = client.ID,
                        ProductID = randomProduct.ID,
                        SaleID = saleID,
                        Date = DateTime.Now,
                        Value = _faker.Random.Int(1, 10).ToString(), // random amount from 1 to 10
                        IsReserved = _faker.Random.Bool()
                    };
                    
                    _dataBase.Add(order);
                }

                // Save in batches to avoid overwhelming the Entity Framework tracker
                if (i > 0 && i % 500 == 0)
                {
                    _dataBase.Save();
                    // We re-initialize the repository to clear EF Core tracking memory
                    _dataBase = new ProjectRepository(new SaleContext());
                }
            }

            // Final save for any remaining records
            _dataBase.Save();
        }
    }
}
