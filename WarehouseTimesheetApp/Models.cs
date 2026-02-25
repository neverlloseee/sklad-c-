namespace WarehouseTimesheetApp;

public enum DayMarkState
{
    Empty,
    Worked,
    Absent,
    CustomWorkedOrAbsent,
    CustomHours
}

public class DayMark
{
    public DateOnly Date { get; set; }
    public DayMarkState State { get; set; } = DayMarkState.Empty;
    public bool IsWorked { get; set; }
    public decimal ExtraAmount { get; set; }
    public double? WorkedHours { get; set; }
}

public class Employee
{
    public string Name { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public decimal HourlyRate { get; set; }
    public bool UseHourlyRate { get; set; }
    public Dictionary<DateOnly, DayMark> Marks { get; } = new();

    public override string ToString() => Name;
}
