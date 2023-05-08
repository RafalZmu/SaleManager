using OxyPlot;
using OxyPlot.Legends;
using OxyPlot.Series;
using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class AllSalesSummaryViewModel : ViewModelBase
    {
        #region Private Fields

        private IProjectRepository _dataBase;

        #endregion Private Fields

        #region Public Constructors

        public AllSalesSummaryViewModel(IProjectRepository dataBase)
        {
            _dataBase = dataBase;

            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            UpdateGraphCommand = ReactiveCommand.Create(UpdateGraph);

            var clientsWhoBought = new List<int>();
            var salesProfit = new List<double>();
            var numberOfSoldProducts = new List<double>();

            foreach (var sale in _dataBase.GetAll<Sale>().ToList())
            {
                var saleProfit = 0.0;
                var saleSumOfProducts = 0.0;
                foreach (var product in _dataBase.GetAll<Product>().ToList())
                {
                    saleProfit += _dataBase.GetAll<ClientOrder>()
                        .Where(x => x.ProductID == product.ID && x.SaleID == sale.SaleID && x.IsReserved == false)
                        .ToList()
                        .Sum(x => long.Parse(x.Value));
                    saleSumOfProducts += saleProfit / product.PricePerKg;
                }
                salesProfit.Add(saleProfit);
                numberOfSoldProducts.Add(saleSumOfProducts);
                var clients = _dataBase.GetAll<ClientOrder>()
                    .Where(x => x.IsReserved == false)
                    .GroupBy(x => x.ClientID);
                clientsWhoBought.Add(clients.Count());
            }
            NumberOfSales = numberOfSoldProducts.Count;

            var saleProfitPoints = new List<DataPoint>();
            var numberOfClientsPoints = new List<DataPoint>();
            var numberOfSoldProductsPoints = new List<DataPoint>();

            for (int i = 0; i < NumberOfSales; i++)
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

        #endregion Public Constructors

        #region Public Properties

        public LineSeries NumberOfCliensSeries { get; set; }
        public bool NumberOfClients { get; set; } = false;
        public int NumberOfSales { get; set; }
        public LineSeries NumberOfSoldProductsSeries { get; set; }
        public ReactiveCommand<Unit, Unit> OpenProjectSelectionCommand { get; }
        public PlotModel Plot { get; set; }
        public bool Profit { get; set; } = true;
        public LineSeries SalesProfitSeries { get; set; }
        public ReactiveCommand<Unit, Unit> UpdateGraphCommand { get; }
        public bool Weight { get; set; } = false;

        #endregion Public Properties

        #region Private Methods

        private void UpdateGraph()
        {
            Plot.Series[0].IsVisible = Profit;
            Plot.Series[1].IsVisible = NumberOfClients;
            Plot.Series[2].IsVisible = Weight;

            Plot.InvalidatePlot(true);
        }

        #endregion Private Methods
    }
}