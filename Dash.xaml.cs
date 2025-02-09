using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace AlgebraSQLizer
{
    /// <summary>
    /// Converter that returns a Thickness whose left is fixed (150),
    /// top and bottom are 0, and right equals the ChatDrawer’s visible content width.
    /// The visible content width is computed as (ChatDrawer.Width - ChatDrawerTransform.X - SlideButtonWidth).
    /// </summary>
    public class BottomBarMarginConverter : IMultiValueConverter
    {
        private const double SlideButtonWidth = 30; // Fixed width of the SlideButton

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double drawerWidth &&
                values[1] is double transformX)
            {
                // Compute the visible width of the chat content (excluding the SlideButton)
                double visibleWidth = drawerWidth - transformX - SlideButtonWidth;
                if (visibleWidth < 0) visibleWidth = 0;
                return new Thickness(150, 0, visibleWidth, 0);
            }
            // Fallback: when closed, visible width is 0
            return new Thickness(150, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RelationalAlgebraVisitor : TSqlFragmentVisitor
    {
        private readonly StringBuilder _projection = new();
        private readonly StringBuilder _selection = new();
        private readonly StringBuilder _join = new();
        private readonly StringBuilder _tables = new();
        private readonly System.Collections.Generic.List<string> _orderBy = new();

        public override void Visit(QuerySpecification node)
        {
            if (node.SelectElements != null)
            {
                foreach (var element in node.SelectElements)
                {
                    if (element is SelectScalarExpression scalar)
                    {
                        string alias = scalar.ColumnName?.Value != null ? $" AS {scalar.ColumnName.Value}" : "";
                        _projection.Append($"{ExtractFragmentText(scalar.Expression)}{alias}, ");
                    }
                }
                if (_projection.Length > 0)
                    _projection.Length -= 2;
            }
            if (node.FromClause != null)
                _tables.Append(ExtractFragmentText(node.FromClause));
            if (node.WhereClause != null)
                _selection.Append(ExtractFragmentText(node.WhereClause.SearchCondition));
            if (node.OrderByClause != null)
            {
                foreach (var order in node.OrderByClause.OrderByElements)
                    _orderBy.Add(ExtractFragmentText(order.Expression));
            }
            base.Visit(node);
        }

        public override void Visit(QualifiedJoin node)
        {
            _join.Append($"({ExtractFragmentText(node.FirstTableReference)} JOIN {ExtractFragmentText(node.SecondTableReference)} " +
                         $"ON {ExtractFragmentText(node.SearchCondition)}), ");
            base.Visit(node);
        }

        public string GetRelationalAlgebra()
        {
            StringBuilder result = new();
            if (_projection.Length > 0)
                result.Append($"π[{_projection}] ");
            if (_selection.Length > 0)
                result.Append($"σ[{_selection}] ");
            if (_join.Length > 0)
                result.Append(_join.ToString().TrimEnd(',', ' '));
            else
                result.Append(_tables);
            if (_orderBy.Count > 0)
                result.Append($" τ[{string.Join(", ", _orderBy)}]");
            return result.ToString().Trim();
        }

        private static string ExtractFragmentText(TSqlFragment fragment)
        {
            if (fragment == null)
                return string.Empty;
            var scriptGenerator = new Sql150ScriptGenerator();
            StringWriter writer = new();
            scriptGenerator.GenerateScript(fragment, writer);
            return writer.ToString().Trim();
        }
    }

    public partial class Dash : Window
    {
        // Track whether the Chat Drawer is open
        private bool isDrawerOpen = false;

        public Dash()
        {
            InitializeComponent();
        }

        private void SlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDrawerOpen)
            {
                // Open the Chat Drawer (its animation updates ChatDrawerTransform.X)
                Storyboard slideIn = (Storyboard)FindResource("SlideIn");
                slideIn.Begin();
            }
            else
            {
                // Close the Chat Drawer
                Storyboard slideOut = (Storyboard)FindResource("SlideOut");
                slideOut.Begin();
            }
            isDrawerOpen = !isDrawerOpen;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text;
            if (!string.IsNullOrEmpty(message))
            {
                Border messageBorder = new Border
                {
                    BorderBrush = Brushes.Purple,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(5),
                    Padding = new Thickness(5),
                    Background = Brushes.DarkGray
                };

                TextBlock messageTextBlock = new TextBlock
                {
                    Text = "Sender: " + message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };

                messageBorder.Child = messageTextBlock;
                MessagesContainer.Children.Add(messageBorder);

                // Scroll the ScrollViewer (if available) to the bottom.
                foreach (var child in LogicalTreeHelper.GetChildren(ChatDrawer))
                {
                    if (child is ScrollViewer sv)
                    {
                        sv.ScrollToBottom();
                        break;
                    }
                }
                MessageTextBox.Clear();
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                int caretIndex = MessageTextBox.CaretIndex;
                MessageTextBox.Text = MessageTextBox.Text.Insert(caretIndex, Environment.NewLine);
                MessageTextBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string item = btn.Content.ToString();
                MainContent.Content = new TextBlock
                {
                    Text = "You selected " + item,
                    Foreground = Brushes.White,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        // Allow dragging the window by clicking on the outer border.
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }

        // Event handlers for Bottom Bar vertical animation
        private void BottomBar_MouseEnter(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarShow");
            sb.Begin();
        }

        private void BottomBar_MouseLeave(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarHide");
            sb.Begin();
        }
    }
}
