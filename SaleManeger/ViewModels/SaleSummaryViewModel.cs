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
		private List<Client> _clients;
		private List<Product> _products;

		#region Public Constructors

		public SaleSummaryViewModel(IProjectRepository dataBase, string saleName)
		{
			_dataBase = dataBase;
			SaleName = saleName;
			Errors = new ObservableCollection<string>();

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
			_products = _products.OrderBy(x => x.Code).ToList();
			ClientsLeft = _clients.Count(x => (x.Products.Any(y => (y.IsReserved == true))) && (!x.Products.Any(y => y.IsReserved == false)));
			GetProducts();

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
			// Get all orders, orders left and all sold products
			foreach (var product in _products)
			{
				//All orders
				// 0 means that the order is wrong
				var AllOrdersList = _dataBase.GetAll<ClientOrder>().Where(x => x.ProductID == product.ID && x.IsReserved == true && x.SaleID == SaleName).ToList();
				var wrongOrders = AllOrdersList.Where(x => x.Value == "0").ToList();
				foreach(var order in wrongOrders)
                {
                    var wrongClient = _dataBase.GetAll<Client>().FirstOrDefault(x => x.ID == order.ClientID);
                    Errors.Add($"Problem w zamówieniu: {wrongClient.Name} {wrongClient.PhoneNumber}");
					IsError = true;
                    AllOrdersList.Remove(order);
                }
                AllOrders += $"{product.Name}: {AllOrdersList.Sum(x => double.Parse(x.Value.Split(' ')[0], culture))}{Environment.NewLine}";

				// Orders left
				var sum = 0.0;
				foreach (var client in _clients)
				{
					if (client.Products.Any(x => x.IsReserved == false))
						continue;

					sum += client.Products.Where(x => x.ID == product.ID).Sum(x => double.Parse(x.Value.Split(" ")[0], culture));
				}
				OrdersLeft += $"{product.Name}: {sum}{Environment.NewLine}";

				// All sold products
				var SoldAllList = _dataBase.GetAll<ClientOrder>().Where(x => x.ProductID == product.ID && x.IsReserved == false && x.SaleID == SaleName).ToList();
				wrongOrders = SoldAllList.Where(x => x.Value == "-1").ToList();

				foreach(var order in wrongOrders)
				{
                    var wrongClient = _dataBase.GetAll<Client>().FirstOrDefault(x => x.ID == order.ClientID);
                    Errors.Add($"Problem w zamówieniu: {wrongClient.Name} {wrongClient.PhoneNumber}");
					IsError = true;
					SoldAllList.Remove(order);
                }

                SoldAll += $"{product.Name}: {SoldAllList.Sum(x => double.Parse(x.Value.Split(' ')[0], culture))}{Environment.NewLine}";
			}
		}

		#endregion Private Methods
	}
}