using Avalonia.Controls;
using Avalonia.Interactivity;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaleManeger.Views;

public partial class ClientEditionView : UserControl
{
    private List<Product> _products;
    private DataBase _dataBase;
    public ClientEditionView()
    {
        InitializeComponent();
        sale.AddHandler(KeyUpEvent, Sale_InputHandler, RoutingStrategies.Tunnel);
        order.AddHandler(KeyUpEvent, Order_InputHandler, RoutingStrategies.Tunnel);
        _dataBase = new DataBase();
        _products = _dataBase.GetProducts();
    }
    private void Order_InputHandler(object? sender, EventArgs e)
    {
        var text = order.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;
        string newText = "";
        var cursorPostion = 0;
        foreach (var line in text.Split("\n"))
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (line.Length == 2 && _products.Any(x => x.Code == line))
            {
                var replacement = _products.Where(x => x.Code == line).First().Name;
                newText += $"{replacement}: ";
                cursorPostion += newText.Length;
            }
            else
            {
                newText += $"{line}\n";
            }

        }
        order.Text = newText;
        if (cursorPostion != 0)
        {
            order.CaretIndex = cursorPostion;
        }
    }
    private void Sale_InputHandler(object? sender, EventArgs e)
    {
        saleSum = saleSum;
        var text = sale.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;
        string newText = "";
        var cursorPostion = 0;
        var codeConverted = false;
        foreach (var line in text.Split("\n"))
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (line.Length == 2 && _products.Any(x => x.Code == line))
            {
                var replacement = _products.Where(x => x.Code == line).First().Name;
                newText += $"{replacement}: ";
                cursorPostion += newText.Length;
                codeConverted = true;
            }
            else
            {
                newText += $"{line}\n";
            }

        }
        sale.Text = newText;
        if (codeConverted == true)
        {
            sale.CaretIndex = cursorPostion;
        }
    }
}