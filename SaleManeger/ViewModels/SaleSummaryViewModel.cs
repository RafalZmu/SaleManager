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
			List<Product> productsLeftToSale = GetSumOfOrdersLeft(_dataBase, SaleName);
			GetProducts();

			var ordersByClient = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID ==SaleName).GroupBy(x => x.ClientID).Select(y => y.ToList()).ToList();
			ordersByClient.ForEach(x => x.RemoveAll(y => y.ProductID == "Comment"));
			var ordersLeft = ordersByClient.Where(x => x.Any(y => y.IsReserved == false) == false).ToList();
			ClientsLeft = ordersLeft.Count;

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
			var ProductsLeft = GetSumOfOrdersLeft(_dataBase, SaleName);
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
				OrdersLeft += $"{product.Name}: {ProductsLeft.First(x => x.ID == product.ID).Value}{Environment.NewLine}";

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
		public static List<Product> GetSumOfOrdersLeft(IProjectRepository database, string saleID)
		{
			var culture = new CultureInfo("en-US");
			var ordersByClient = database.GetAll<ClientOrder>().Where(x => x.SaleID ==saleID).GroupBy(x => x.ClientID).Select(y => y.ToList()).ToList();
			ordersByClient.ForEach(x => x.RemoveAll(y => y.ProductID == "Comment"));
			var ordersLeft = ordersByClient.Where(x => x.Any(y => y.IsReserved == false) == false).ToList();
			List<ClientOrder> ordersLeftList = ordersLeft.SelectMany(x => x).ToList();

			var products = database.GetAll<Product>().ToList();
			products.ForEach(products => products.Value = "0");
            foreach (var product in products)
            {
				product.Value = ordersLeftList.Where(x => x.ProductID == product.ID).Sum(x => double.Parse(x.Value.Split(" ")[0], culture)).ToString(); 
            }

            return products;
		}

		#endregion Private Methods
	}
}