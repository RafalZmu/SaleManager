using ReactiveUI;
using SaleManeger.Models;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    internal class SaleDeletionConfirmationViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> DeleteSaleCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }
        public DataBase _dataBase { get; set; }
        private string SaleName { get; set; }

        public SaleDeletionConfirmationViewModel(DataBase dataBase, string saleName)
        {
            _dataBase = dataBase;
            SaleName = saleName;

            DeleteSaleCommand = ReactiveCommand.Create(DeleteSale);
            ReturnCommand = ReactiveCommand.Create(() => { });
        }

        private void DeleteSale()
        {
            _dataBase.DeleteSale(SaleName);
        }
    }
}
