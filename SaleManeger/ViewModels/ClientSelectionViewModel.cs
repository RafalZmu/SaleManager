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

        #endregion Private Fields

        #region Public Properties

        public ObservableCollection<Client> AllClients { get; set; }
        public bool AreAllClientsShowing { get; set; } = true;
        public bool AreClientsWithOrderShowing { get; set; } = true;
        public bool AreClientsWithSaleShowing { get; set; } = false;

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
                }
            }
        }

        public List<Product> ProductsList { get; }
        public string SaleID { get; set; }
        public ReactiveCommand<Unit, List<bool>> UpdateClientsCommand { get; }

        #endregion Public Properties

        #region Public Constructors

        public ClientSelectionViewModel(string saleID, IProjectRepository dataBase, List<bool> selected)
        {
            SaleID = saleID;
            _dataBase = dataBase;

            OpenClientEditionCommand = ReactiveCommand.Create<string, Client>(CreateNewClient);
            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            OpenSaleSummaryCommand = ReactiveCommand.Create(() => { return saleID; });
            UpdateClientsCommand = ReactiveCommand.Create<List<bool>>(FiltrClients);
            DeleteClientCommand = ReactiveCommand.Create((string clientID) => { return clientID; });

            AllClients = new ObservableCollection<Client>();
            ProductsList = _dataBase.GetAll<Product>().AsNoTracking().ToList();

            // Get all clients and their orders.
            var clients = _dataBase.GetAll<Client>().ToList();
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
            }

            // Get all clients for the current sale and set their colors based on their product reservations.
            foreach (var client in AllClients)
            {
                client.Color = client.Products.Any(x => x.IsReserved == false) ? "Green" :
                    client.Products.Any(x => x.IsReserved == true) ? "Red" : "White";
            }
            FiltrClients();
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
            Clients = OrderAndSaleFiltr(ProductFiltr(NameAndNumberFiltr(AllClients)));
            var tempList = new List<bool>();
            foreach (var product in Products)
            {
                tempList.Add(product.IsReserved);
            }
            tempList.Add(AreClientsWithOrderShowing);
            tempList.Add(AreClientsWithSaleShowing);
            tempList.Add(AreAllClientsShowing);
            return tempList;
        }

        private ObservableCollection<Client> NameAndNumberFiltr(ObservableCollection<Client> clients)
        {
            return string.IsNullOrWhiteSpace(ClientName) ?
                 clients : new ObservableCollection<Client>(clients.Where(x => x.Name.ToLower().Contains(ClientName.ToLower()) || x.PhoneNumber.Contains(ClientName)));
        }

        private ObservableCollection<Client> OrderAndSaleFiltr(ObservableCollection<Client> clients)
        {
            if (AreAllClientsShowing)
                return clients;

            return (AreClientsWithOrderShowing, AreClientsWithSaleShowing) switch
            {
                (true, true) => new ObservableCollection<Client>(clients.Where(x => x.Products.Any(y => y.IsReserved == false) && x.Products.Any(y => y.IsReserved == true)).OrderByDescending(x => x.Products.Count)),
                (true, false) => new ObservableCollection<Client>(clients.Where(x => x.Products.All(y => y.IsReserved == true) && x.Products.Count != 0).OrderByDescending(x => x.Products.Count)),
                (false, true) => new ObservableCollection<Client>(clients.Where(x => x.Products.All(y => y.IsReserved == false) && x.Products.Count != 0).OrderByDescending(x => x.Products.Count)),
                _ => new ObservableCollection<Client>(),
            };
        }

        private ObservableCollection<Client> ProductFiltr(ObservableCollection<Client> clients)
        {
            var reservedProductIds = Products.Where(p => p.IsReserved).Select(p => p.Code).ToList();
            return reservedProductIds.Count != 0 ? new ObservableCollection<Client>(clients.Where(c => c.Products.Any(cp => reservedProductIds.Contains(cp.Code)))) : clients;
        }

        #endregion Private Methods
    }
}