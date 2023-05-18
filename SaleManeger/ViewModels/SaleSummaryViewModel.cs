using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class SaleSummaryViewModel : ViewModelBase
    {
        #region Private Fields

        private List<Client> _clients;
        private List<Product> _products;

        #endregion Private Fields

        #region Public Constructors

        public SaleSummaryViewModel(IProjectRepository dataBase, string saleName)
        {
            _dataBase = dataBase;
            SaleName = saleName;

            // Get all clients and their orders.
            var ProductsList = _dataBase.GetAll<Product>().ToList();
            var clients = _dataBase.GetAll<Client>().ToList();
            var clientsOrders = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID == SaleName).ToList();
            var clientsOrdersList = clientsOrders.GroupBy(x => x.ClientID).Select(y => y.ToList()).ToList();
            foreach (var clientOrder in clientsOrdersList)
            {
                ObservableCollection<Product> orders = new();
                foreach (var order in clientOrder)
                {
                    if (order.ProductID == "Comment")
                        continue;
                    Product product = ProductsList.FirstOrDefault(x => order.ProductID == x.ID);

                    orders.Add(new Product()
                    {
                        Name = product.Name,
                        ID = product.ID,
                        IsReserved = order.IsReserved,
                        PricePerKg = product.PricePerKg,
                        Value = order.Value,
                        Code = product.Code,
                    });
                }
                clients.Where(x => x.ID == clientOrder[0].ClientID).FirstOrDefault().Products = orders;
            }
            clients.ForEach(x => x.Products ??= new ObservableCollection<Product>());
            _clients = clients;

            _products = _dataBase.GetAll<Product>().ToList();
            ClientsLeft = _clients.Count(x => (x.Products.Any(y => (y.IsReserved == true))) && (!x.Products.Any(y => y.IsReserved == false)));
            GetProducts();

            OpenClientSelectionCommand = ReactiveCommand.Create(() => { return SaleName; });
        }

        #endregion Public Constructors

        #region Public Properties

        public IProjectRepository _dataBase { get; set; }
        public string AllOrders { get; set; }
        public int ClientsLeft { get; set; }
        public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; set; }
        public string OrdersLeft { get; set; }
        public string SaleName { get; set; }
        public string SoldAll { get; set; }

        #endregion Public Properties

        #region Private Methods

        private void GetProducts()
        {
            // Get all orders, orders left and all sold products
            foreach (var product in _products)
            {
                AllOrders += $"{product.Name}: {_dataBase.GetAll<ClientOrder>()
                    .Where(x => x.ProductID == product.ID && x.IsReserved == true && x.SaleID == SaleName)
                    .ToList()
                    .Sum(x => double.Parse(x.Value.Split(' ')[0]))}{Environment.NewLine}";

                var sum = 0.0;
                foreach (var client in _clients)
                {
                    if (client.Products.Any(x => x.IsReserved == false))
                        continue;

                    sum += client.Products.Where(x => x.ID == product.ID).Sum(x => double.Parse(x.Value.Split(" ")[0]));
                }
                OrdersLeft += $"{product.Name}: {sum}{Environment.NewLine}";

                SoldAll += $"{product.Name}: {_dataBase.GetAll<ClientOrder>()
                    .Where(x => x.ProductID == product.ID && x.IsReserved == false && x.SaleID == SaleName)
                    .ToList()
                    .Sum(x => double.Parse(x.Value.Split(' ')[0]))}{Environment.NewLine}";
            }
        }

        #endregion Private Methods
    }
}