using ReactiveUI;
using SaleManeger.Models;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    class PopUpViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OpenClientSelectionCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnCommand { get; }
        public DataBase _dataBase { get; set; }
        private string ClientID { get; set; }
        private string SaleName { get; set; }

        public PopUpViewModel(DataBase dataBase, string saleName, string clientID)
        {
            _dataBase = dataBase;
            SaleName = saleName;
            ClientID = clientID;

            OpenClientSelectionCommand = ReactiveCommand.Create(DeleteClient);
            ReturnCommand = ReactiveCommand.Create(() => { });
        }

        private void DeleteClient()
        {
            _dataBase.DeleteClient(ClientID, SaleName);
        }

    }
}
