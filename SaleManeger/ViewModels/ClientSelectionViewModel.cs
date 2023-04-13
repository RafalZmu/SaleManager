using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ClientSelectionViewModel : ViewModelBase
    {
        public string SaleName { get; set; }
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
        public ObservableCollection<Client> AllClients { get; set; }
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

        public ReactiveCommand<string, Client> OpenClientEditionCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenProjectSelectionCommand { get; }

        private DataBase _dataBase;

        public ClientSelectionViewModel(string saleName, DataBase dataBase)
        {
            SaleName = saleName;
            _dataBase = dataBase;

            OpenClientEditionCommand = ReactiveCommand.Create<string, Client>(CreateNewClient);

            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });

            AllClients = Clients = _dataBase.GetClientsFromSale(saleName);
        }
        private Client CreateNewClient(string clientID)
        {
            if (!Guid.TryParse(clientID, out var id))
            {
                Client newClient = new Client()
                {
                    ID = Guid.NewGuid().ToString(),
                    Name = "",
                    PhoneNumber = "",
                    Products = new ObservableCollection<Product>()

                };
                return newClient;
            }
            var client = Clients.Where(x => x.ID == clientID).FirstOrDefault();
            return client;
        }
        private void FiltrClients()
        {
            if (string.IsNullOrWhiteSpace(ClientName))
            {
                Clients = new ObservableCollection<Client>(AllClients);
            }
            Clients = new ObservableCollection<Client>(AllClients.Where(x => x.Name.ToLower().Contains(ClientName.ToLower()) || x.PhoneNumber.Contains(ClientName)));

        }
    }
}
