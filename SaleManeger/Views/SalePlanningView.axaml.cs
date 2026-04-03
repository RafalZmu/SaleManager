using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SaleManeger.Views
{
    public partial class SalePlanningView : UserControl
    {
        public SalePlanningView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
