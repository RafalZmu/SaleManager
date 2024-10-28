using ReactiveUI;
using System.Reactive;

namespace SaleManeger.ViewModels
{
    public class MoreSettingsViewModel : ViewModelBase
    {
        public string saleID { get; set; }

        public ReactiveCommand<Unit, string> OpenCurrentProductStateCommand { get; set; }
        public ReactiveCommand<Unit, string> OpenClientSelectionCommand{ get; set; }
        public MoreSettingsViewModel(string saleID)
        {
            this.saleID = saleID;


            OpenCurrentProductStateCommand = ReactiveCommand.Create(() => {return saleID;});
            OpenClientSelectionCommand = ReactiveCommand.Create(() => { return saleID;});

        }

    }
}
