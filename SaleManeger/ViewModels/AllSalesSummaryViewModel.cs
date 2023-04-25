using DynamicData;
using OxyPlot;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class AllSalesSummaryViewModel : ViewModelBase
    {
        public int NumberOfSales { get; set; }
        public LineSeries NumberOfCliensSeries { get; set; }
        public LineSeries NumberOfSoldProductsSeries { get; set; }
        public LineSeries SalesProfitSeries { get; set; }
        public bool Profit { get; set; } = true;
        public bool NumberOfClients { get; set; } = false;
        public bool Weight { get; set; } = false;
        public PlotModel Plot { get; set; }

        public ReactiveCommand<Unit, Unit> UpdateGraphCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenProjectSelectionCommand { get; }

        private DataBase _dataBase;

        public AllSalesSummaryViewModel(DataBase dataBase)
        {
            this._dataBase = dataBase;
            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            UpdateGraphCommand = ReactiveCommand.Create(UpdateGraph);

            var sales = _dataBase.GetSalesList();
            var products = _dataBase.GetProducts();
            NumberOfSales = sales.Count;

            var clientsWhoBought = new List<int>();
            var salesProfit = new List<double>();
            var numberOfSoldProducts = new List<double>();

            foreach (var sale in sales)
            {
                var saleProfit = 0.0;
                var saleSumOfProducts = 0.0;
                foreach (var product in products)
                {
                    saleProfit +=_dataBase.GetSumOfProduct(sale.SaleDate, product.Code, false);
                    saleSumOfProducts += _dataBase.GetSumOfProduct(sale.SaleDate, product.Code, false)/product.PricePerKg;

                }
                salesProfit.Add(saleProfit);
                numberOfSoldProducts.Add(saleSumOfProducts);
                var clients = _dataBase.GetClientsFromSale(sale.SaleDate).Where(x => x.Products.Any(y =>y.IsReserved ==false));
                clientsWhoBought.Add(clients.Count());
            }

            var saleProfitPoints = new List<DataPoint>();
            var numberOfClientsPoints = new List<DataPoint>();
            var numberOfSoldProductsPoints = new List<DataPoint>();

            for (int i = 0; i < sales.Count; i++)
            {
                if (clientsWhoBought[i] == 0)
                    continue;
                saleProfitPoints.Add(new DataPoint(i, salesProfit[i]));
                numberOfClientsPoints.Add(new DataPoint(i, clientsWhoBought[i]));
                numberOfSoldProductsPoints.Add(new DataPoint(i, numberOfSoldProducts[i]));
            }
            NumberOfSales = saleProfitPoints.Count;

            Plot = new PlotModel();

            // Create a series to represent the data
            SalesProfitSeries = new LineSeries 
            { 
                ItemsSource = saleProfitPoints,
                Color = OxyColors.Green,
                Title = "Zysk ze sprzedaży"
            };
            NumberOfCliensSeries = new LineSeries 
            { 
                ItemsSource = numberOfClientsPoints,
                Color = OxyColors.Blue,
                Title = "Ilość klientów"
            };
            NumberOfSoldProductsSeries = new LineSeries 
            { 
                ItemsSource = numberOfSoldProductsPoints,
                Color = OxyColors.Red,
                Title = "Ilość sprzedanych produktów"
            };

            // Add the series to the chart model
            Plot.Series.Add(SalesProfitSeries);
            Plot.Series.Add(NumberOfCliensSeries);
            Plot.Series.Add(NumberOfSoldProductsSeries);
            Plot.Series[1].IsVisible = false;
            Plot.Series[2].IsVisible = false;
            Plot.Legends.Add(new Legend()
            {
                LegendTitle = "Legenda",
                LegendPosition = LegendPosition.RightTop,
                Selectable = false,
                ShowInvisibleSeries = false,
            });
        }


        private void UpdateGraph()
        {
            if(Profit)
            {
                Plot.Series[0].IsVisible = true;
            }
            else
            {
                Plot.Series[0].IsVisible = false;
            }
            if(NumberOfClients)
            {
                Plot.Series[1].IsVisible = true;
            }
            else
            {
                Plot.Series[1].IsVisible = false;
            }
            if(Weight)
            {
                Plot.Series[2].IsVisible = true;
            }
            else
            {
                Plot.Series[2].IsVisible = false;
            }

            Plot.InvalidatePlot(true);
        }
    }
}
