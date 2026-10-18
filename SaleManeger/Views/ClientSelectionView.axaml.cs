using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaleManeger.ViewModels;

namespace SaleManeger.Views;

public partial class ClientSelectionView : UserControl
{
    #region Public Constructors

    public ClientSelectionView()
    {
        InitializeComponent();
        clientName.AddHandler(KeyUpEvent, UpdateClients_InputHandler, RoutingStrategies.Tunnel);

        // Listen on the whole window: when nothing on this screen has the keyboard focus the keys never pass through this control.
        AttachedToVisualTree += (_, e) => (e.Root as InputElement)?.AddHandler(KeyDownEvent, Shortcut_KeyDown, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, e) => (e.Root as InputElement)?.RemoveHandler(KeyDownEvent, Shortcut_KeyDown);
    }

    #endregion Public Constructors

    #region Private Methods

    // Ctrl+N new client, Ctrl+F search, Enter in the search opens the first client, Esc clears the search and then goes back.
    private void Shortcut_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ClientSelectionViewModel vm) return;

        if (e.Key == Key.N && e.KeyModifiers == KeyModifiers.Control)
        {
            vm.AddNewClient();
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            clientName.Focus();
            clientName.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && clientName.IsKeyboardFocusWithin)
        {
            vm.OpenFirstClient();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (!string.IsNullOrEmpty(vm.ClientName))
                vm.ClientName = "";
            else
                vm.GoBack();
            e.Handled = true;
        }
    }

    private void UpdateClients_InputHandler(object sender, KeyEventArgs e)
    {
        clients = clients;
        // Without this function clients list isn't updating
    }

    #endregion Private Methods
}