using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class CurrentProductStateViewModel : ViewModelBase
    {
        private IProjectRepository _database;
        private string _saleId;
        private List<Product> _products;

        public ReactiveCommand<Unit, string> OpenMoreSettingsCommand { get; set; }

        public ObservableCollection<SaleProduct> saleProducts { get; set; }

        public CurrentProductStateViewModel(IProjectRepository database, string saleId)
        {
            _database = database;
            _saleId = saleId;

            OpenMoreSettingsCommand = ReactiveCommand.Create(OpenOptions);

            _products = new List<Product>(_database.GetAll<Product>());

            saleProducts = new ObservableCollection<SaleProduct>(_database.GetAll<SaleProduct>().Where(x => x.SaleID == _saleId));

            saleProducts = CreateSaleItems(_products, saleId, saleProducts, database);

        }

        private string OpenOptions()
        {
            SaveSaleProducts();

            return _saleId;
        }

        private void SaveSaleProducts()
        {
            foreach (var item in saleProducts)
            {
                _database.Update(item);
            }
            _database.Save();
            
        }

        public static ObservableCollection<SaleProduct> CreateSaleItems(List<Product> products, string saleID, ObservableCollection<SaleProduct> saleProducts, IProjectRepository database)
        {
            if (saleProducts == null || saleProducts.Count != products.Count)
            {
                foreach (var product in products)
                {
                    if (saleProducts.Any(x => x.ProductID == product.ID))
                    {
                        continue;
                    }
                    SaleProduct saleProduct = new SaleProduct()
                    {
                        ID = Guid.NewGuid().ToString(),
                        ProductID = product.ID,
                        ProductName = product.Name,
                        ProductCode = product.Code,
                        SaleID = saleID,
                        Amount = 0,
                        PricePerKg = product.PricePerKg
                    };
                    saleProducts.Add(saleProduct);
                    database.Add(saleProduct);
                }
                database.Save();
            }

            return saleProducts;
        }
    }
}
