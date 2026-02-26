using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WarehouseTimesheetApp;

public partial class MainWindow : Window
{
    private readonly List<Employee> _employees = new();
    private readonly StorageService _storageService = new();
    private DateOnly _selectedMonth;
    private double _globalShiftHours = 8;
    private static readonly DayOfWeek CalendarFirstDayOfWeek = DayOfWeek.Monday;

    public MainWindow()
    {
        InitializeComponent();

        _storageService.Initialize();
        _employees.AddRange(_storageService.LoadEmployees());

        var now = DateTime.Today;
        MonthDatePicker.SelectedDate = now;
        ReportFromDatePicker.SelectedDate = new DateTime(now.Year, now.Month, 1);
        ReportToDatePicker.SelectedDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

        _selectedMonth = new DateOnly(now.Year, now.Month, 1);
        RenderWeekHeaders();
        RefreshEmployeeList(_employees.FirstOrDefault());
        RefreshCalendar();
        RecalculateSalary();
        BuildReport();
    }

    private Employee? SelectedEmployee => EmployeesListBox.SelectedItem as Employee;

    private void OnAddEmployee(object sender, RoutedEventArgs e)
    {
        if (!TryParseEmployeeForm(out var employee))
        {
            return;
        }

        _storageService.SaveEmployee(employee);
        _employees.Add(employee);
        RefreshEmployeeList(employee);
        BuildReport();
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
        SelectedEmployee.Warehouse = parsed.Warehouse;
        SelectedEmployee.ShiftName = parsed.ShiftName;
        SelectedEmployee.DailyRate = parsed.DailyRate;
        SelectedEmployee.HourlyRate = parsed.HourlyRate;
        SelectedEmployee.UseHourlyRate = parsed.UseHourlyRate;

        _storageService.SaveEmployee(SelectedEmployee);
        RefreshEmployeeList(SelectedEmployee);
        RecalculateSalary();
        BuildReport();
    }

    private void OnDeleteEmployee(object sender, RoutedEventArgs e)
    {
        if (SelectedEmployee is null)
        {
            return;
        }

        _storageService.DeleteEmployee(SelectedEmployee.Id);
        _employees.Remove(SelectedEmployee);
        RefreshEmployeeList();
        RefreshCalendar();
        RecalculateSalary();
        BuildReport();
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
            Id = SelectedEmployee?.Id ?? 0,
            Name = NameTextBox.Text.Trim(),
            Warehouse = string.IsNullOrWhiteSpace(WarehouseTextBox.Text) ? "Основной склад" : WarehouseTextBox.Text.Trim(),
            ShiftName = string.IsNullOrWhiteSpace(ShiftNameTextBox.Text) ? "Дневная" : ShiftNameTextBox.Text.Trim(),
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
            NameTextBox.Text = string.Empty;
            WarehouseTextBox.Text = "Основной склад";
            ShiftNameTextBox.Text = "Дневная";
            return;
        }

        NameTextBox.Text = SelectedEmployee.Name;
        WarehouseTextBox.Text = SelectedEmployee.Warehouse;
        ShiftNameTextBox.Text = SelectedEmployee.ShiftName;
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
            BuildReport();
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
        CalendarGrid.Rows = Math.Max(5, rowCount);

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
                MinHeight = 96,
                Margin = new Thickness(2),
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
            Margin = new Thickness(2),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(7, 16, 37)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(22, 35, 63)),
            BorderThickness = new Thickness(1),
            Opacity = 0.62,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = dayNumber.ToString(CultureInfo.InvariantCulture),
                Foreground = new SolidColorBrush(Color.FromRgb(96, 118, 158)),
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
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(7),
                Background = new SolidColorBrush(Color.FromRgb(5, 16, 38)),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = title,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(167, 184, 220))
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

        if (mark.State == DayMarkState.Empty)
        {
            employee.Marks.Remove(date);
            _storageService.DeleteMark(employee.Id, date);
        }
        else
        {
            _storageService.SaveMark(employee.Id, mark);
        }

        ApplyDayVisual(button, date);
        RecalculateSalary();
        BuildReport();
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
            salaryDetailsTextBlock.Text =
                $"Склад: {employee.Warehouse} · Смена: {employee.ShiftName}. " +
                (employee.UseHourlyRate
                    ? $"Смен: {daysWorked}. Часов: {totalHours:0.##}. База: {baseAmount:0.##} ₽. Доплаты: {extrasAmount:0.##} ₽."
                    : $"Смен: {daysWorked}. База: {baseAmount:0.##} ₽. Доплаты: {extrasAmount:0.##} ₽.");
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

    private void OnBuildReport(object sender, RoutedEventArgs e) => BuildReport();

    private void BuildReport()
    {
        var from = ReportFromDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
        var to = ReportToDatePicker.SelectedDate ?? DateTime.Today;

        if (to < from)
        {
            ReportTextBox.Text = "Проверьте период отчёта: дата 'по' меньше даты 'с'.";
            return;
        }

        var fromDate = DateOnly.FromDateTime(from.Date);
        var toDate = DateOnly.FromDateTime(to.Date);

        var sb = new StringBuilder();
        sb.AppendLine($"Отчёт за период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}");
        sb.AppendLine(new string('=', 60));

        var grouped = _employees
            .GroupBy(e => e.Warehouse)
            .OrderBy(g => g.Key);

        foreach (var warehouseGroup in grouped)
        {
            sb.AppendLine($"\nСклад: {warehouseGroup.Key}");

            foreach (var shiftGroup in warehouseGroup.GroupBy(e => e.ShiftName).OrderBy(g => g.Key))
            {
                sb.AppendLine($"  Смена: {shiftGroup.Key}");

                foreach (var employee in shiftGroup.OrderBy(e => e.Name))
                {
                    decimal baseAmount = 0;
                    decimal extras = 0;
                    decimal hours = 0;
                    var shifts = 0;

                    foreach (var mark in employee.Marks.Values.Where(m => m.Date >= fromDate && m.Date <= toDate))
                    {
                        extras += mark.ExtraAmount;
                        if (!mark.IsWorked)
                        {
                            continue;
                        }

                        shifts++;
                        if (employee.UseHourlyRate)
                        {
                            var worked = (decimal)(mark.WorkedHours ?? _globalShiftHours);
                            hours += worked;
                            baseAmount += employee.HourlyRate * worked;
                        }
                        else
                        {
                            baseAmount += employee.DailyRate;
                        }
                    }

                    var total = baseAmount + extras;
                    var mode = employee.UseHourlyRate ? $"почасовая, ч: {hours:0.##}" : "дневная";
                    sb.AppendLine($"    • {employee.Name,-16} | {mode,-20} | смен: {shifts,2} | итог: {total,8:0.##} ₽");
                }
            }
        }

        ReportTextBox.Text = sb.ToString();
    }
}
