using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Diagnostics;

namespace SaleManeger.Views;

public partial class SMSImportView : UserControl
{
    private TextBlock _dragState;
    public SMSImportView()
    {
        InitializeComponent();

        _dragState = this.Find<TextBlock>("DropState");

        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void Drop(object sender, DragEventArgs e)
    {
        Debug.WriteLine("Drop");

        _dragState.Text = string.Join(Environment.NewLine, e.Data.GetFileNames()); 
    }
}