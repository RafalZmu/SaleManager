using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SaleManeger.Views
{
    public partial class SmsBroadcastView : UserControl
    {
        public SmsBroadcastView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
