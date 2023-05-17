using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    internal class ClientDeletionConfirmationViewModel : ViewModelBase
    {
        #region Public Properties

        public IProjectRepository _dataBase { get; set; }
        public ReactiveCommand<Unit, Unit> OpenClientSelectionCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }

        #endregion Public Properties

        #region Private Properties

        private string ClientID { get; set; }
        private string SaleID { get; set; }

        #endregion Private Properties

        #region Public Constructors

        public ClientDeletionConfirmationViewModel(IProjectRepository dataBase, string saleID, string clientID)
        {
            _dataBase = dataBase;
            SaleID = saleID;
            ClientID = clientID;

            OpenClientSelectionCommand = ReactiveCommand.Create(DeleteClient);
            ReturnCommand = ReactiveCommand.Create(() => { });
        }

        #endregion Public Constructors

        #region Private Methods

        private void DeleteClient()
        {
            Client client = _dataBase.GetAll<Client>().Where(x => x.ID == ClientID).FirstOrDefault();
            _dataBase.Delete(client);
            _dataBase.GetAll<ClientOrder>().Where(x => x.ClientID == ClientID && x.SaleID == SaleID).ToList().ForEach(x => _dataBase.Delete(x));
            _dataBase.Save();
        }

        #endregion Private Methods
    }
}