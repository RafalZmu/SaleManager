using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SaleManeger.Views;

public partial class ClientSelectionView : UserControl
{
    #region Public Constructors

    public ClientSelectionView()
    {
        InitializeComponent();
        clientName.AddHandler(KeyUpEvent, UpdateClients_InputHandler, RoutingStrategies.Tunnel);
    }

    #endregion Public Constructors

    #region Private Methods

    private void UpdateClients_InputHandler(object sender, KeyEventArgs e)
    {
        clients = clients;
        // Without this function clients list isn't updating
    }

    #endregion Private Methods
}