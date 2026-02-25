using System.Globalization;
using System.Windows;

namespace WarehouseTimesheetApp;

public partial class CustomDayDialog : Window
{
    public bool IsWorked { get; private set; }
    public decimal ExtraAmount { get; private set; }

    public CustomDayDialog(bool isWorked = true, decimal extraAmount = 0)
    {
        InitializeComponent();
        WorkedCheckBox.IsChecked = isWorked;
        ExtraTextBox.Text = extraAmount.ToString(CultureInfo.InvariantCulture);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ExtraTextBox.Text, CultureInfo.InvariantCulture, out var extraAmount))
        {
            MessageBox.Show("Введите корректную сумму.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsWorked = WorkedCheckBox.IsChecked == true;
        ExtraAmount = extraAmount;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
