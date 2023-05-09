﻿using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ClientEditionViewModel : ViewModelBase
    {
        #region Private properties

        private string _order;
        private List<Product> _products;
        private string _sale;
        private string _saleID;
        private string _saleSum;

        #endregion Private properties

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

            _products = _dataBase.GetAll<Product>().AsNoTracking().ToList();
            _products.ForEach(p =>
            {
                Codes += $"{p.Code}-{p.Name}{Environment.NewLine}";
            });

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
        }

        #endregion Public Constructors

        #region Public properties

        public Client Client { get; set; }
        public string ClientID { get; set; }
        public string Codes { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public ReactiveCommand<Unit, string> OpenClientSelectionCommand { get; }

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

        public string SaleSum
        {
            get => _saleSum;
            set
            {
                this.RaiseAndSetIfChanged(ref _saleSum, value);
            }
        }

        private IProjectRepository _dataBase { get; set; }

        #endregion Public properties

        #region Private Methods

        private void GetProductsFromInput(bool IsOrder)
        {
            var text = IsOrder ? Order : Sale;
            if (string.IsNullOrWhiteSpace(text))
                return;

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
                        IsReserved = IsOrder
                    };
                    Client.Products.Add(comment);
                    continue;
                }
                var name = item.Split(":")[0].Trim();
                var value = item.Split(':')[1].Trim();
                var code = _products.First(x => x.Name == name).Code;
                var ID = _products.First(x => x.Code == code).ID;
                Product product = new()
                {
                    ID = ID,
                    Name = name,
                    Code = code,
                    Value = value,
                    IsReserved = IsOrder
                };
                Client.Products.Add(product);
            }
        }

        private string OpenClientSelection()
        {
            SaveClient();
            return _saleID;
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
            GetProductsFromInput(true);

            //Get sold products
            GetProductsFromInput(false);

            //Save client
            if (!_dataBase.GetAll<Client>().AsNoTracking().Any(x => x.ID == Client.ID))
                _dataBase.Add<Client>(Client);
            else
                _dataBase.Update<Client>(Client);

            //Delete old client orders
            foreach (var item in _dataBase.GetAll<ClientOrder>().Where(x => x.ClientID == Client.ID && x.SaleID == _saleID))
            {
                _dataBase.Delete<ClientOrder>(item);
            }
            _dataBase.Save();

            //Add new client orders
            foreach (var order in Client.Products)
            {
                if (order.ID == "")
                {
                    order.ID = "Comment";
                }
                _dataBase.Add(new ClientOrder()
                {
                    ClientID = Client.ID,
                    ClientOrderID = Guid.NewGuid().ToString(),
                    ProductID = order.ID,
                    SaleID = _saleID,
                    Date = DateTime.Now,
                    Value = order.Value,
                    IsReserved = order.IsReserved
                });
                _dataBase.Save();
            }
        }

        private void UpdateSaleSum()
        {
            if (string.IsNullOrWhiteSpace(Sale))
                return;

            SaleSum = "";
            List<string> sumOfOrder = new List<string>();
            double sum = 0;
            foreach (var line in Sale.Split("\n"))
            {
                if (line.Contains(':'))
                {
                    double.TryParse(line.Split(":")[1].Trim().Replace(",", "."), out double productCost);
                    sum += productCost;
                }
                else
                {
                    sumOfOrder.Add(sum.ToString());
                    sum = 0;
                }
            }
            SaleSum = string.Join(" + ", sumOfOrder);

            double allSaleSum = 0;
            if (SaleSum.Contains('+'))
            {
                SaleSum.Replace(" ", "");
                foreach (var num in SaleSum.Split('+'))
                {
                    double.TryParse(num, out var value);
                    allSaleSum += value;
                }
                SaleSum += $"{Environment.NewLine}Suma: {allSaleSum}";
            }
            if (allSaleSum != 0)
            {
                SaleSum += $"       Reszta z {Math.Ceiling(allSaleSum / 100) * 100}: {Math.Ceiling(allSaleSum / 100) * 100 - allSaleSum}";
                SaleSum += $"  Reszta z {Math.Ceiling(allSaleSum / 50) * 50}: {Math.Ceiling(allSaleSum / 50) * 50 - allSaleSum}";
            }
        }

        #endregion Private Methods
    }
}