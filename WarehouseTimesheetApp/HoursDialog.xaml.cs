using System.Globalization;
using System.Windows;

namespace WarehouseTimesheetApp;

public partial class HoursDialog : Window
{
    public bool IsWorked { get; private set; }
    public double WorkedHours { get; private set; }
    public decimal ExtraAmount { get; private set; }

    public HoursDialog(bool isWorked = true, double hours = 8, decimal extraAmount = 0)
    {
        InitializeComponent();
        WorkedCheckBox.IsChecked = isWorked;
        HoursTextBox.Text = hours.ToString(CultureInfo.InvariantCulture);
        ExtraTextBox.Text = extraAmount.ToString(CultureInfo.InvariantCulture);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(HoursTextBox.Text, CultureInfo.InvariantCulture, out var hours) || hours < 0)
        {
            MessageBox.Show("Введите корректное количество часов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(ExtraTextBox.Text, CultureInfo.InvariantCulture, out var extraAmount))
        {
            MessageBox.Show("Введите корректную сумму доплаты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsWorked = WorkedCheckBox.IsChecked == true;
        WorkedHours = hours;
        ExtraAmount = extraAmount;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
