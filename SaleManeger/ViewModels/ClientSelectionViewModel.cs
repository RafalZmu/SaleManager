using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ClientSelectionViewModel : ViewModelBase
    {
        #region Private Fields

        // The name of the client currently being searched for.
        private string _clientName;

        // Clients that match the current search term.
        private ObservableCollection<Client> _clients;

        private IProjectRepository _dataBase;
        private ObservableCollection<Product> _products;
        private string _productSearchText;
        private ObservableCollection<Product> _filteredProducts;

        #endregion Private Fields

        #region Public Properties

        public ObservableCollection<Client> AllClients { get; set; }

        // Extra filters on ordered amounts/values; all of them have to match (AND).
        public ObservableCollection<ClientFilterRule> AdvancedFilters { get; }

        // Product choices for the rules: a leading "all products" entry followed by the real products.
        public List<Product> FilterProducts { get; }

        // Filters set in the flyout: checked products, the order/sale restriction and valid advanced rules.
        // The name search is not counted, it is visible in its own box.
        public int ActiveFilterCount =>
            (Products?.Count(p => p.IsReserved) ?? 0)
            + (AreAllClientsShowing ? 0 : 1)
            + (AreSmsPendingShowing ? 1 : 0)
            + AdvancedFilters.Count(x => x.IsActive);

        public bool HasActiveFilters => ActiveFilterCount > 0;

        public ReactiveCommand<Unit, Unit> AddFilterCommand { get; }
        public ReactiveCommand<ClientFilterRule, Unit> RemoveFilterCommand { get; }
        public bool AreAllClientsShowing { get; set; } = true;
        public bool AreClientsWithOrderShowing { get; set; } = true;
        public bool AreClientsWithSaleShowing { get; set; } = false;

        // Show only clients marked "Dodać do wysyłki SMS" that have a phone number.
        public bool AreSmsPendingShowing { get; set; } = false;

        public string ClientName
        {
            get => _clientName;
            set
            {
                if (_clientName != value)
                {
                    this.RaiseAndSetIfChanged(ref _clientName, value);
                    FiltrClients();
                }
            }
        }

        public ObservableCollection<Client> Clients
        {
            get => _clients;
            set
            {
                if (_clients != value)
                {
                    this.RaiseAndSetIfChanged(ref _clients, value);
                }
            }
        }

        public ReactiveCommand<string, string> DeleteClientCommand { get; }
        public ReactiveCommand<string, Client> OpenClientEditionCommand { get; }
        public ReactiveCommand<Unit, string> OpenMoreSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenProjectSelectionCommand { get; }
        public ReactiveCommand<Unit, string> OpenSaleSummaryCommand { get; }

        public ObservableCollection<Product> Products
        {
            get => _products;
            set
            {
                if (_products != value)
                {
                    this.RaiseAndSetIfChanged(ref _products, value);
                    FiltrClients();
                    FiltrProducts();
                }
            }
        }

        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (_productSearchText != value)
                {
                    this.RaiseAndSetIfChanged(ref _productSearchText, value);
                    FiltrProducts();
                }
            }
        }

        public ObservableCollection<Product> FilteredProducts
        {
            get => _filteredProducts;
            set => this.RaiseAndSetIfChanged(ref _filteredProducts, value);
        }

        public List<Product> ProductsList { get; }
        public string SaleID { get; set; }
        public ReactiveCommand<Unit, List<bool>> UpdateClientsCommand { get; }

        #endregion Public Properties

        #region Public Constructors

        public ClientSelectionViewModel(string saleID, IProjectRepository dataBase, List<bool> selected,
            IEnumerable<ClientFilterRule> advancedFilters = null)
        {
            SaleID = saleID;
            _dataBase = dataBase;

            // Copies, so rules from a previous visit don't keep notifying the old view model.
            AdvancedFilters = new ObservableCollection<ClientFilterRule>(
                (advancedFilters ?? Enumerable.Empty<ClientFilterRule>()).Select(x => x.Clone()));
            AdvancedFilters.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (ClientFilterRule rule in e.NewItems)
                        rule.PropertyChanged += (_, _) => FiltrClients();
                FiltrClients();
            };
            AddFilterCommand = ReactiveCommand.Create(() => { AdvancedFilters.Add(new ClientFilterRule { SelectedProduct = FilterProducts[0] }); });
            RemoveFilterCommand = ReactiveCommand.Create<ClientFilterRule>(rule => { AdvancedFilters.Remove(rule); });

            OpenClientEditionCommand = ReactiveCommand.Create<string, Client>(CreateNewClient);
            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            OpenSaleSummaryCommand = ReactiveCommand.Create(() => { return saleID; });
            UpdateClientsCommand = ReactiveCommand.Create<List<bool>>(FiltrClients);
            DeleteClientCommand = ReactiveCommand.Create((string clientID) => { return clientID; });
            OpenMoreSettingsCommand = ReactiveCommand.Create(() => { return saleID; });

            AllClients = new ObservableCollection<Client>();
            ProductsList = _dataBase.GetAll<Product>().AsNoTracking().ToList();

            FilterProducts = new List<Product> { new Product { Name = "Wszystkie produkty" } };
            FilterProducts.AddRange(ProductsList.OrderBy(x => x.Code));
            // Rules kept from a previous visit point to old product instances; re-link them by ID.
            foreach (var rule in AdvancedFilters)
            {
                rule.SelectedProduct = FilterProducts.FirstOrDefault(x => x.ID != null && x.ID == rule.SelectedProduct?.ID)
                    ?? FilterProducts[0];
                rule.PropertyChanged += (_, _) => FiltrClients();
            }

            // Get all clients and their orders.
            var clients = _dataBase.GetAll<Client>().ToList();
            clients.ForEach(x => x.Products = new ObservableCollection<Product>());
            var clientsOrders = _dataBase.GetAll<ClientOrder>().Where(x => x.SaleID == SaleID).ToList();
            var clientsOrdersList = clientsOrders.GroupBy(x => x.ClientID).Select(y => y.ToList()).ToList();
            foreach (var clientOrder in clientsOrdersList)
            {
                ObservableCollection<Product> orders = new();
                foreach (var order in clientOrder)
                {
                    if (order.ProductID == "Comment")
                    {
                        orders.Add(new Product()
                        {
                            Name = "",
                            ID = "Comment",
                            IsReserved = order.IsReserved,
                            PricePerKg = 1,
                            Value = order.Value,
                            Code = "",
                        });
                        continue;
                    }
                    Product product = ProductsList.FirstOrDefault(x => order.ProductID == x.ID);
                    // The product was removed after this order was created.
                    if (product == null)
                        continue;

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
                var orderOwner = clients.FirstOrDefault(x => x.ID == clientOrder[0].ClientID);
                if (orderOwner != null)
                    orderOwner.Products = orders;
            }
            clients.ForEach(x => x.Products ??= new ObservableCollection<Product>());
            AllClients = new ObservableCollection<Client>(clients);

            AllClients ??= new ObservableCollection<Client>();
            Clients = AllClients;

            //Get products that were selected when this window was last opened.
            Products = new ObservableCollection<Product>(ProductsList);
            if (selected != null)
            {
                int i;
                for (i = 0; i < Products.Count; i++)
                {
                    Products[i].IsReserved = selected[i];
                }
                AreClientsWithOrderShowing = selected[i];
                AreClientsWithSaleShowing = selected[i + 1];
                AreAllClientsShowing = selected[i + 2];
                // Lists saved before this filter existed do not have the extra entry.
                AreSmsPendingShowing = selected.Count > i + 3 && selected[i + 3];
            }

            // Get all clients for the current sale and set their colors based on their product reservations.
            var saleInfos = _dataBase.GetAll<ClientSaleInfo>().Where(x => x.SaleID == SaleID).ToList();
            foreach (var client in AllClients)
            {
                client.Color = client.Products.Any(x => x.IsReserved == false) ? "Green" :
                    client.Products.Any(x => x.IsReserved == true) ? "Red" : "White";
                
                var info = saleInfos.FirstOrDefault(x => x.ClientID == client.ID);
                client.ExpectedArrivalTime = info?.ExpectedArrivalTime;
            }
            FiltrClients();
            FiltrProducts();
        }

        #endregion Public Constructors

        #region Private Methods

        // Create a new client with the given ID.
        private Client CreateNewClient(string clientID)
        {
            if (!Guid.TryParse(clientID, out var id))
            {
                // If the ID is not a valid GUID, create a new client with a new GUID.
                Client newClient = new()
                {
                    ID = Guid.NewGuid().ToString(),
                    Name = "",
                    PhoneNumber = "",
                    Products = new ObservableCollection<Product>()
                };
                return newClient;
            }
            // If the ID is valid, return the existing client with that ID.
            var client = Clients.Where(x => x.ID == clientID).FirstOrDefault();
            return client;
        }

        // Filter the clients based on the current search term.
        private List<bool> FiltrClients()
        {
            Clients = OrderAndSaleFiltr(AdvancedFiltr(ProductFiltr(SmsPendingFiltr(NameAndNumberFiltr(AllClients)))));
            var tempList = new List<bool>();
            foreach (var product in Products)
            {
                tempList.Add(product.IsReserved);
            }
            tempList.Add(AreClientsWithOrderShowing);
            tempList.Add(AreClientsWithSaleShowing);
            tempList.Add(AreAllClientsShowing);
            tempList.Add(AreSmsPendingShowing);
            this.RaisePropertyChanged(nameof(ActiveFilterCount));
            this.RaisePropertyChanged(nameof(HasActiveFilters));
            return tempList;
        }

        private void FiltrProducts()
        {
            if (Products == null) return;

            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                FilteredProducts = new ObservableCollection<Product>(Products.OrderBy(x => x.Code));
            }
            else
            {
                var lowerSearch = ProductSearchText.ToLower();
                FilteredProducts = new ObservableCollection<Product>(
                    Products.Where(p => 
                        (p.Name != null && p.Name.ToLower().Contains(lowerSearch)) || 
                        (p.Code != null && p.Code.ToLower().Contains(lowerSearch))
                    ).OrderBy(x => x.Code)
                );
            }
        }

        private ObservableCollection<Client> SmsPendingFiltr(ObservableCollection<Client> clients)
        {
            return AreSmsPendingShowing
                ? new ObservableCollection<Client>(clients.Where(x => x.AddToSmsGroup && !string.IsNullOrWhiteSpace(x.PhoneNumber)))
                : clients;
        }

        private ObservableCollection<Client> NameAndNumberFiltr(ObservableCollection<Client> clients)
        {
            return string.IsNullOrWhiteSpace(ClientName) ?
                 clients : new ObservableCollection<Client>(clients.Where(x => x.Name.ToLower().Contains(ClientName.ToLower()) || x.PhoneNumber.Contains(ClientName)));
        }

        private int GetClientPriority(Client client)
        {
            bool hasOrder = client.Products.Any(y => y.IsReserved == true);
            bool hasSale = client.Products.Any(y => y.IsReserved == false);

            if (hasOrder && !hasSale) return 1;
            if (hasSale) return 2;
            return 3;
        }

        private ObservableCollection<Client> OrderAndSaleFiltr(ObservableCollection<Client> clients)
        {
            IEnumerable<Client> filteredClients = clients;

            if (!AreAllClientsShowing)
            {
                filteredClients = (AreClientsWithOrderShowing, AreClientsWithSaleShowing) switch
                {
                    (true, true) => clients.Where(x => x.Products.Any(y => y.IsReserved == false) && x.Products.Any(y => y.IsReserved == true)),
                    (true, false) => clients.Where(x => x.Products.All(y => y.IsReserved == true) && x.Products.Count != 0),
                    (false, true) => clients.Where(x => x.Products.All(y => y.IsReserved == false) && x.Products.Count != 0),
                    _ => Enumerable.Empty<Client>()
                };
            }

            var sortedClients = filteredClients
                .OrderBy(GetClientPriority)
                .ThenBy(x => x.ExpectedArrivalTime.HasValue ? Math.Abs((x.ExpectedArrivalTime.Value - DateTime.Now).Ticks) : long.MaxValue)
                .ThenByDescending(x => x.Products.Count);
                
            return new ObservableCollection<Client>(sortedClients);
        }

        private ObservableCollection<Client> AdvancedFiltr(ObservableCollection<Client> clients)
        {
            var activeRules = AdvancedFilters.Where(x => x.IsActive).ToList();
            return activeRules.Count == 0 ? clients : new ObservableCollection<Client>(clients.Where(c => activeRules.All(r => r.Matches(c))));
        }

        private ObservableCollection<Client> ProductFiltr(ObservableCollection<Client> clients)
        {
            var reservedProductIds = Products.Where(p => p.IsReserved).Select(p => p.Code).ToList();
            return reservedProductIds.Count != 0 ? new ObservableCollection<Client>(clients.Where(c => c.Products.Any(cp => reservedProductIds.Contains(cp.Code)))) : clients;
        }

        #endregion Private Methods
    }
}