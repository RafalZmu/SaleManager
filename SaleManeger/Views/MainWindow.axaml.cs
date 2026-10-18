using Avalonia.ReactiveUI;
using SaleManeger.ViewModels;

namespace SaleManeger.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        #region Public Constructors

        public MainWindow()
        {
            InitializeComponent();
            Closing += (_, e) =>
            {
                if (DataContext is MainWindowViewModel vm && !vm.CanCloseWindow())
                    e.Cancel = true;
            };
        }

        #endregion Public Constructors
    }
}