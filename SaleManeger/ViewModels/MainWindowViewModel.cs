using ReactiveUI;
using SaleManeger.Models;
using SaleManeger.Repositories;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace SaleManeger.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        #region Private Fields

        private string _saleName;
        private List<bool> _selected;
        private ViewModelBase content;

        #endregion Private Fields

        #region Public Properties

        public ViewModelBase Content
        {
            get => content;
            private set => this.RaiseAndSetIfChanged(ref content, value);
        }

        #endregion Public Properties

        #region Private Properties

        private IProjectRepository _dataBase { get; set; }

        #endregion Private Properties

        #region Public Constructors

        public MainWindowViewModel()
        {
            var saleContext = new SaleContext();
            _dataBase = new ProjectRepository(saleContext);

            OpenProjectSelection();
            Batteries.Init();
        }

        #endregion Public Constructors

        #region Public Methods

        public void OpenClientDeletion(string model)
        {
            var clientDeletionViewModel = new ClientDeletionConfirmationViewModel(_dataBase, _saleName, model);
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

        public void OpenClientSelection(string saleName)
        {
            _saleName = saleName;
            var clientSelectionViewModel = new ClientSelectionViewModel(saleName, _dataBase, _selected);
            Content = clientSelectionViewModel;

            clientSelectionViewModel.OpenClientEditionCommand.Subscribe(OpenClientEdition);
            clientSelectionViewModel.DeleteClientCommand.Subscribe(OpenClientDeletion);
            clientSelectionViewModel.OpenSaleSummaryCommand.Subscribe(OpenSaleSummary);
            clientSelectionViewModel.OpenMoreSettingsCommand.Subscribe(OpenMoreSettingView);
            clientSelectionViewModel.OpenProjectSelectionCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
            clientSelectionViewModel.UpdateClientsCommand.Subscribe(model =>
            {
                _selected = model;
            });
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
            var projectViewModel = new SaleSelectionViewModel(_dataBase);
            Content = projectViewModel;

            projectViewModel.CreateNewSaleCommand.Subscribe(OpenClientSelection);
            projectViewModel.OpenSaleCommand.Subscribe(OpenClientSelection);
            projectViewModel.DeleteSaleCommand.Subscribe(OpenSaleDeletionConfirmation);
            projectViewModel.OpenAllSalesSummaryCommand.Subscribe(model =>
            {
                OpenAllSalesSummary();
            });
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

        public void OpenMoreSettingView(string saleId)
        {
            var moreSettingViewModel = new MoreSettingsViewModel(saleId);
            Content = moreSettingViewModel;
            //moreSettingViewModel.OpenMoreOption.OpenMoreSettingsViewCommand(model =>
            //{
             //   OpenProjectSelection();
            //});
        }

        #endregion Public Methods

        #region Private Methods

        private void OpenAllSalesSummary()
        {
            var allSalesSummaryViewModel = new AllSalesSummaryViewModel(_dataBase);
            Content = allSalesSummaryViewModel;
            allSalesSummaryViewModel.OpenProjectSelectionCommand.Subscribe(model =>
            {
                OpenProjectSelection();
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

        #endregion Private Methods
    }
}