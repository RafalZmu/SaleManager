using ReactiveUI;
using SaleManeger.Models;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private List<bool> _selected;
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
            projectViewModel.OpenAllSalesSummaryCommand.Subscribe(model =>
            {
                OpenAllSalesSummary();
            });
            projectViewModel.DeleteSaleCommand.Subscribe(model =>
            {
                OpenSaleDeletionConfirmation(model);
            });
        }

        private void OpenSaleDeletionConfirmation(string saleName)
        {
            var saleDeletionConfirmationViewModel = new SaleDeletionConfirmationViewModel(_dataBase, saleName);
            Content = saleDeletionConfirmationViewModel;

            saleDeletionConfirmationViewModel.DeleteSaleCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
            saleDeletionConfirmationViewModel.ReturnCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
        }

        private void OpenAllSalesSummary()
        {
            var allSalesSummaryViewModel = new AllSalesSummaryViewModel(_dataBase);
            Content = allSalesSummaryViewModel;
            allSalesSummaryViewModel.OpenProjectSelectionCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
        }

        public void OpenClientSelection(string saleName)
        {
            _saleName = saleName;
            var clientSelectionViewModel = new ClientSelectionViewModel(saleName, _dataBase, _selected);
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
            clientSelectionViewModel.UpdateClientsCommand.Subscribe(model =>
            {
                _selected = model;
            });
        }

        public void OpenClientDeletion(string model)
        {
            var clientDeletionViewModel = new PopUpViewModel(_dataBase, _saleName, model);
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