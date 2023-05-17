using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class SaleSelectionViewModel : ViewModelBase
    {
        #region Private Fields

        private string _newSaleDate;

        #endregion Private Fields

        #region Public Properties

        public ReactiveCommand<Unit, string> CreateNewSaleCommand { get; }
        public ReactiveCommand<string, string> DeleteSaleCommand { get; }

        public string NewSaleDate
        {
            get => _newSaleDate;
            set => this.RaiseAndSetIfChanged(ref _newSaleDate, value);
        }

        public ReactiveCommand<Unit, Unit> OpenAllSalesSummaryCommand { get; }
        public ReactiveCommand<string, string> OpenSaleCommand { get; }

        #endregion Public Properties

        #region Private Properties

        private IProjectRepository _dataBase { get; set; }
        private ObservableCollection<Sale> _salesList { get; set; }

        #endregion Private Properties

        #region Public Constructors

        public SaleSelectionViewModel(IProjectRepository db)
        {
            _dataBase = db;
            _salesList = new ObservableCollection<Sale>(_dataBase.GetAll<Sale>().ToList());
            CreateNewSaleCommand = ReactiveCommand.Create(CreateNewSale, this.WhenAnyValue(x => x.NewSaleDate, text => !string.IsNullOrWhiteSpace(text)));
            OpenAllSalesSummaryCommand = ReactiveCommand.Create(() => { });
            OpenSaleCommand = ReactiveCommand.Create((string saleID) => { return saleID; });
            DeleteSaleCommand = ReactiveCommand.Create((string saleID) => { return saleID; });
        }

        #endregion Public Constructors

        #region Private Methods

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
            var ID = Guid.NewGuid().ToString();
            _dataBase.Add(new Sale()
            {
                SaleID = ID,
                SaleDate = NewSaleDate
            });
            _dataBase.Save();
            return ID;
        }

        #endregion Private Methods
    }
}