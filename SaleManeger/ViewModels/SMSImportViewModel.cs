using ReactiveUI;
using SaleManeger.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace SaleManeger.ViewModels
{
    public class SMSImportViewModel: ViewModelBase
    {
        private string _saleID;
        private IProjectRepository _database;
        public ReactiveCommand<Unit, string> OpenMoreSettingsCommand {  get; set; }
        public SMSImportViewModel(IProjectRepository database, string saleID)
        {
            _database = database;
            _saleID = saleID;
            OpenMoreSettingsCommand = ReactiveCommand.Create(() => { return _saleID; });
        }
    }
}
