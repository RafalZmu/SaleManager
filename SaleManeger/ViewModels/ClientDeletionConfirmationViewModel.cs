using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using System.Linq;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class ClientDeletionConfirmationViewModel : ViewModelBase
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

            OpenClientSelectionCommand = ReactiveCommand.Create(() => DeleteClient(_dataBase, ClientID, SaleID));
            ReturnCommand = ReactiveCommand.Create(() => { });
        }

        #endregion Public Constructors

        #region Private Methods

        public static void DeleteClient(IProjectRepository dataBase, string clientID, string saleID)
        {
            Client client = dataBase.GetAll<Client>().Where(x => x.ID == clientID).FirstOrDefault();
            dataBase.Delete(client);
            dataBase.GetAll<ClientOrder>().Where(x => x.ClientID == clientID && x.SaleID == saleID).ToList().ForEach(x => dataBase.Delete(x));
            dataBase.Save();
        }

        #endregion Private Methods
    }
}