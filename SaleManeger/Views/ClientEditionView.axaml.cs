using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaleManeger.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaleManeger.Views;

public partial class ClientEditionView : UserControl
{
    private List<Product> _products = null;

    public ClientEditionView()
    {
        InitializeComponent();
        sale.AddHandler(KeyUpEvent, Sale_InputHandler, RoutingStrategies.Tunnel);
        order.AddHandler(KeyUpEvent, Order_InputHandler, RoutingStrategies.Tunnel);
    }

    private void Order_InputHandler(object sender, KeyEventArgs e)
    {
        if (_products == null)
        {
            _products = new List<Product>();
            var codesList = codes.Text.Trim().Split("\n");
            foreach (var code in codesList)
            {
                _products.Add(new Product()
                {
                    Code = code.Split("-")[0].Trim(),
                    Name = code.Split("-")[1].Trim(),
                });
            }
        }

        if (order.Text == null) return;
        order.Text = order.Text.Replace(",", ".");
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
        order.Text = newText.Trim('\r', '\n') + "\n";
        if (cursorPostion != 0)
        {
            order.CaretIndex = cursorPostion;
        }
    }

    private void Sale_InputHandler(object sender, EventArgs e)
    {
        if (_products == null)
        {
            _products = new List<Product>();
            var codesList = codes.Text.Trim().Split("\n");
            foreach (var code in codesList)
            {
                _products.Add(new Product()
                {
                    Code = code.Split("-")[0].Trim(),
                    Name = code.Split("-")[1].Trim(),
                });
            }
        }
        //Updates saleSum on input
        if (sale.Text == null) return;
        sale.Text = sale.Text.Replace(",", ".");
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
        sale.Text = newText.Trim('\r', '\n') + "\n";
        if (codeConverted == true)
        {
            sale.CaretIndex = cursorPostion;
        }
    }
}