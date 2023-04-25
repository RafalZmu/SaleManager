using ReactiveUI;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace SaleManeger.ViewModels
{
    internal class SaleDeletionConfirmationViewModel:ViewModelBase
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
            ReturnCommand = ReactiveCommand.Create(Return);
        }

        private void Return()
        {
        }
        private void DeleteSale()
        {
            _dataBase.DeleteSale(SaleName);
        }
    }
}
