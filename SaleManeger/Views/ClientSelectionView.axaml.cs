using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;

namespace SaleManeger.Views;

public partial class ClientSelectionView : UserControl
{
    public ClientSelectionView()
    {
        InitializeComponent();
        clientName.AddHandler(KeyUpEvent, UpdateClients_InputHandler, RoutingStrategies.Tunnel);
    }
    private void UpdateClients_InputHandler(object sender, KeyEventArgs e)
    {
        clients = clients;
        // Without this function clients list isn't updating 
    }
}