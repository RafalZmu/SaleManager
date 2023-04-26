using ReactiveUI;
using SaleManeger.Models;
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
        private ObservableCollection<Sale> _salesList { get; set; }
        private DataBase _dataBase { get; set; }

        private string _newSaleDate;
        public string NewSaleDate
        {
            get => _newSaleDate;
            set => this.RaiseAndSetIfChanged(ref _newSaleDate, value);
        }
        public ProjectSelectionViewModel(DataBase db)
        {
            _dataBase = db;
            _salesList = _dataBase.GetSalesList();
            CreateNewSaleCommand = ReactiveCommand.Create(CreateNewSale, this.WhenAnyValue(x => x.NewSaleDate, text => !string.IsNullOrWhiteSpace(text)));
            OpenAllSalesSummaryCommand = ReactiveCommand.Create(() => { });
            OpenSaleCommand = ReactiveCommand.Create((string saleName) => { return saleName; });
            DeleteSaleCommand = ReactiveCommand.Create((string saleName) => { return saleName; });
        }

        private string CreateNewSale()
        {
            if (_salesList.Any(x => x.SaleDate == NewSaleDate))
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                    .GetMessageBoxStandardWindow("Warning", "Sprzedarz o takiej nazwie już istnieje");
                messageBoxStandardWindow.Show();
                return null;
            }
            Sale sale = new(NewSaleDate);
            _salesList.Add(sale);
            _dataBase.AddToTable("Sales", ("SaleName", NewSaleDate));
            return NewSaleDate;

        }
    }
}
