using Avalonia.FreeDesktop.DBusIme;
using Avalonia.Interactivity;
using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ClientSelectionViewModel : ViewModelBase
    {
        // The name of the current sale.
        public string SaleName { get; set; }
        public bool AreClientsWithSaleShowing { get; set; } = false;
        public bool AreClientsWithOrderShowing { get; set; } = true;
        public bool AreAllClientsShowing { get; set; } = true;
        public ReactiveCommand<string, Client> OpenClientEditionCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenProjectSelectionCommand { get; } 
        public ReactiveCommand<Unit, string> OpenSaleSummaryCommand { get; }
        public ReactiveCommand<Unit,Unit> UpdateClientsCommand { get; }
        // All clients in the current sale.
        public ObservableCollection<Client> AllClients { get; set; }

        // The name of the client currently being searched for.
        private string _clientName;
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

        // Clients that match the current search term.
        private ObservableCollection<Client> _clients;
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
        private ObservableCollection<Product> _products;
        public ObservableCollection<Product> Products
        {
            get => _products;
            set
            {
                if(_products != value)
                {
                    this.RaiseAndSetIfChanged(ref _products, value);
                    FiltrClients();
                }
            }
        }


        private DataBase _dataBase;

        public ClientSelectionViewModel(string saleName, DataBase dataBase)
        {
            SaleName = saleName;
            _dataBase = dataBase;

            OpenClientEditionCommand = ReactiveCommand.Create<string, Client>(CreateNewClient);
            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            OpenSaleSummaryCommand = ReactiveCommand.Create(OpenSaleSummary);
            UpdateClientsCommand = ReactiveCommand.Create(FiltrClients);

            AllClients = Clients = _dataBase.GetClientsFromSale(saleName);
            Products = new ObservableCollection<Product>(_dataBase.GetProducts());

            // Get all clients for the current sale and set their colors based on their product reservations.
            foreach (var client in AllClients)
            {
                if (client.Products.Any(x => x.IsReserved == true) && client.Products.Any(x => x.IsReserved == false))
                {
                    client.Color = "Green";
                }
                else if (client.Products.Any(x => x.IsReserved == true) && !client.Products.Any(x => x.IsReserved == false))
                {
                    client.Color = "Red";
                }
                else
                {
                    client.Color = "White";
                }
            }
        }

        // Create a new client with the given ID.
        private Client CreateNewClient(string clientID)
        {
            if (!Guid.TryParse(clientID, out var id))
            {
                // If the ID is not a valid GUID, create a new client with a new GUID.
                Client newClient = new Client()
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
        private void FiltrClients()
        {
            Clients = OrderAndSaleFiltr(ProductFiltr(NameAndNumberFiltr(AllClients)));
        }
        private ObservableCollection<Client> ProductFiltr(ObservableCollection<Client> clients)
        {
            var reservedProductIds = Products.Where(p => p.IsReserved).Select(p => p.Code).ToList();
            if(reservedProductIds.Count != 0)
            {
                return new ObservableCollection<Client>(clients.Where(c => c.Products.Any(cp => reservedProductIds.Contains(cp.Code))));
            }
            else
            {
                return clients;
            }
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
                (true, true) => new ObservableCollection<Client>(clients.Where(x => x.Products.Any(y => y.IsReserved == false) && x.Products.Any(y => y.IsReserved == true)).OrderByDescending(x=>x.Products.Count)),
                (true, false) => new ObservableCollection<Client>(clients.Where(x => x.Products.All(y => y.IsReserved == true)).OrderByDescending(x=>x.Products.Count)),
                (false, true) => new ObservableCollection<Client>(clients.Where(x => x.Products.All(y => y.IsReserved == false)).OrderByDescending(x=>x.Products.Count)),
                _ => new ObservableCollection<Client>(),
            };
        }
        private string OpenSaleSummary()
        {
            return SaleName;
        }
    }
}

