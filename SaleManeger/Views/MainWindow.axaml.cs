using Avalonia.ReactiveUI;
using SaleManeger.ViewModels;

namespace SaleManeger.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}