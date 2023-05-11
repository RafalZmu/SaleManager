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

        #region Public Constructors

        public AllSalesSummaryViewModel(IProjectRepository dataBase)
        {
            _dataBase = dataBase;

            OpenProjectSelectionCommand = ReactiveCommand.Create(() => { });
            UpdateGraphCommand = ReactiveCommand.Create(UpdateGraph);

            var clientsWhoBoughtPerSale = new List<int>();
            var saleProfitPerSale = new List<double>();
            var weightOfSoldProductsPerSale = new List<double>();

            foreach (var sale in _dataBase.GetAll<Sale>().ToList())
            {
                var saleProfit = 0.0;
                var sumOfProductsWeight = 0.0;
                foreach (var product in _dataBase.GetAll<Product>().ToList())
                {
                    saleProfit += _dataBase.GetAll<ClientOrder>()
                        .Where(x => x.ProductID == product.ID && x.SaleID == sale.SaleID && x.IsReserved == false)
                        .ToList()
                        .Sum(x => long.Parse(x.Value.Split(' ')[0]));
                    sumOfProductsWeight += saleProfit / product.PricePerKg;
                }
                saleProfitPerSale.Add(saleProfit);
                weightOfSoldProductsPerSale.Add(sumOfProductsWeight);
                var clients = _dataBase.GetAll<ClientOrder>()
                    .Where(x => x.IsReserved == false)
                    .GroupBy(x => x.ClientID)
                    .ToList();
                clientsWhoBoughtPerSale.Add(clients.Count);
            }
            NumberOfSales = weightOfSoldProductsPerSale.Count;

            CreateGraph(clientsWhoBoughtPerSale, saleProfitPerSale, weightOfSoldProductsPerSale);
        }

        #endregion Public Constructors

        #region Private Methods

        private void CreateGraph(List<int> clientsWhoBought, List<double> salesProfit, List<double> numberOfSoldProducts)
        {
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