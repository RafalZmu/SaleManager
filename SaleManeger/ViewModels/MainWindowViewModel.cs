using ReactiveUI;
using SaleManeger.Models;
using SQLitePCL;
using System;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private DataBase _dataBase { get; set; }
        private ViewModelBase content;
        public ViewModelBase Content
        {
            get => content;
            private set => this.RaiseAndSetIfChanged(ref content, value);
        }

        private string _saleName;

        public MainWindowViewModel()
        {
            _dataBase = new DataBase();


            OpenProjectSelection();
            Batteries.Init();

        }

        public void OpenEditProductsView()
        {
            var productView = new ProductEditionViewModel(_dataBase);
            Content = productView;
            productView.SaveToDataBaseCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
        }

        public void OpenProjectSelection()
        {
            var projectViewModel = new ProjectSelectionViewModel(_dataBase);
            Content = projectViewModel;

            projectViewModel.CreateNewSaleCommand.Subscribe(model =>
            {
                OpenClientSelection(model);
            });
            projectViewModel.OpenSaleCommand.Subscribe(model =>
            {
                OpenClientSelection(model);
            });
        }
        public void OpenClientSelection(string saleName)
        {
            _saleName = saleName;
            var clientSelectionViewModel = new ClientSelectionViewModel(saleName, _dataBase);
            Content = clientSelectionViewModel;
            clientSelectionViewModel.OpenClientEditionCommand.Subscribe(model =>
            {
                OpenClientEdition(model);
            });
            clientSelectionViewModel.DeleteClientCommand.Subscribe(model =>
            {
                OpenClientDeletion(model);
            });
            clientSelectionViewModel.OpenProjectSelectionCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
            clientSelectionViewModel.OpenSaleSummaryCommand.Subscribe(model =>
            {
                OpenSaleSummary(model);
            });
        }

        public void OpenClientDeletion(string model)
        {
            var clientDeletionViewModel = new PopUpViewModel(_dataBase, model, _saleName);
            Content = clientDeletionViewModel;
            clientDeletionViewModel.OpenClientSelectionCommand.Subscribe(model =>
            {
                OpenClientSelection(_saleName);
            });
            clientDeletionViewModel.ReturnCommand.Subscribe(model =>
            {
                OpenClientSelection(_saleName);
            });

        }

        public void OpenClientEdition(Client client)
        {
            var clientEditionViewModel = new ClientEditionViewModel(_dataBase, client, _saleName);
            Content = clientEditionViewModel;

            clientEditionViewModel.OpenClientSelectionCommand.Subscribe(OpenClientSelection);

        }
        public void OpenSaleSummary(string saleName)
        {
            var saleSummaryViewModel = new SaleSummaryViewModel(_dataBase, saleName);
            Content = saleSummaryViewModel;
            saleSummaryViewModel.OpenClientSelectionCommand.Subscribe(model =>
            {
                OpenClientSelection(saleName);
            });
        }
    }
}