using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WarehouseTimesheetApp;

public partial class MainWindow : Window
{
    private readonly List<Employee> _employees = new();
    private DateOnly _selectedMonth;
    private double _globalShiftHours = 8;

    public MainWindow()
    {
        InitializeComponent();
        MonthDatePicker.SelectedDate = DateTime.Today;
        _selectedMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        RefreshCalendar();
        RecalculateSalary();
    }

    private Employee? SelectedEmployee => EmployeesListBox.SelectedItem as Employee;

    private void OnAddEmployee(object sender, RoutedEventArgs e)
    {
        if (!TryParseEmployeeForm(out var employee))
        {
            return;
        }

        _employees.Add(employee);
        RefreshEmployeeList(employee);
    }

    private void OnUpdateEmployee(object sender, RoutedEventArgs e)
    {
        if (SelectedEmployee is null)
        {
            return;
        }

        if (!TryParseEmployeeForm(out var parsed))
        {
            return;
        }

        SelectedEmployee.Name = parsed.Name;
        SelectedEmployee.DailyRate = parsed.DailyRate;
        SelectedEmployee.HourlyRate = parsed.HourlyRate;
        SelectedEmployee.UseHourlyRate = parsed.UseHourlyRate;

        RefreshEmployeeList(SelectedEmployee);
        RecalculateSalary();
    }

    private void OnDeleteEmployee(object sender, RoutedEventArgs e)
    {
        if (SelectedEmployee is null)
        {
            return;
        }

        _employees.Remove(SelectedEmployee);
        RefreshEmployeeList();
        RefreshCalendar();
        RecalculateSalary();
    }

    private bool TryParseEmployeeForm(out Employee employee)
    {
        employee = new Employee();
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Введите имя сотрудника.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!decimal.TryParse(DailyRateTextBox.Text, CultureInfo.InvariantCulture, out var dailyRate) || dailyRate < 0)
        {
            MessageBox.Show("Проверьте дневную ставку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!decimal.TryParse(HourlyRateTextBox.Text, CultureInfo.InvariantCulture, out var hourlyRate) || hourlyRate < 0)
        {
            MessageBox.Show("Проверьте почасовую ставку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        employee = new Employee
        {
            Name = NameTextBox.Text.Trim(),
            DailyRate = dailyRate,
            HourlyRate = hourlyRate,
            UseHourlyRate = UseHourlyRateCheckBox.IsChecked == true
        };

        return true;
    }

    private void RefreshEmployeeList(Employee? toSelect = null)
    {
        EmployeesListBox.ItemsSource = null;
        EmployeesListBox.ItemsSource = _employees;
        EmployeesListBox.SelectedItem = toSelect;
    }

    private void OnEmployeeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedEmployee is null)
        {
            return;
        }

        NameTextBox.Text = SelectedEmployee.Name;
        DailyRateTextBox.Text = SelectedEmployee.DailyRate.ToString(CultureInfo.InvariantCulture);
        HourlyRateTextBox.Text = SelectedEmployee.HourlyRate.ToString(CultureInfo.InvariantCulture);
        UseHourlyRateCheckBox.IsChecked = SelectedEmployee.UseHourlyRate;

        RefreshCalendar();
        RecalculateSalary();
    }

    private void OnMonthChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedDate = MonthDatePicker.SelectedDate ?? DateTime.Today;
        _selectedMonth = new DateOnly(selectedDate.Year, selectedDate.Month, 1);
        RefreshCalendar();
        RecalculateSalary();
    }

