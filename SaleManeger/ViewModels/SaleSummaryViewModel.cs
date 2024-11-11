using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
	public class SaleSummaryViewModel : ViewModelBase
	{
		private List<Product> _products;
		private List<Product> _productsLeftToSale;

		#region Public Constructors

		public SaleSummaryViewModel(IProjectRepository dataBase, string saleName)
		{
			_dataBase = dataBase;
			SaleName = saleName;
			Errors = new ObservableCollection<string>();

			// Get all clients and their orders.
			var ProductsList = _dataBase.GetAll<Product>().ToList();

			_products = _dataBase.GetAll<Product>().ToList();
			_products = _products.OrderBy(x => x.Code).ToList();
			_productsLeftToSale = GetSumOfOrdersLeft(_dataBase, SaleName);
			GetProducts();

            var startTime = DateTime.Now;

            var unreservedClients = new HashSet<string>();
            var allClientIDs = new HashSet<string>();

            foreach (var order in _dataBase.GetAll<ClientOrder>()
                                            .Where(x => x.SaleID == SaleName && x.ProductID != "Comment"))
            {
                allClientIDs.Add(order.ClientID);

                if (!order.IsReserved)
                {
                    unreservedClients.Add(order.ClientID);
                }
            }

            // Count clients who have only reserved orders by checking those not in `unreservedClients`
            ClientsLeft = allClientIDs.Count(clientId => !unreservedClients.Contains(clientId));

            var endTime = DateTime.Now;
            var time = endTime - startTime;
			OpenClientSelectionCommand = ReactiveCommand.Create(() => { return SaleName; });
			CloseErrorCommand = ReactiveCommand.Create(() => { IsError=false; });
		}

		#endregion Public Constructors

		public IProjectRepository _dataBase { get; set; }
		public string AllOrders { get; set; }
		public int ClientsLeft { get; set; }
		public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; set; }

		public ReactiveCommand<Unit, Unit> CloseErrorCommand { get; set; }
		public string OrdersLeft { get; set; }
		public string SaleName { get; set; }
		public string SoldAll { get; set; }
		public ObservableCollection<string> Errors { get; set; }
		private bool _IsError;
		public bool IsError
		{
			get => _IsError;
			set => this.RaiseAndSetIfChanged(ref _IsError, value);
		}

		#region Private Methods

		private void GetProducts()
		{
            CultureInfo culture = new CultureInfo("en-US");

            // Fetch all necessary data upfront to reduce database calls
            var allOrders = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID == SaleName).ToList();
            var clients = _dataBase.GetAll<Client>().ToDictionary(x => x.ID); // Use a dictionary for faster lookup

            foreach (var product in _products)
            {
                // Filter orders for the current product
                var productOrders = allOrders.Where(x => x.ProductID == product.ID).ToList();

                // Process All Orders
                var reservedOrders = productOrders.Where(x => x.IsReserved).ToList();
                var wrongOrders = reservedOrders.Where(x => x.Value == "0").ToList();

                // Log errors for wrong orders and remove them from reserved orders
                foreach (var order in wrongOrders)
                {
                    if (clients.TryGetValue(order.ClientID, out var wrongClient))
                    {
                        Errors.Add($"Problem w zamówieniu: {wrongClient.Name} {wrongClient.PhoneNumber}");
                    }
                    IsError = true;
                    reservedOrders.Remove(order);
                }

                AllOrders += $"{product.Name}: {reservedOrders.Sum(x => double.Parse(x.Value.Split(' ')[0], culture))}{Environment.NewLine}";

                // Orders Left
                var ordersLeft = _productsLeftToSale.FirstOrDefault(x => x.ID == product.ID)?.Value ?? "0";
                OrdersLeft += $"{product.Name}: {ordersLeft}{Environment.NewLine}";

                // Process Sold Products
                var soldOrders = productOrders.Where(x => !x.IsReserved).ToList();
                var wrongSoldOrders = soldOrders.Where(x => x.Value == "0").ToList();

                foreach (var order in wrongSoldOrders)
                {
                    if (clients.TryGetValue(order.ClientID, out var wrongClient))
                    {
                        Errors.Add($"Problem w zamówieniu: {wrongClient.Name} {wrongClient.PhoneNumber}");
                    }
                    IsError = true;
                    soldOrders.Remove(order);
                }

                SoldAll += $"{product.Name}: {soldOrders.Sum(x => double.Parse(x.Value.Split(' ')[0], culture))}{Environment.NewLine}";
            }
        }
        public static List<Product> GetSumOfOrdersLeft(IProjectRepository database, string saleID)
		{
            var culture = new CultureInfo("en-US");

            // Step 1: Filter and group by ClientID
            var ordersByClient = database.GetAll<ClientOrder>()
                                         .Where(x => x.SaleID == saleID && x.ProductID != "Comment")
                                         .GroupBy(x => x.ClientID)
                                         .ToList();

            // Step 2: Filter out clients with unreserved items
            var ordersLeftList = ordersByClient
                                    .Where(group => group.All(order => order.IsReserved))
                                    .SelectMany(group => group)
                                    .ToList();

            // Step 3: Group by ProductID to get sums
            var productSums = ordersLeftList.GroupBy(x => x.ProductID)
                                            .ToDictionary(g => g.Key, g => g.Sum(x => double.Parse(x.Value.Split(" ")[0], culture)));

            // Step 4: Initialize Products and set values based on calculated sums
            var products = database.GetAll<Product>().ToList();
            products.ForEach(product =>
            {
                product.Value = productSums.TryGetValue(product.ID, out var sum) ? sum.ToString(culture) : "0";
            });

            return products;
        }

        #endregion Private Methods
    }
}