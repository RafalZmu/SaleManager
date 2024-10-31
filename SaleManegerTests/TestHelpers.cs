using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleManegerTests
{
    public class TestHelpers
    {
        private IProjectRepository _database;
        public TestHelpers()
        {
            var saleContext = new SaleContext(Environment.CurrentDirectory);
            _database = new ProjectRepository(saleContext);
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
    }
}
