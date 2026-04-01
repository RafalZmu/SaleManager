using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using SaleManeger.Models;
using SaleManeger.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaleManeger.Views;

public partial class ClientEditionView : UserControl
{
    private List<Product> _products = null;
    private OrderColorizer _colorizer;

    public ClientEditionView()
    {
        InitializeComponent();
        sale.AddHandler(KeyUpEvent, Sale_InputHandler, RoutingStrategies.Tunnel);
        order.AddHandler(KeyUpEvent, Order_InputHandler, RoutingStrategies.Tunnel);
        
        // Prevent AvaloniaEdit 0.10.x crash on Ctrl+Backspace on empty lines
        sale.AddHandler(KeyDownEvent, Editor_KeyDown, RoutingStrategies.Tunnel);
        order.AddHandler(KeyDownEvent, Editor_KeyDown, RoutingStrategies.Tunnel);
    }

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var editor = sender as AvaloniaEdit.TextEditor;
            if (editor == null) return;
            
            if (editor.CaretOffset == 0)
            {
                e.Handled = true;
                return;
            }

            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            if (line.Length == 0 || string.IsNullOrWhiteSpace(editor.Document.GetText(line.Offset, editor.CaretOffset - line.Offset)))
            {
                e.Handled = true;
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ClientEditionViewModel vm)
        {
            order.Text = vm.Order ?? "";
            sale.Text = vm.Sale ?? "";

            orderWatermark.IsVisible = string.IsNullOrEmpty(order.Text);
            saleWatermark.IsVisible = string.IsNullOrEmpty(sale.Text);

            if (_colorizer == null)
            {
                _colorizer = new OrderColorizer();
                order.TextArea.TextView.LineTransformers.Add(_colorizer);
            }

            order.TextChanged -= Order_TextChanged;
            order.TextChanged += Order_TextChanged;

            sale.TextChanged -= Sale_TextChanged;
            sale.TextChanged += Sale_TextChanged;

            UpdateColors();
        }
    }

    private void Order_TextChanged(object sender, EventArgs e)
    {
        if (DataContext is ClientEditionViewModel vm)
        {
            vm.Order = order.Text;
            orderWatermark.IsVisible = string.IsNullOrEmpty(order.Text);
            UpdateColors();
        }
    }

    private void Sale_TextChanged(object sender, EventArgs e)
    {
        if (DataContext is ClientEditionViewModel vm)
        {
            vm.Sale = sale.Text;
            saleWatermark.IsVisible = string.IsNullOrEmpty(sale.Text);
            UpdateColors();
        }
    }

    private void UpdateColors()
    {
        if (DataContext is ClientEditionViewModel vm && vm.ProductsList != null)
        {
            var matched = OrderSaleMatcher.GetMatchedOrderLines(order.Text, sale.Text, vm.ProductsList);
            _colorizer.GreenLines = matched;
            order.TextArea.TextView.Redraw();
        }
    }

    private void Order_InputHandler(object sender, KeyEventArgs e)
    {
        if (_products == null)
        {
            _products = new List<Product>();
            var codesList = codes.Text.Trim().Split('\n');
            foreach (var code in codesList)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                _products.Add(new Product()
                {
                    Code = code.Split("-")[0].Trim(),
                    Name = code.Split("-")[1].Trim().Split(":")[0],
                });
            }
        }

        if (order.Text == null) return;
        string originalText = order.Text;
        var text = originalText.Replace(",", ".");
        if (string.IsNullOrWhiteSpace(text))
            return;
            
        string newText = "";
        var cursorPostion = 0;
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var cleanLine = line.Trim('\r');
            if (string.IsNullOrEmpty(cleanLine)) continue;
            if (cleanLine.Length == 2 && _products.Any(x => x.Code == cleanLine))
            {
                var replacement = _products.Where(x => x.Code == cleanLine).First().Name;
                newText += $"{replacement}: ";
                cursorPostion = newText.Length;
            }
            else
            {
                newText += $"{cleanLine}\n";
            }
        }
        
        string finalNewText = newText.TrimEnd('\r', '\n') + "\n";
        if (originalText != finalNewText)
        {
            int oldCaret = order.CaretOffset;
            order.Text = finalNewText;
            if (cursorPostion != 0)
            {
                order.CaretOffset = cursorPostion;
            }
            else
            {
                order.CaretOffset = Math.Min(oldCaret, order.Document.TextLength);
            }
        }
    }

    private void Sale_InputHandler(object sender, EventArgs e)
    {
        if (_products == null)
        {
            _products = new List<Product>();
            var codesList = codes.Text.Trim().Split('\n');
            foreach (var code in codesList)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                _products.Add(new Product()
                {
                    Code = code.Split("-")[0].Trim(),
                    Name = code.Split("-")[1].Trim().Split(":")[0],
                });
            }
        }
        
        if (sale.Text == null) return;
        string originalText = sale.Text;
        var text = originalText.Replace(",", ".");
        if (string.IsNullOrWhiteSpace(text))
            return;
            
        string newText = "";
        var cursorPostion = 0;
        var codeConverted = false;
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var cleanLine = line.Trim('\r');
            if (string.IsNullOrEmpty(cleanLine)) continue;
            if (cleanLine.Length == 2 && _products.Any(x => x.Code == cleanLine))
            {
                var replacement = _products.Where(x => x.Code == cleanLine).First().Name;
                newText += $"{replacement}: ";
                cursorPostion = newText.Length;
                codeConverted = true;
            }
            else
            {
                newText += $"{cleanLine}\n";
            }
        }
        
        string finalNewText = newText.TrimEnd('\r', '\n') + "\n";
        if (originalText != finalNewText)
        {
            int oldCaret = sale.CaretOffset;
            sale.Text = finalNewText;
            if (codeConverted == true && cursorPostion != 0)
            {
                sale.CaretOffset = cursorPostion;
            }
            else
            {
                sale.CaretOffset = Math.Min(oldCaret, sale.Document.TextLength);
            }
        }
    }
}

