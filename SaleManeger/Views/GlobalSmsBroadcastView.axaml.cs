using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SaleManeger.Views
{
    public partial class GlobalSmsBroadcastView : UserControl
    {
        public GlobalSmsBroadcastView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
