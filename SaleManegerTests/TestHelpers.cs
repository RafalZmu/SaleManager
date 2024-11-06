using Bogus;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleManegerTests
{
    public class TestHelpers
    {
        public TestHelpers()
        {

        }
        public static string DeleteAllSalesAndCreteNewOne(IProjectRepository database)
        {
            foreach (var sale in database.GetAll<Sale>().ToList())
            {
                database.Delete<Sale>(sale);
            }
            database.Save();

            string saleID = Guid.NewGuid().ToString();
            database.Add(new Sale()
            {
                SaleID = saleID,
                SaleName = "Sale1"
            });
            database.Save();
            return saleID;
        }
        public static Client CreateClient(IProjectRepository database, Faker faker)
        {
            var client = new Client()
            {
                ID = Guid.NewGuid().ToString(),
                Name = faker.Name.FullName(),
                PhoneNumber = faker.Phone.PhoneNumber(),
                Products = new ObservableCollection<Product>()
            };
            database.Add<Client>(client);
            database.Save();

            return client;
        }
        public static List<Client> CreateClients(IProjectRepository database, Faker faker, int clientsNumber)
        {
            List<Client> clients = new ();
            for (int i = 0; i < clientsNumber; i++)
            {
                var client = new Client()
                {
                    ID = Guid.NewGuid().ToString(),
                    Name = faker.Name.FullName(),
                    PhoneNumber = faker.Phone.PhoneNumber(),
                    Products = new ObservableCollection<Product>()
                };
                clients.Add(client);
                database.Add<Client>(client);
            }
            database.Save();

            return clients;
        }

        public static void ClearWholeDatabase(IProjectRepository database)
        {
            var products = database.GetAll<Product>();
            var clients = database.GetAll<Client>();
            var clientsOrders = database.GetAll<ClientOrder>();
            var sales = database.GetAll<Sale>();

            foreach (var product in products)
            {
                database.Delete(product);
            }
            foreach (var client in clients)
            {
                database.Delete<Client>(client);
            }
            foreach(var clientOrder in clientsOrders)
            {
                database.Delete(clientOrder);
            }
            foreach(Sale sale in sales)
            {
                database.Delete<Sale>(sale);
            }

        }
    }
}