public class OrderColorizer : DocumentColorizingTransformer
{
    public HashSet<int> GreenLines { get; set; } = new HashSet<int>();

    protected override void ColorizeLine(AvaloniaEdit.Document.DocumentLine line)
    {
        if (GreenLines.Contains(line.LineNumber))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.ForegroundBrush = Brushes.Green;
            });
        }
    }
}

public class OrderSaleMatcher
{
    public static HashSet<int> GetMatchedOrderLines(string orderText, string saleText, List<Product> products)
    {
        var matchedOrderLines = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(orderText) || string.IsNullOrWhiteSpace(saleText))
            return matchedOrderLines;

        var orderItems = ParseLines(orderText, products);
        var saleItems = ParseLines(saleText, products);

        foreach (var saleItem in saleItems)
        {
            var candidates = orderItems
                .Where(o => !matchedOrderLines.Contains(o.LineIndex))
                .Where(o => o.Comment == saleItem.Comment && o.ProductID == saleItem.ProductID)
                .ToList();

            if (candidates.Any())
            {
                var bestMatch = candidates.OrderBy(o => Math.Abs(saleItem.Value - (o.Value * o.PricePerKg))).First();
                matchedOrderLines.Add(bestMatch.LineIndex);
            }
        }

        return matchedOrderLines;
    }

    private static List<ParsedItem> ParseLines(string text, List<Product> products)
    {
        var result = new List<ParsedItem>();
        var lines = text.Replace("\r", "").Split('\n');
        string currentComment = "";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim(' ');
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (!line.Contains(':'))
            {
                currentComment = line;
            }
            else
            {
                var parts = line.Split(':');
                var name = parts[0].Trim();
                var valStr = parts[1].Trim().Split(' ')[0];
                double.TryParse(valStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val);

                var prod = products.FirstOrDefault(p => p.Name == name);
                if (prod != null)
                {
                    result.Add(new ParsedItem
                    {
                        LineIndex = i + 1,
                        Comment = currentComment,
                        ProductID = prod.ID,
                        Value = val,
                        PricePerKg = prod.PricePerKg
                    });
                }
            }
        }
        return result;
    }

    class ParsedItem
    {
        public int LineIndex { get; set; }
        public string Comment { get; set; }
        public string ProductID { get; set; }
        public double Value { get; set; }
        public double PricePerKg { get; set; }
    }
}