    private void OnGlobalHoursChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(GlobalHoursTextBox.Text, CultureInfo.InvariantCulture, out var globalHours) && globalHours > 0)
        {
            _globalShiftHours = globalHours;
            RecalculateSalary();
        }
    }

    private void RefreshCalendar()
    {
        CalendarGrid.Children.Clear();

        var year = _selectedMonth.Year;
        var month = _selectedMonth.Month;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var button = new Button
            {
                Tag = date,
                MinHeight = 88,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
            };

            button.Click += OnDayClicked;
            ApplyDayVisual(button, date);
            CalendarGrid.Children.Add(button);
        }
    }

    private void ApplyDayVisual(Button button, DateOnly date)
    {
        var mark = GetMark(date);
        var extraText = mark.ExtraAmount == 0 ? string.Empty : $"\n+{mark.ExtraAmount:0.##} ₽";
        var hoursText = mark.WorkedHours is null ? string.Empty : $"\n{mark.WorkedHours:0.##} ч";

        button.Content = $"{date.Day}\n{GetStateTitle(mark)}{hoursText}{extraText}";

        button.Background = mark.State switch
        {
            DayMarkState.Worked => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            DayMarkState.Absent => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            DayMarkState.CustomWorkedOrAbsent => new SolidColorBrush(Color.FromRgb(14, 165, 233)),
            DayMarkState.CustomHours => new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            _ => new SolidColorBrush(Color.FromRgb(30, 41, 59))
        };
    }

    private static string GetStateTitle(DayMark mark) => mark.State switch
    {
        DayMarkState.Worked => "Работал",
        DayMarkState.Absent => "Не работал",
        DayMarkState.CustomWorkedOrAbsent => mark.IsWorked ? "Кастом: работал" : "Кастом: не работал",
        DayMarkState.CustomHours => mark.IsWorked ? "Почасовой день" : "Почасовой (выходной)",
        _ => "Пусто"
    };

    private DayMark GetMark(DateOnly date)
    {
        var employee = SelectedEmployee;
        if (employee is null)
        {
            return new DayMark { Date = date };
        }

        if (!employee.Marks.TryGetValue(date, out var mark))
        {
            mark = new DayMark { Date = date };
            employee.Marks[date] = mark;
        }

        return mark;
    }

    private void OnDayClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not DateOnly date)
        {
            return;
        }

        var employee = SelectedEmployee;
        if (employee is null)
        {
            MessageBox.Show("Сначала выберите сотрудника.", "Табель", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mark = GetMark(date);
        switch (mark.State)
        {
            case DayMarkState.Empty:
                mark.State = DayMarkState.Worked;
                mark.IsWorked = true;
                mark.ExtraAmount = 0;
                mark.WorkedHours = null;
                break;
            case DayMarkState.Worked:
                mark.State = DayMarkState.Absent;
                mark.IsWorked = false;
                mark.ExtraAmount = 0;
                mark.WorkedHours = null;
                break;
            case DayMarkState.Absent:
                var customDialog = new CustomDayDialog(true, mark.ExtraAmount) { Owner = this };
                if (customDialog.ShowDialog() == true)
                {
                    mark.State = DayMarkState.CustomWorkedOrAbsent;
                    mark.IsWorked = customDialog.IsWorked;
                    mark.ExtraAmount = customDialog.ExtraAmount;
                    mark.WorkedHours = null;
                }
                break;
            case DayMarkState.CustomWorkedOrAbsent:
                var hoursDialog = new HoursDialog(true, _globalShiftHours, mark.ExtraAmount) { Owner = this };
                if (hoursDialog.ShowDialog() == true)
                {
                    mark.State = DayMarkState.CustomHours;
                    mark.IsWorked = hoursDialog.IsWorked;
                    mark.WorkedHours = hoursDialog.WorkedHours;
                    mark.ExtraAmount = hoursDialog.ExtraAmount;
                }
                break;
            case DayMarkState.CustomHours:
                mark.State = DayMarkState.Empty;
                mark.IsWorked = false;
                mark.ExtraAmount = 0;
                mark.WorkedHours = null;
                break;
        }

        ApplyDayVisual(button, date);
        RecalculateSalary();
    }

    private void RecalculateSalary()
    {
        var employee = SelectedEmployee;
        if (employee is null)
        {
            SalaryTextBlock.Text = "Выберите сотрудника для расчёта зарплаты.";
            return;
        }

        decimal total = 0;
        var daysWorked = 0;

        foreach (var mark in employee.Marks.Values.Where(v => v.Date.Year == _selectedMonth.Year && v.Date.Month == _selectedMonth.Month))
        {
            if (!mark.IsWorked)
            {
                total += mark.ExtraAmount;
                continue;
            }

            daysWorked++;
            if (employee.UseHourlyRate)
            {
                var hours = (decimal)(mark.WorkedHours ?? _globalShiftHours);
                total += employee.HourlyRate * hours;
            }
            else
            {
                total += employee.DailyRate;
            }

            total += mark.ExtraAmount;
        }

        SalaryTextBlock.Text = $"{employee.Name}: смен {daysWorked}, итог за месяц: {total:0.##} ₽";
    }
}
