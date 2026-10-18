using Avalonia.Media;
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
	public class TagOption : ReactiveObject
	{
		private bool _isSelected;
		public string Name { get; set; }

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}
	}

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
		private string _name;
		private string _number;
		private bool _addToSmsGroup;
		private bool _isDirty;
		private bool _closeWarned;
		private string _autoSaveStatus;

		public string Name
		{
			get => _name;
			set { if (_name != value) { this.RaiseAndSetIfChanged(ref _name, value); MarkDirty(); } }
		}

		public string Number
		{
			get => _number;
			set { if (_number != value) { this.RaiseAndSetIfChanged(ref _number, value); MarkDirty(); } }
		}

		public bool AddToSmsGroup
		{
			get => _addToSmsGroup;
			set { if (_addToSmsGroup != value) { this.RaiseAndSetIfChanged(ref _addToSmsGroup, value); MarkDirty(); } }
		}

		// True when something on the screen has not reached the database yet.
		public bool IsDirty
		{
			get => _isDirty;
			private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
		}

		// What the last save did, shown small next to the name ("Zapisano automatycznie 12:30:05").
		public string AutoSaveStatus
		{
			get => _autoSaveStatus;
			private set => this.RaiseAndSetIfChanged(ref _autoSaveStatus, value);
		}

		private string _notes;
		private string _newTagText;

		public string Notes
		{
			get => _notes;
			set
			{
				if (_notes == value) return;
				this.RaiseAndSetIfChanged(ref _notes, value);
				this.RaisePropertyChanged(nameof(NotesHeader));
				MarkDirty();
			}
		}

		// All tags offered for this client (suggested ones, the ones other clients use and this client's own).
		public ObservableCollection<TagOption> TagOptions { get; } = new();

		public string NewTagText
		{
			get => _newTagText;
			set => this.RaiseAndSetIfChanged(ref _newTagText, value);
		}

		public string SelectedTagsText => ClientTags.Join(TagOptions.Where(x => x.IsSelected).Select(x => x.Name));

		// The section is collapsed, so the header says what is inside.
		public string NotesHeader
		{
			get
			{
				var parts = ClientTags.Parse(SelectedTagsText).ToList();
				if (!string.IsNullOrWhiteSpace(Notes))
					parts.Add("notatka");
				return parts.Count == 0 ? "Notatki i tagi" : $"Notatki i tagi ({string.Join(", ", parts)})";
			}
		}

		public ReactiveCommand<Unit, Unit> AddTagCommand { get; }
		private List<SaleProduct> _curruntlyAvailableProducts; 
		public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleDisplayModeCommand { get; }
        
        private bool _isShowingWolne = false;
        // The button names the view you will switch TO; the header below it names the view you are looking at.
        public string DisplayModeText => _isShowingWolne ? "Przełącz na: Zamówione" : "Przełącz na: Wolne";
        public string CodesHeader => _isShowingWolne ? "WOLNE (na stanie)" : "ZAMÓWIONE (razem)";
        public string CodesHint => _isShowingWolne
            ? "Dostępne minus zamówienia wszystkich klientów. Wartość ujemna = brakuje."
            : "Łącznie zamówione przez wszystkich klientów i jeszcze niekupione.";
        public IBrush CodesHeaderBrush => _isShowingWolne ? Brushes.LightGreen : Brushes.Orange;
        
        private List<Product> _requiredProducts;

		public string Order
		{
			get => _order;
			set
			{
				if (_order != value)
				{
					this.RaiseAndSetIfChanged(ref _order, value, nameof(Order));
					MarkDirty();
				}
			}
		}

		public string Sale
		{
			get => _sale;
			set
			{
				if (_sale != value)
					MarkDirty();
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
                MarkDirty();
            }
        }

        public TimeSpan? ExpectedArrivalTime
        {
            get => _expectedArrivalTime;
            set 
            {
                this.RaiseAndSetIfChanged(ref _expectedArrivalTime, value);
                _hasIgnoredTimeWarning = false; 
                MarkDirty();
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
			_name = client.Name;
			_number = client.PhoneNumber;
			_addToSmsGroup = client.AddToSmsGroup;
			_notes = client.Notes;
			ClientID = client.ID;
			Client = client;

			OpenClientSelectionCommand = ReactiveCommand.Create(OpenClientSelection);
			AddTagCommand = ReactiveCommand.Create(AddTag);

			var ownTags = ClientTags.Parse(client.Tags);
			var knownTags = ClientTags.AllKnown(_dataBase.GetAll<Client>().ToList());
			foreach (var tag in ownTags.Where(t => !knownTags.Contains(t, StringComparer.CurrentCultureIgnoreCase)))
				knownTags.Add(tag);
			foreach (var tag in knownTags)
				AddTagOption(tag, ownTags.Contains(tag, StringComparer.CurrentCultureIgnoreCase));

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

			// Filling the boxes from the database is not an edit.
			IsDirty = false;
		}

        #endregion Public Constructors

        #region Public Methods

		/// <summary>
		/// Saves what is on screen when something changed (called by a timer). A half-typed order, e.g. "Jabłka:" without
		/// a weight, is not saved and its warning is not shown: the user is still typing.
		/// Returns true when nothing is left unsaved.
		/// </summary>
		public bool AutoSave()
		{
			if (!IsDirty)
				return true;
			if (!TrySave())
			{
				AutoSaveStatus = "Autozapis czeka na poprawne dane";
				return false;
			}
			AutoSaveStatus = $"Zapisano automatycznie {DateTime.Now:HH:mm:ss}";
			return true;
		}

		/// <summary>Ctrl+S: saves now and stays on the screen. Errors are shown like when leaving the screen.</summary>
		public bool SaveNow()
		{
			if (!TrySave(showWarnings: true))
				return false;
			WarningMessage = string.Empty;
			AutoSaveStatus = $"Zapisano {DateTime.Now:HH:mm:ss}";
			return true;
		}

		/// <summary>
		/// Called when the window is about to close. Saves unsaved work; when that is not possible the first attempt is
		/// refused with a warning and the second one lets the window close.
		/// </summary>
		public bool CanCloseWindow()
		{
			if (!IsDirty || AutoSave())
				return true;
			if (_closeWarned)
				return true;
			_closeWarned = true;
			if (!ValidateText(Order) || !ValidateText(Sale))
				WarningMessage = $"{WarningMessage} Zamknij program ponownie, aby wyjść bez zapisu.";
			else
				WarningMessage = "Nie udało się zapisać zmian. Zamknij program ponownie, aby wyjść bez zapisu.";
			return false;
		}

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

		private void MarkDirty()
		{
			IsDirty = true;
			_closeWarned = false;
		}

		private bool TrySave(bool showWarnings = false)
		{
			var previousWarning = WarningMessage;
			if (!ValidateText(Order) || !ValidateText(Sale))
			{
				if (!showWarnings)
					WarningMessage = previousWarning;
				return false;
			}

			try
			{
				// Same parsing as the save itself; an unknown product name throws here, before anything is written.
				GetProductsFromText(_products, Order, true);
				GetProductsFromText(_products, Sale, false);
				SaveClient();
			}
			catch (InvalidOperationException)
			{
				return false;
			}
			return true;
		}

		private TagOption AddTagOption(string name, bool selected)
		{
			var option = new TagOption { Name = name, IsSelected = selected };
			option.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(TagOption.IsSelected))
				{
					this.RaisePropertyChanged(nameof(NotesHeader));
					MarkDirty();
				}
			};
			TagOptions.Add(option);
			return option;
		}

		private void AddTag()
		{
			var typed = ClientTags.Parse(NewTagText);
			NewTagText = "";
			foreach (var tag in typed)
			{
				var existing = TagOptions.FirstOrDefault(x => string.Equals(x.Name, tag, StringComparison.CurrentCultureIgnoreCase));
				if (existing != null)
					existing.IsSelected = true;
				else
					AddTagOption(tag, true);
			}
			this.RaisePropertyChanged(nameof(NotesHeader));
		}

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
            this.RaisePropertyChanged(nameof(CodesHeader));
            this.RaisePropertyChanged(nameof(CodesHint));
            this.RaisePropertyChanged(nameof(CodesHeaderBrush));
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
				if (!OrderValue.TryParse(valueStr, out _))
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
				IsDirty = false;
				return;
			}
			Client.Name = string.IsNullOrWhiteSpace(Name) ? "" : Name;
			Client.PhoneNumber = string.IsNullOrWhiteSpace(Number) ? "" : Number;
			Client.AddToSmsGroup = AddToSmsGroup;
			Client.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
			Client.Tags = SelectedTagsText.Length == 0 ? null : SelectedTagsText;

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
			IsDirty = false;
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
					sum += OrderValue.Parse(line.Split(':')[1]);
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