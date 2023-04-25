using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class ProjectSelectionViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, string> CreateNewSaleCommand { get; }
        public ReactiveCommand<string, string> DeleteSaleCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenAllSalesSummaryCommand { get; }
        public ReactiveCommand<string, string> OpenSaleCommand { get; }
        private string _newSaleDate;
        public string NewSaleDate
        {
            get => _newSaleDate;
            set => this.RaiseAndSetIfChanged(ref _newSaleDate, value);
        }

        private ObservableCollection<Sale> SalesList { get; set; }
        private DataBase _dataBase { get; set; }
        public ProjectSelectionViewModel(DataBase db)
        {
            _dataBase = db;
            SalesList = _dataBase.GetSalesList();
            CreateNewSaleCommand = ReactiveCommand.Create(CreateNewSale, this.WhenAnyValue(x => x.NewSaleDate, text => !string.IsNullOrWhiteSpace(text)));
            OpenAllSalesSummaryCommand = ReactiveCommand.Create(() => { });
            OpenSaleCommand = ReactiveCommand.Create<string, string>(OpenSale);
            DeleteSaleCommand = ReactiveCommand.Create<string, string>(DeleteSale);
        }

        private string DeleteSale(string saleName)
        {
            return saleName;
        }

        private string OpenSale(string saleName)
        {
            return saleName;
        }

        private string CreateNewSale()
        {
            if (SalesList.Any(x => x.SaleDate == NewSaleDate))
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                    .GetMessageBoxStandardWindow("Warning", "Sprzedarz o takiej nazwie już istnieje");
                messageBoxStandardWindow.Show();
                return null;
            }
            Sale sale = new(NewSaleDate);
            SalesList.Add(sale);
            _dataBase.AddToTable("Sales", ("SaleName", NewSaleDate));
            return NewSaleDate;

        }
    }
}
