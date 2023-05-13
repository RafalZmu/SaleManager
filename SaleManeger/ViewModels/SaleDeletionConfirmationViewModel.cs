using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    internal class SaleDeletionConfirmationViewModel : ViewModelBase
    {
        #region Public Properties

        public IProjectRepository _dataBase { get; set; }
        public ReactiveCommand<Unit, Unit> DeleteSaleCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }

        #endregion Public Properties

        #region Private Properties

        private Sale Sale { get; set; }

        #endregion Private Properties

        #region Public Constructors

        public SaleDeletionConfirmationViewModel(IProjectRepository dataBase, string saleID)
        {
            _dataBase = dataBase;
            Sale = _dataBase.GetAll<Sale>().Where(x => x.SaleID == saleID).First();

            DeleteSaleCommand = ReactiveCommand.Create(DeleteSale);
            ReturnCommand = ReactiveCommand.Create(() => { });
        }

        #endregion Public Constructors

        #region Private Methods

        private void DeleteSale()
        {
            _dataBase.Delete(Sale);
        }

        #endregion Private Methods
    }
}