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
        }

        #endregion Public Constructors
    }
}