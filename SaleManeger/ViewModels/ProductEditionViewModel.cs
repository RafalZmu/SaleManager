using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class ProductEditionViewModel : ViewModelBase
    {
        public ObservableCollection<Product> Products { get; set; }
        public ReactiveCommand<Unit, Unit> SaveToDataBaseCommand { get; set; }
        private DataBase _dataBase { get; set; }

        public ProductEditionViewModel(DataBase db)
        {
            _dataBase = db;
            Products = new ObservableCollection<Product>(_dataBase.GetProducts());
            SaveToDataBaseCommand = ReactiveCommand.Create(SaveToDataBase);
        }

        public void AddProduct()
        {
            Products.Add(new Product("", 1, ""));
        }
        public void SaveToDataBase()
        {
            //Clear products table and replace it with updated products
            var random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            foreach (var item in Products)
            {
                if (item.PricePerKg <= 0)
                    item.PricePerKg = 1;

                if (string.IsNullOrEmpty(item.Name))
                    item.Name = "Podaj nazwe przedmiotu";


                if (Products.Where(x => x.Code == item.Code).Count() > 1)
                {
                    // Generate a random two-character string
                    string code = new string(Enumerable.Repeat(chars, 2)
                        .Select(s => s[random.Next(s.Length)]).ToArray());

                    // Check if the code already exists in the database
                    while (Products.Any(p => p.Code == code))
                    {
                        // If the code already exists, generate a new one
                        code = new string(Enumerable.Repeat(chars, 2)
                            .Select(s => s[random.Next(s.Length)]).ToArray());
                    }
                    item.Code = code;
                }
            }

            _dataBase.AddProductsToDatabase(Products);
        }
    }
}
