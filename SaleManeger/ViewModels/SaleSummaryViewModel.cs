using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class SaleSummaryViewModel : ViewModelBase
    {
        public DataBase _dataBase { get; set; }
        public string SaleName { get; set; }
        public string OrdersLeft { get; set; }
        public string AllOrders { get; set; }
        public string SoldAll { get; set; }
        public int ClientsLeft { get; set; }
        private List<Client> _clients;
        private List<Product> _products;

        public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; set; }
        public SaleSummaryViewModel(DataBase dataBase, string saleName)
        {
            _dataBase = dataBase;
            SaleName = saleName;
            _clients = new List<Client>(_dataBase.GetClientsFromSale(SaleName));
            _products = _dataBase.GetProducts();
            ClientsLeft = _clients.Count(x => (x.Products.Any(y => (y.IsReserved == true))) && (!x.Products.Any(y => y.IsReserved == false)));
            GetProducts();

            OpenClientSelectionCommand = ReactiveCommand.Create(OpenClientSelection);
        }

        private void GetProducts()
        {
            foreach (var product in _products)
            {
                AllOrders += $"{product.Name}: {_dataBase.GetSumOfProduct(SaleName, product.Code, true)}{Environment.NewLine}";
                OrdersLeft += $"{product.Name}: {_dataBase.GetLeftProduct(SaleName, product.Code, true)}{Environment.NewLine}";
                SoldAll += $"{product.Name}: {_dataBase.GetSumOfProduct(SaleName, product.Code, false) / product.PricePerKg}{Environment.NewLine}";
            }
        }
        private string OpenClientSelection()
        {
            return SaleName;
        }

    }
}
