using Microsoft.Data.Sqlite;

namespace WarehouseTimesheetApp;

public sealed class StorageService
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WarehouseTimesheetApp",
        "timesheet.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var createEmployees = connection.CreateCommand();
        createEmployees.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Warehouse TEXT NOT NULL,
                ShiftName TEXT NOT NULL,
                DailyRate REAL NOT NULL,
                HourlyRate REAL NOT NULL,
                UseHourlyRate INTEGER NOT NULL
            );
            """;
        createEmployees.ExecuteNonQuery();

        var createMarks = connection.CreateCommand();
        createMarks.CommandText =
            """
            CREATE TABLE IF NOT EXISTS DayMarks (
                EmployeeId INTEGER NOT NULL,
                MarkDate TEXT NOT NULL,
                State INTEGER NOT NULL,
                IsWorked INTEGER NOT NULL,
                ExtraAmount REAL NOT NULL,
                WorkedHours REAL NULL,
                PRIMARY KEY (EmployeeId, MarkDate),
                FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
            );
            """;
        createMarks.ExecuteNonQuery();
    }

    public List<Employee> LoadEmployees()
    {
        var employees = new List<Employee>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var employeeCmd = connection.CreateCommand();
        employeeCmd.CommandText = "SELECT Id, Name, Warehouse, ShiftName, DailyRate, HourlyRate, UseHourlyRate FROM Employees ORDER BY Name;";

        using var reader = employeeCmd.ExecuteReader();
        while (reader.Read())
        {
            employees.Add(new Employee
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Warehouse = reader.GetString(2),
                ShiftName = reader.GetString(3),
                DailyRate = reader.GetDecimal(4),
                HourlyRate = reader.GetDecimal(5),
                UseHourlyRate = reader.GetInt64(6) == 1
            });
        }

        var marksCmd = connection.CreateCommand();
        marksCmd.CommandText = "SELECT EmployeeId, MarkDate, State, IsWorked, ExtraAmount, WorkedHours FROM DayMarks;";

        using var marksReader = marksCmd.ExecuteReader();
        while (marksReader.Read())
        {
            var employee = employees.FirstOrDefault(x => x.Id == marksReader.GetInt32(0));
            if (employee is null)
            {
                continue;
            }

            var date = DateOnly.Parse(marksReader.GetString(1));
            employee.Marks[date] = new DayMark
            {
                Date = date,
                State = (DayMarkState)marksReader.GetInt32(2),
                IsWorked = marksReader.GetInt64(3) == 1,
                ExtraAmount = marksReader.GetDecimal(4),
                WorkedHours = marksReader.IsDBNull(5) ? null : marksReader.GetDouble(5)
            };
        }

        return employees;
    }

    public void SaveEmployee(Employee employee)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        if (employee.Id <= 0)
        {
            var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO Employees (Name, Warehouse, ShiftName, DailyRate, HourlyRate, UseHourlyRate)
                VALUES ($name, $warehouse, $shiftName, $dailyRate, $hourlyRate, $useHourlyRate);
                SELECT last_insert_rowid();
                """;
            BindEmployee(insert, employee);
            employee.Id = Convert.ToInt32(insert.ExecuteScalar());
            return;
        }

        var update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE Employees
            SET Name = $name,
                Warehouse = $warehouse,
                ShiftName = $shiftName,
                DailyRate = $dailyRate,
                HourlyRate = $hourlyRate,
                UseHourlyRate = $useHourlyRate
            WHERE Id = $id;
            """;
        update.Parameters.AddWithValue("$id", employee.Id);
        BindEmployee(update, employee);
        update.ExecuteNonQuery();
    }

    public void DeleteEmployee(int employeeId)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM Employees WHERE Id = $id;";
        delete.Parameters.AddWithValue("$id", employeeId);
        delete.ExecuteNonQuery();
    }

    public void SaveMark(int employeeId, DayMark mark)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO DayMarks (EmployeeId, MarkDate, State, IsWorked, ExtraAmount, WorkedHours)
            VALUES ($employeeId, $markDate, $state, $isWorked, $extraAmount, $workedHours)
            ON CONFLICT(EmployeeId, MarkDate)
            DO UPDATE SET
                State = excluded.State,
                IsWorked = excluded.IsWorked,
                ExtraAmount = excluded.ExtraAmount,
                WorkedHours = excluded.WorkedHours;
            """;

        upsert.Parameters.AddWithValue("$employeeId", employeeId);
        upsert.Parameters.AddWithValue("$markDate", mark.Date.ToString("yyyy-MM-dd"));
        upsert.Parameters.AddWithValue("$state", (int)mark.State);
        upsert.Parameters.AddWithValue("$isWorked", mark.IsWorked ? 1 : 0);
        upsert.Parameters.AddWithValue("$extraAmount", mark.ExtraAmount);
        upsert.Parameters.AddWithValue("$workedHours", mark.WorkedHours is null ? DBNull.Value : mark.WorkedHours);
        upsert.ExecuteNonQuery();
    }

    public void DeleteMark(int employeeId, DateOnly date)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM DayMarks WHERE EmployeeId = $employeeId AND MarkDate = $markDate;";
        delete.Parameters.AddWithValue("$employeeId", employeeId);
        delete.Parameters.AddWithValue("$markDate", date.ToString("yyyy-MM-dd"));
        delete.ExecuteNonQuery();
    }

    private static void BindEmployee(SqliteCommand command, Employee employee)
    {
        command.Parameters.AddWithValue("$name", employee.Name);
        command.Parameters.AddWithValue("$warehouse", employee.Warehouse);
        command.Parameters.AddWithValue("$shiftName", employee.ShiftName);
        command.Parameters.AddWithValue("$dailyRate", employee.DailyRate);
        command.Parameters.AddWithValue("$hourlyRate", employee.HourlyRate);
        command.Parameters.AddWithValue("$useHourlyRate", employee.UseHourlyRate ? 1 : 0);
    }
}
