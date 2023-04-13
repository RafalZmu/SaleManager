using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ClientEditionViewModel : ViewModelBase
    {
        public string ClientID { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string Order { get; set; }
        private string _sale;
        public string Sale
        {
            get => _sale;
            set
            {
                if (_sale != value)
                {
                    this.RaiseAndSetIfChanged(ref _sale, value);
                }
            }
        }
        public string Codes { get; set; }
        public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; }
        private string _saleName;
        private List<Product> _products;
        private DataBase _dataBase { get; set; }

        public ClientEditionViewModel(DataBase db, Client client, string saleName)
        {
            _dataBase = db;
            _saleName = saleName;
            OpenClientSelectionCommand = ReactiveCommand.Create(OpenClientSelection);
            Name = client.Name;
            Number = client.PhoneNumber;
            ClientID = client.ID;
            _products = _dataBase.GetProducts();

            foreach (var item in client.Products)
            {
                if (item.IsReserved)
                {
                    if (item.Name == "")
                        Order += $"{item.Value.Trim()}{Environment.NewLine}";
                    else
                        Order += $"{item.Name}: {item.Value}{Environment.NewLine}";
                }
                else
                {
                    if (item.Name == "")
                        Sale += $"{item.Value.Trim()}{Environment.NewLine}";
                    else
                        Sale += $"{item.Name}: {item.Value}{Environment.NewLine}";
                }
            }
        }
        private string OpenClientSelection()
        {
            SaveClient();
            return _saleName;
        }
        private void SaveClient()
        {
            if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Number) && string.IsNullOrWhiteSpace(Sale) && string.IsNullOrWhiteSpace(Order))
            {
                return;
            }
            var client = new Client()
            {
                ID = ClientID,
                Name = string.IsNullOrWhiteSpace(Name) ? "Podaj imie" : Name,
                PhoneNumber = string.IsNullOrWhiteSpace(Number) ? "Podaj numer" : Number,
                Products = new ObservableCollection<Product>()
            };


            //Get ordered products
            if (!string.IsNullOrWhiteSpace(Order))
            {
                foreach (var item in Order.Trim().Split("\n"))
                {
                    if (!item.Contains(':'))
                    {
                        Product comment = new()
                        {
                            Name = "",
                            Code = "",
                            Value = item,
                            IsReserved = true
                        };
                        client.Products.Add(comment);
                        continue;

                    }
                    var name = item.Split(":")[0].Trim();
                    var code = _products.Where(x => x.Name == name).First().Code;
                    Product product = new()
                    {
                        Name = name,
                        Code = code,
                        Value = item.Split(':')[1].Trim(),
                        IsReserved = true
                    };
                    client.Products.Add(product);

                }
            }

            //Get sold products
            if (!string.IsNullOrWhiteSpace(Sale))
            {
                foreach (var item in Sale.Trim().Split("\n"))
                {
                    if (!item.Contains(':'))
                    {
                        Product comment = new()
                        {
                            Name = "",
                            Code = "",
                            Value = item,
                            IsReserved = false
                        };
                        client.Products.Add(comment);
                        continue;

                    }
                    double.TryParse(item.Split(':')[1].Trim(), out var value);
                    var name = item.Split(":")[0].Trim();
                    var code = _products.Where(x => x.Name == name).First().Code;
                    Product product = new()
                    {
                        Name = name,
                        Code = code,
                        Value = item.Split(':')[1].Trim(),
                        IsReserved = false
                    };
                    client.Products.Add(product);

                }
            }

            _dataBase.UpdateOrCreateClient(client, _saleName);

        }
    }
}
