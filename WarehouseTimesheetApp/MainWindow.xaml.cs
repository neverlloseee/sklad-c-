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
    private static readonly DayOfWeek CalendarFirstDayOfWeek = DayOfWeek.Monday;

    public MainWindow()
    {
        InitializeComponent();
        MonthDatePicker.SelectedDate = DateTime.Today;
        _selectedMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        RenderWeekHeaders();
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

        var firstDay = new DateOnly(year, month, 1);
        var leadingDays = ((int)firstDay.DayOfWeek - (int)CalendarFirstDayOfWeek + 7) % 7;

        var prevMonthDate = firstDay.AddMonths(-1);
        var prevMonthDays = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);

        var totalCells = leadingDays + daysInMonth;
        var rowCount = (int)Math.Ceiling(totalCells / 7d);
        CalendarGrid.Rows = Math.Max(4, rowCount);

        var trailingDays = CalendarGrid.Rows * 7 - totalCells;

        for (var i = 0; i < leadingDays; i++)
        {
            var dayNumber = prevMonthDays - leadingDays + i + 1;
            CalendarGrid.Children.Add(CreateAdjacentMonthCell(dayNumber));
        }

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var button = new Button
            {
                Tag = date,
                MinHeight = 104,
                Margin = new Thickness(4),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                Style = (Style)FindResource("CalendarDayButtonStyle")
            };

            button.Click += OnDayClicked;
            ApplyDayVisual(button, date);
            CalendarGrid.Children.Add(button);
        }

        for (var day = 1; day <= trailingDays; day++)
        {
            CalendarGrid.Children.Add(CreateAdjacentMonthCell(day));
        }
    }

    private static Border CreateAdjacentMonthCell(int dayNumber)
    {
        return new Border
        {
            Margin = new Thickness(4),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(12, 20, 38)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderThickness = new Thickness(1),
            Opacity = 0.45,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = dayNumber.ToString(CultureInfo.InvariantCulture),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            }
        };
    }

    private void RenderWeekHeaders()
    {
        WeekHeaderGrid.Children.Clear();
        var culture = new CultureInfo("ru-RU");

        for (var i = 0; i < 7; i++)
        {
            var dayOfWeek = (DayOfWeek)(((int)CalendarFirstDayOfWeek + i) % 7);
            var title = culture.DateTimeFormat.GetAbbreviatedDayName(dayOfWeek);

            WeekHeaderGrid.Children.Add(new Border
            {
                Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(6),
                Background = new SolidColorBrush(Color.FromRgb(11, 18, 32)),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = title,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
                }
            });
        }
    }

    private void ApplyDayVisual(Button button, DateOnly date)
    {
        var mark = GetMark(date);
        var extraText = mark.ExtraAmount == 0 ? string.Empty : $"\n+{mark.ExtraAmount:0.##} ₽";
        var hoursText = mark.WorkedHours is null ? string.Empty : $"\n{mark.WorkedHours:0.##} ч";

        button.Content = $"{date.Day}\n{GetStateTitle(mark)}{hoursText}{extraText}";
        button.Foreground = Brushes.White;
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        button.BorderThickness = new Thickness(1);

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
        try
        {
            var salaryTextBlock = SalaryTextBlock ?? FindName(nameof(SalaryTextBlock)) as TextBlock;
            var salaryDetailsTextBlock = SalaryDetailsTextBlock ?? FindName(nameof(SalaryDetailsTextBlock)) as TextBlock;

            if (salaryTextBlock is null || salaryDetailsTextBlock is null)
            {
                ErrorLogger.LogMessage("MainWindow.RecalculateSalary", "Не найдены элементы SalaryTextBlock/SalaryDetailsTextBlock.");
                return;
            }

            var employee = SelectedEmployee;
            if (employee is null)
            {
                salaryTextBlock.Text = "Выберите сотрудника";
                salaryDetailsTextBlock.Text = "Сумма появится здесь сразу после выбора сотрудника и отметок в календаре.";
                return;
            }

            decimal baseAmount = 0;
            decimal extrasAmount = 0;
            var daysWorked = 0;
            decimal totalHours = 0;

            foreach (var mark in employee.Marks.Values.Where(v => v.Date.Year == _selectedMonth.Year && v.Date.Month == _selectedMonth.Month))
            {
                extrasAmount += mark.ExtraAmount;

                if (!mark.IsWorked)
                {
                    continue;
                }

                daysWorked++;
                if (employee.UseHourlyRate)
                {
                    var hours = (decimal)(mark.WorkedHours ?? _globalShiftHours);
                    totalHours += hours;
                    baseAmount += employee.HourlyRate * hours;
                }
                else
                {
                    baseAmount += employee.DailyRate;
                }
            }

            var total = baseAmount + extrasAmount;
            salaryTextBlock.Text = $"{employee.Name}: {total:0.##} ₽";

            salaryDetailsTextBlock.Text = employee.UseHourlyRate
                ? $"Смен: {daysWorked}. Часов: {totalHours:0.##}. База (почасовая): {baseAmount:0.##} ₽. Доплаты: {extrasAmount:0.##} ₽."
                : $"Смен: {daysWorked}. База (дневная): {baseAmount:0.##} ₽. Доплаты: {extrasAmount:0.##} ₽.";
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException("MainWindow.RecalculateSalary", ex, new Dictionary<string, object?>
            {
                ["SelectedMonth"] = _selectedMonth,
                ["GlobalShiftHours"] = _globalShiftHours,
                ["SelectedEmployee"] = SelectedEmployee?.Name ?? "<null>",
                ["EmployeesCount"] = _employees.Count
            });

            MessageBox.Show(
                $"Ошибка расчёта зарплаты. Подробности сохранены в лог:\n{ErrorLogger.LogFilePath}",
                "Ошибка расчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
