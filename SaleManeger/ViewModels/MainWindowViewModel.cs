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

            clientEditionViewModel.OpenClientSelectionCommand.Subscribe(x => 
            {
                if (!string.IsNullOrWhiteSpace(x))
                {
                    OpenClientSelection(x);
                }
            });
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
            projectViewModel.OpenStatisticsCommand.Subscribe(model =>
            {
                OpenStatistics();
            });
            projectViewModel.OpenGlobalSmsBroadcastCommand.Subscribe(model =>
            {
                OpenGlobalSmsBroadcast();
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
            moreSettingViewModel.OpenCurrentProductStateCommand.Subscribe(OpenCurrentProductState);
            moreSettingViewModel.OpenClientSelectionCommand.Subscribe(OpenClientSelection);
            moreSettingViewModel.OpenSMSImportCommand.Subscribe(OpenSMSImport);
            moreSettingViewModel.OpenSalePlanningCommand.Subscribe(OpenSalePlanning);
            moreSettingViewModel.OpenSmsTestCommand.Subscribe(OpenSmsTestView);
            moreSettingViewModel.OpenSmsBroadcastCommand.Subscribe(OpenSmsBroadcastView);
        }

        private void OpenSMSImport(string saleId)
        {
            var SMSImportViewModel = new SMSImportViewModel(_dataBase, saleId);
            Content = SMSImportViewModel;
            SMSImportViewModel.OpenMoreSettingsCommand.Subscribe(OpenMoreSettingView);
        }

        public void OpenCurrentProductState(string saleId)
        {
            var currentProductStateViewModel = new CurrentProductStateViewModel(_dataBase, saleId);
            Content = currentProductStateViewModel;
            currentProductStateViewModel.OpenMoreSettingsCommand.Subscribe(OpenMoreSettingView);
        }

        private void OpenSalePlanning(string saleId)
        {
            var salePlanningViewModel = new SalePlanningViewModel(_dataBase, saleId);
            Content = salePlanningViewModel;
            salePlanningViewModel.GoBackCommand.Subscribe(OpenMoreSettingView);
        }

        private void OpenSmsTestView(string saleId)
        {
            var smsTestViewModel = new SmsTestViewModel(saleId);
            Content = smsTestViewModel;
            smsTestViewModel.GoBackCommand.Subscribe(OpenMoreSettingView);
        }

        private void OpenSmsBroadcastView(string saleId)
        {
            var smsBroadcastViewModel = new SmsBroadcastViewModel(saleId, _dataBase);
            Content = smsBroadcastViewModel;
            smsBroadcastViewModel.GoBackCommand.Subscribe(OpenMoreSettingView);
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

        private void OpenStatistics()
        {
            var statisticsViewModel = new StatisticsViewModel(_dataBase);
            Content = statisticsViewModel;
            statisticsViewModel.ReturnCommand.Subscribe(model =>
            {
                OpenProjectSelection();
            });
        }

        private void OpenGlobalSmsBroadcast()
        {
            var globalSmsBroadcastViewModel = new GlobalSmsBroadcastViewModel(_dataBase);
            Content = globalSmsBroadcastViewModel;
            globalSmsBroadcastViewModel.GoBackCommand.Subscribe(model =>
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