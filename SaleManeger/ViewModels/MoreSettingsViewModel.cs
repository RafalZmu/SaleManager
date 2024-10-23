using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaleManeger.ViewModels
{
    public class MoreSettingsViewModel : ViewModelBase
    {
        public string saleID { get; set; }
        public MoreSettingsViewModel(string saleID)
        {
            this.saleID = saleID;
        }

    }
}
