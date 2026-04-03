using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SaleManeger.Views
{
    public partial class SmsTestView : UserControl
    {
        public SmsTestView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
