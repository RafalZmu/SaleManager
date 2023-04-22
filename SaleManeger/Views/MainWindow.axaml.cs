using Avalonia.Controls;
using SaleManeger.ViewModels;
using Avalonia.ReactiveUI;

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