using AvaloniaEdit.Utils;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace SaleManeger.ViewModels
{
	public class ClientEditionViewModel : ViewModelBase, INotifyPropertyChanged
	{
		#region Fields

		private string _order;
		private List<Product> _products;
        public List<Product> ProductsList => _products;
		private string _sale;
		private string _saleID;
		private string _saleSum;
		private string _warningMessage;
        private DateTimeOffset? _expectedArrivalDate;
        private TimeSpan? _expectedArrivalTime;
        private bool _hasIgnoredTimeWarning = false;

		#endregion Fields

		#region Properties

		public Client Client { get; set; }
		public string ClientID { get; set; }
		public string Codes { get; set; }
		public string Name { get; set; }
		public string Number { get; set; }
		private List<SaleProduct> _curruntlyAvailableProducts; 
		public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleDisplayModeCommand { get; }
        
        private bool _isShowingWolne = true;
        public string DisplayModeText => _isShowingWolne ? "Pokaż: Zamówione" : "Pokaż: Wolne";
        
        private List<Product> _requiredProducts;

		public string Order
		{
			get => _order;
			set
			{
				if (_order != value)
				{
					this.RaiseAndSetIfChanged(ref _order, value, nameof(Order));
				}
			}
		}

		public string Sale
		{
			get => _sale;
			set
			{
				_sale = value;
				UpdateSaleSum();
			}
		}
        
        public DateTimeOffset? ExpectedArrivalDate
        {
            get => _expectedArrivalDate;
            set 
            {
                this.RaiseAndSetIfChanged(ref _expectedArrivalDate, value);
                _hasIgnoredTimeWarning = false; 
            }
        }

        public TimeSpan? ExpectedArrivalTime
        {
            get => _expectedArrivalTime;
            set 
            {
                this.RaiseAndSetIfChanged(ref _expectedArrivalTime, value);
                _hasIgnoredTimeWarning = false; 
            }
        }

        public HashSet<string> LowStockProductIDs { get; set; } = new HashSet<string>();

		public string SaleSum
		{
			get => _saleSum;
			set
			{
				this.RaiseAndSetIfChanged(ref _saleSum, value);
			}
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set
			{
				this.RaiseAndSetIfChanged(ref _warningMessage, value);
			}
		}

		private IProjectRepository _dataBase { get; set; }

		#endregion Properties

		#region Public Constructors

		public ClientEditionViewModel(IProjectRepository db, Client client, string saleID)
		{
			_dataBase = db;
			_saleID = saleID;
			Name = client.Name;
			Number = client.PhoneNumber;
			ClientID = client.ID;
			Client = client;

			OpenClientSelectionCommand = ReactiveCommand.Create(OpenClientSelection);

			_curruntlyAvailableProducts = new List<SaleProduct>(_dataBase.GetAll<SaleProduct>().Where(x => x.SaleID == _saleID));

			_products = _dataBase.GetAll<Product>().AsNoTracking().ToList();

			if (_curruntlyAvailableProducts.Count == 0)
			{
				var saleProducts = new ObservableCollection<SaleProduct>();
				CurrentProductStateViewModel.CreateSaleItems(_products, _saleID, saleProducts, _dataBase);
                _curruntlyAvailableProducts = new List<SaleProduct>(_dataBase.GetAll<SaleProduct>().Where(x => x.SaleID == _saleID));
			}

			_requiredProducts = SaleSummaryViewModel.GetSumOfOrdersLeft(_dataBase, _saleID);

			_products = _products.OrderBy(x => x.Code).ToList();
            
            ToggleDisplayModeCommand = ReactiveCommand.Create(() => 
            {
                _isShowingWolne = !_isShowingWolne;
                RefreshCodesText();
            });

            RefreshCodesText();

			foreach (var item in client.Products)
			{
				if (item.IsReserved)
				{
					Order += $"{item.Name}{(string.IsNullOrEmpty(item.Name) ? "" : ": ")}{item.Value}{Environment.NewLine}";
				}
				else
				{
					Sale += $"{item.Name}{(string.IsNullOrEmpty(item.Name) ? "" : ": ")}{item.Value}{Environment.NewLine}";
				}
			}

            var clientSaleInfo = _dataBase.GetAll<ClientSaleInfo>().FirstOrDefault(x => x.ClientID == client.ID && x.SaleID == _saleID);
            if (clientSaleInfo != null && clientSaleInfo.ExpectedArrivalTime.HasValue)
            {
                _expectedArrivalDate = clientSaleInfo.ExpectedArrivalTime.Value.Date;
                _expectedArrivalTime = clientSaleInfo.ExpectedArrivalTime.Value.TimeOfDay;
                this.RaisePropertyChanged(nameof(ExpectedArrivalDate));
                this.RaisePropertyChanged(nameof(ExpectedArrivalTime));
            }
		}

        #endregion Public Constructors

        #region Public Methods

        public static void SaveClientOrder(IProjectRepository dataBase, Client client, string saleID)
		{
			//Delete old client orders
			foreach (var item in dataBase.GetAll<ClientOrder>().Where(x => x.ClientID == client.ID && x.SaleID == saleID))
			{
				dataBase.Delete(item);
			}
			dataBase.Save();

			//Add new client orders
			foreach (var order in client.Products)
			{
				if (order.ID == "")
				{
					order.ID = "Comment";
				}
				order.Value = order.Value.TrimStart();
				order.Value = Regex.Replace(order.Value, @"(\d)([a-zA-Z])", "$1 $2");

				dataBase.Add(new ClientOrder()
				{
					ClientID = client.ID,
					ClientOrderID = Guid.NewGuid().ToString(),
					ProductID = order.ID,
					SaleID = saleID,
					Date = DateTime.Now,
					Value = order.Value,
					IsReserved = order.IsReserved
				});
				dataBase.Save();
			}
		}

		public static List<Product> GetProductsFromText(List<Product> products, string text, bool IsReserved)
		{
			var productsList = new List<Product>();
			if (string.IsNullOrWhiteSpace(text))
				return productsList;

			foreach (var item in text.Trim().Split("\n"))
			{
				if (!item.Contains(':'))
				{
					Product comment = new()
					{
						ID = "",
						Name = "",
						Code = "",
						Value = item.Trim(),
						IsReserved = IsReserved
					};
					productsList.Add(comment);
					continue;
				}
				var name = item.Split(":")[0].Trim();
				var value = item.Split(':')[1].Trim();
				var parsedValue = value.Split(" ")[0];

                if (double.TryParse(value.Split(" ")[0],CultureInfo.InvariantCulture, out _) == false)
                {
                    value = "0";
                }

                var code = products.First(x => x.Name == name).Code;
				var ID = products.First(x => x.Code == code).ID;
				Product product = new()
				{
					ID = ID,
					Name = name,
					Code = code,
					Value = value,
					IsReserved = IsReserved
				};
				productsList.Add(product);
			}
			return productsList;
		}

		#endregion Public Methods

		#region Private Methods

        private void RefreshCodesText()
        {
            Codes = "";
            LowStockProductIDs.Clear();

            _products.ForEach(p =>
            {
                var required = _requiredProducts.First(x => x.Code == p.Code);
                var available = _curruntlyAvailableProducts.FirstOrDefault(x => x.ProductID == p.ID);

                // Always calculate the buffer mathematics to safely trigger Orange Highlighting natively without relying on the UI state!
                double productsToSale = 0;
                if (available != null)
                {
                    productsToSale = available.Amount - double.Parse(required.Value, CultureInfo.InvariantCulture);
                    if (productsToSale < 0 && available.Amount > 0)
                    {
                        LowStockProductIDs.Add(p.ID);
                    }
                }

                if (_isShowingWolne)
                {
                    Codes += $"{p.Code}-{p.Name}: Wolne = [{productsToSale}]{Environment.NewLine}";
                }
                else
                {
                    Codes += $"{p.Code}-{p.Name}: Zamówione = [{required.Value}]{Environment.NewLine}";
                }
            });

            this.RaisePropertyChanged(nameof(Codes));
            this.RaisePropertyChanged(nameof(DisplayModeText));
        }

		private string OpenClientSelection()
		{
			if (!ValidateText(Order) || !ValidateText(Sale))
			{
				return string.Empty;
			}
            
            if (ExpectedArrivalTime.HasValue && ExpectedArrivalDate.HasValue && !_hasIgnoredTimeWarning)
            {
                var targetTime = ExpectedArrivalDate.Value.Date.Add(ExpectedArrivalTime.Value);
                var existingInfo = _dataBase.GetAll<ClientSaleInfo>().FirstOrDefault(x => x.SaleID == _saleID && x.ClientID != ClientID && x.ExpectedArrivalTime == targetTime);
                
                if (existingInfo != null)
                {
                    WarningMessage = $"Uwaga: Inny klient ma przypisaną tę samą godzinę odbioru! Kliknij Cofnij ponownie, aby wymusić zapis.";
                    _hasIgnoredTimeWarning = true;
                    return string.Empty;
                }
            }

			WarningMessage = string.Empty;
			SaveClient();
			return _saleID;
		}

		private bool ValidateText(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return true;

			foreach (var item in text.Trim().Split('\n'))
			{
				if (!item.Contains(':')) continue;
				
				var parts = item.Split(':');
				var name = parts[0].Trim();
				if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
				{
					WarningMessage = $"Błąd: Brak wagi dla produktu '{name}'!";
					return false;
				}
				
				var valueStr = parts[1].Trim().Split(' ')[0];
				if (!double.TryParse(valueStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
				{
					WarningMessage = $"Błąd: '{valueStr}' nie jest poprawną wagą dla '{name}'!";
					return false;
				}
			}
			return true;
		}

		private void SaveClient()
		{
			Client.Products.Clear();
			// Return if all fields are empty
			if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Number) && string.IsNullOrWhiteSpace(Sale) && string.IsNullOrWhiteSpace(Order))
			{
				return;
			}
			Client.Name = string.IsNullOrWhiteSpace(Name) ? "" : Name;
			Client.PhoneNumber = string.IsNullOrWhiteSpace(Number) ? "" : Number;

			//Get ordered products
			Client.Products.AddRange(GetProductsFromText(_products, Order, true));

			//Get sold products
			Client.Products.AddRange(GetProductsFromText(_products, Sale, false));


			//Save client
			if (!_dataBase.GetAll<Client>().AsNoTracking().Any(x => x.ID == Client.ID))
				_dataBase.Add(Client);
			else
				_dataBase.Update(Client);

            // Time Tracking Mechanics for ClientSaleInfo
            var info = _dataBase.GetAll<ClientSaleInfo>().FirstOrDefault(x => x.ClientID == Client.ID && x.SaleID == _saleID);
            bool isNew = false;
            if (info == null)
            {
                info = new ClientSaleInfo { ID = Guid.NewGuid().ToString(), ClientID = Client.ID, SaleID = _saleID };
                isNew = true;
            }

            // Track First Order
            if (!info.FirstOrderTime.HasValue && Client.Products.Any(x => x.IsReserved))
            {
                info.FirstOrderTime = DateTime.Now;
            }
            
            // Track First Purchase
            if (!info.FirstPurchaseTime.HasValue && Client.Products.Any(x => !x.IsReserved))
            {
                info.FirstPurchaseTime = DateTime.Now;
            }

            // Apply Expected Arrival Time
            if (ExpectedArrivalTime.HasValue && ExpectedArrivalDate.HasValue)
            {
                info.ExpectedArrivalTime = ExpectedArrivalDate.Value.Date.Add(ExpectedArrivalTime.Value);
            }
            else
            {
                info.ExpectedArrivalTime = null;
            }

            if (isNew) _dataBase.Add(info);
            else _dataBase.Update(info);

			SaveClientOrder(_dataBase, Client, _saleID);
		}

		private void UpdateSaleSum()
		{
			if (string.IsNullOrWhiteSpace(Sale))
				return;

			SaleSum = "";
			List<string> sumOfOrderSections = new();
			double sum = 0;
			// Get sum of each order
			foreach (var line in Sale.Split('\n'))
			{
				// If line contains ':' it means that it is a product
				if (line.Contains(':'))
				{
					double.TryParse(line.Split(':')[1].Trim().Replace(",", ".").Split(' ')[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double productCost);
					sum += productCost;
				}
				else
				{
					sumOfOrderSections.Add(sum.ToString(CultureInfo.InvariantCulture));
					sum = 0;
				}
			}
			
			// Always add the trailing sum if the text didn't end with a newline comment break
			if (sum != 0 || sumOfOrderSections.Count == 0)
			{
			    sumOfOrderSections.Add(sum.ToString(CultureInfo.InvariantCulture));
			}
			
			SaleSum = string.Join(" + ", sumOfOrderSections);

			// Get sum of all orders safely
			double allSaleSum = SaleSum.Split('+')
			                           .Where(x => !string.IsNullOrWhiteSpace(x))
			                           .Sum(x => { double.TryParse(x.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val); return val; });
			                           
			SaleSum += $"{Environment.NewLine}Suma: {allSaleSum}";

			if (allSaleSum != 0)
			{
				// Get change
				SaleSum += $"       Reszta z {Math.Ceiling(allSaleSum / 100) * 100}: {Math.Ceiling(allSaleSum / 100) * 100 - allSaleSum}";
				SaleSum += $"  Reszta z {Math.Ceiling(allSaleSum / 50) * 50}: {Math.Ceiling(allSaleSum / 50) * 50 - allSaleSum}";
			}
		}

		#endregion Private Methods
	}
}