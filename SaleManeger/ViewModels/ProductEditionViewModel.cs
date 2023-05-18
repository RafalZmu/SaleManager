using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;

namespace SaleManeger.ViewModels
{
    public class ProductEditionViewModel : ViewModelBase
    {
        #region Public Properties

        public ObservableCollection<Product> Products { get; set; }
        public ReactiveCommand<Unit, Unit> SaveToDataBaseCommand { get; set; }

        #endregion Public Properties

        #region Private Properties

        private IProjectRepository _dataBase { get; set; }

        #endregion Private Properties

        #region Public Constructors

        public ProductEditionViewModel(IProjectRepository db)
        {
            _dataBase = db;
            Products = new ObservableCollection<Product>(db.GetAll<Product>().OrderBy(x => x.Code).ToList());
            SaveToDataBaseCommand = ReactiveCommand.Create(() => SaveToDataBase(_dataBase, Products));
        }

        #endregion Public Constructors

        #region Public Methods

        public void AddProduct()
        {
            Products.Add(new Product()
            {
                ID = Guid.NewGuid().ToString(),
                Name = "",
                Code = "",
                PricePerKg = 1
            });
        }

        public void SaveToDataBase(IProjectRepository dataBase, ObservableCollection<Product> productsList)
        {
            //Clear products table and replace it with updated products
            var random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";

            // Remove products with empty name and code
            productsList = new ObservableCollection<Product>(productsList.Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Code)));

            foreach (var item in productsList)
            {
                if (item.PricePerKg <= 0)
                    item.PricePerKg = 1;

                if (string.IsNullOrEmpty(item.Name))
                    item.Name = "Podaj nazwe przedmiotu";

                // Check if there are two products with the same code if there are, generate a new code
                if (productsList.Where(x => x.Code == item.Code).Count() > 1)
                {
                    // Generate a random two-character string
                    string code = new string(Enumerable.Repeat(chars, 2)
                        .Select(s => s[random.Next(s.Length)]).ToArray());

                    // Check if the code already exists in the database
                    while (productsList.Any(p => p.Code == code))
                    {
                        // If the code already exists, generate a new one
                        code = new string(Enumerable.Repeat(chars, 2)
                            .Select(s => s[random.Next(s.Length)]).ToArray());
                    }
                    item.Code = code;
                }
            }

            foreach (var product in dataBase.GetAll<Product>().ToList())
            {
                dataBase.Delete(product);
            }
            dataBase.Save();

            foreach (var product in productsList)
            {
                dataBase.Add(product);
            }

            dataBase.Save();
        }

        #endregion Public Methods
    }
}