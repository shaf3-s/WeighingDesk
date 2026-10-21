using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace WeighingDesk;

internal sealed class WeighingTicketRepository
{
    private readonly string _connectionString;

    public WeighingTicketRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = $"Data Source={databasePath}";
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Tickets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TicketNumber TEXT NOT NULL UNIQUE,
                PlateNumber TEXT NOT NULL,
                DriverName TEXT NOT NULL DEFAULT '',
                CustomerName TEXT NOT NULL DEFAULT '',
                MaterialName TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                FirstWeight TEXT NOT NULL,
                FirstWeightWasLoaded INTEGER NOT NULL,
                FirstWeighedAt TEXT NOT NULL,
                SecondWeight TEXT,
                SecondWeighedAt TEXT,
                GrossWeight TEXT,
                TareWeight TEXT,
                NetWeight TEXT,
                MovementType TEXT NOT NULL DEFAULT 'Unspecified',
                FromParty TEXT NOT NULL DEFAULT '',
                ToParty TEXT NOT NULL DEFAULT '',
                WeighedBy TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL DEFAULT 'Open'
            );
            CREATE INDEX IF NOT EXISTS IX_Tickets_Plate_Status ON Tickets(PlateNumber, Status);
            """;
        command.ExecuteNonQuery();
        using var migration = connection.CreateCommand();
        migration.CommandText = "PRAGMA table_info(Tickets)";
        using var reader = migration.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            columns.Add(reader.GetString(1));
        reader.Close();
        foreach (var (column, definition) in new[] { ("MovementType", "TEXT NOT NULL DEFAULT 'Unspecified'"), ("FromParty", "TEXT NOT NULL DEFAULT ''"), ("ToParty", "TEXT NOT NULL DEFAULT ''"), ("WeighedBy", "TEXT NOT NULL DEFAULT ''") })
        {
            if (columns.Contains(column)) continue;
            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE Tickets ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }
    }

    public ActiveTicketInfo? GetActiveTicket(string plateNumber)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TicketNumber, Status, FirstWeightWasLoaded FROM Tickets WHERE PlateNumber = $plate AND Status IN ('Draft','Open','ReadyToClose') ORDER BY Id DESC LIMIT 1";
        command.Parameters.AddWithValue("$plate", plateNumber.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? new ActiveTicketInfo(reader.GetString(0), reader.GetString(1), reader.GetInt32(2) == 1) : null;
    }

    public string StartTicket(string plateNumber, string driver, string customer, string material, string notes, string movementType, string fromParty, string toParty, string weighedBy, bool loaded)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var exists = connection.CreateCommand())
        {
            exists.Transaction = transaction;
            exists.CommandText = "SELECT COUNT(*) FROM Tickets WHERE PlateNumber = $plate AND Status IN ('Draft','Open','ReadyToClose')";
            exists.Parameters.AddWithValue("$plate", plateNumber.Trim());
            if (Convert.ToInt64(exists.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("لهذه المركبة وزنة مفتوحة. اخترها من السجل للمتابعة، أو أغلقها أولاً.");
        }

        var temporaryNumber = "PENDING-" + Guid.NewGuid().ToString("N");
        long id;
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Tickets (TicketNumber, PlateNumber, DriverName, CustomerName, MaterialName, Notes, MovementType, FromParty, ToParty, WeighedBy, FirstWeight, FirstWeightWasLoaded, FirstWeighedAt, Status)
                VALUES ($ticket, $plate, $driver, $customer, $material, $notes, $movement, $from, $to, $weighedBy, '0', $loaded, '', 'Draft')
                """;
            insert.Parameters.AddWithValue("$ticket", temporaryNumber);
            insert.Parameters.AddWithValue("$plate", plateNumber.Trim());
            insert.Parameters.AddWithValue("$driver", driver.Trim());
            insert.Parameters.AddWithValue("$customer", customer.Trim());
            insert.Parameters.AddWithValue("$material", material.Trim());
            insert.Parameters.AddWithValue("$notes", notes.Trim());
            insert.Parameters.AddWithValue("$movement", movementType);
            insert.Parameters.AddWithValue("$from", fromParty.Trim());
            insert.Parameters.AddWithValue("$to", toParty.Trim());
            insert.Parameters.AddWithValue("$weighedBy", weighedBy.Trim());
            insert.Parameters.AddWithValue("$loaded", loaded ? 1 : 0);
            insert.ExecuteNonQuery();
            using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid()";
            id = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var ticketNumber = $"W-{DateTime.Now:yyyyMMdd}-{id:D5}";
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Tickets SET TicketNumber = $ticket WHERE Id = $id";
            update.Parameters.AddWithValue("$ticket", ticketNumber);
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
        transaction.Commit();
        return ticketNumber;
    }

    public void RecordFirstWeighing(string ticketNumber, decimal weight)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tickets SET FirstWeight = $weight, FirstWeighedAt = $at, Status = 'Open' WHERE TicketNumber = $ticket AND Status = 'Draft'";
        command.Parameters.AddWithValue("$weight", Format(weight));
        command.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$ticket", ticketNumber);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("الوزنة الأولى غير متاحة لهذه التذكرة. حدّث سجل التذاكر وحاول مرة ثانية.");
    }

    public TicketDetails? GetTicket(string ticketNumber)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TicketNumber, PlateNumber, DriverName, CustomerName, MaterialName, Notes, FirstWeight, FirstWeightWasLoaded, FirstWeighedAt, SecondWeight, SecondWeighedAt, MovementType, FromParty, ToParty, WeighedBy, Status FROM Tickets WHERE TicketNumber = $ticket";
        command.Parameters.AddWithValue("$ticket", ticketNumber);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new TicketDetails(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
            reader.GetString(6), reader.GetInt32(7) == 1, reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? "" : reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetString(14), reader.GetString(15));
    }

    public void ReopenTicket(string ticketNumber)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        string plate;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT PlateNumber FROM Tickets WHERE TicketNumber = $ticket AND Status IN ('Closed','Complete')";
            find.Parameters.AddWithValue("$ticket", ticketNumber);
            plate = Convert.ToString(find.ExecuteScalar(), CultureInfo.InvariantCulture) ?? throw new InvalidOperationException("لا يمكن إعادة فتح هذه التذكرة.");
        }
        using (var active = connection.CreateCommand())
        {
            active.Transaction = transaction;
            active.CommandText = "SELECT COUNT(*) FROM Tickets WHERE PlateNumber = $plate AND Status IN ('Draft','Open','ReadyToClose')";
            active.Parameters.AddWithValue("$plate", plate);
            if (Convert.ToInt64(active.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("لهذه المركبة تذكرة أخرى نشطة. أغلقها أولاً قبل إعادة فتح هذه التذكرة.");
        }
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Tickets SET Status = CASE WHEN SecondWeight IS NOT NULL THEN 'ReadyToClose' WHEN FirstWeighedAt <> '' THEN 'Open' ELSE 'Draft' END WHERE TicketNumber = $ticket AND Status IN ('Closed','Complete')";
            update.Parameters.AddWithValue("$ticket", ticketNumber);
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("لا يمكن إعادة فتح هذه التذكرة.");
        }
        transaction.Commit();
    }

    public void CloseTicket(string ticketNumber)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tickets SET Status = 'Closed' WHERE TicketNumber = $ticket AND Status = 'ReadyToClose' AND SecondWeight IS NOT NULL";
        command.Parameters.AddWithValue("$ticket", ticketNumber);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("سجّل الوزنتين الأولى والثانية قبل إغلاق التذكرة.");
    }

    public CompletedWeighing RecordSecondWeighing(string ticketNumber, decimal secondWeight)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        long id;
        decimal firstWeight;
        bool firstWeightWasLoaded;
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT Id, TicketNumber, FirstWeight, FirstWeightWasLoaded FROM Tickets WHERE TicketNumber = $ticket AND Status = 'Open' ORDER BY Id DESC LIMIT 1";
            find.Parameters.AddWithValue("$ticket", ticketNumber);
            using var reader = find.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException("ما في وزنة أولى مفتوحة لهذه المركبة.");
            id = reader.GetInt64(0);
            firstWeight = decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            firstWeightWasLoaded = reader.GetInt32(3) == 1;
        }

        var gross = firstWeightWasLoaded ? firstWeight : secondWeight;
        var tare = firstWeightWasLoaded ? secondWeight : firstWeight;
        var net = Math.Abs(gross - tare);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Tickets SET SecondWeight = $second, SecondWeighedAt = $at, GrossWeight = $gross, TareWeight = $tare, NetWeight = $net, Status = 'ReadyToClose' WHERE Id = $id AND Status = 'Open'";
            update.Parameters.AddWithValue("$second", Format(secondWeight));
            update.Parameters.AddWithValue("$at", timestamp);
            update.Parameters.AddWithValue("$gross", Format(gross));
            update.Parameters.AddWithValue("$tare", Format(tare));
            update.Parameters.AddWithValue("$net", Format(net));
            update.Parameters.AddWithValue("$id", id);
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("تغيّرت حالة التذكرة. حدّث السجل وحاول مرة ثانية.");
        }
        transaction.Commit();
        return new CompletedWeighing(ticketNumber, gross, tare, net);
    }

    public void CancelOpenTicket(string ticketNumber)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tickets SET Status = 'Cancelled' WHERE TicketNumber = $ticket AND Status IN ('Draft','Open','ReadyToClose')";
        command.Parameters.AddWithValue("$ticket", ticketNumber);
        if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("ما في تذكرة مفتوحة لهذه المركبة.");
    }

    public void DeleteTicket(string ticketNumber)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tickets WHERE TicketNumber = $ticket";
        command.Parameters.AddWithValue("$ticket", ticketNumber);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("لم يتم العثور على التذكرة للحذف.");
    }

    public DataTable LoadHistory(string? driver = null, string? plate = null, string? material = null, string? movementType = null, DateTime? dateFrom = null, DateTime? dateTo = null, int limit = 300)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id AS 'التسلسل', TicketNumber AS 'رقم التذكرة', PlateNumber AS 'لوحة المركبة', DriverName AS 'السائق',
                   CustomerName AS 'العميل', MaterialName AS 'المادة',
                   FromParty AS 'From', ToParty AS 'To', WeighedBy AS 'اسم منفذ الوزن',
                   CASE MovementType WHEN 'Incoming' THEN 'داخلة للمعمل' WHEN 'Outgoing' THEN 'خارجة من المعمل' WHEN 'Return' THEN 'مرتجع' ELSE 'غير محدد' END AS 'حركة الحمولة',
                   CAST(CASE WHEN FirstWeighedAt = '' THEN NULL ELSE FirstWeight END AS TEXT) AS 'الوزنة الأولى (كغ)', SecondWeight AS 'الوزنة الثانية (كغ)',
                   GrossWeight AS 'القائم (كغ)', TareWeight AS 'الفارغ (كغ)', NetWeight AS 'الصافي (كغ)',
                   FirstWeighedAt AS 'وقت الوزنة الأولى', SecondWeighedAt AS 'وقت الوزنة الثانية',
                   CASE Status WHEN 'Draft' THEN 'بانتظار الوزنة الأولى' WHEN 'Open' THEN 'بانتظار الوزنة الثانية' WHEN 'ReadyToClose' THEN 'جاهزة للإغلاق' WHEN 'Closed' THEN 'مغلقة' WHEN 'Cancelled' THEN 'ملغاة' ELSE 'مكتملة' END AS 'الحالة', Notes AS 'ملاحظات'
            FROM Tickets
            WHERE ($driver IS NULL OR DriverName LIKE $driver)
              AND ($plate IS NULL OR PlateNumber LIKE $plate)
              AND ($material IS NULL OR MaterialName = $material)
              AND ($movement IS NULL OR MovementType = $movement)
              AND ($dateFrom IS NULL OR date(FirstWeighedAt) >= date($dateFrom))
              AND ($dateTo IS NULL OR date(FirstWeighedAt) <= date($dateTo))
            ORDER BY Id DESC LIMIT $limit
            """;
        command.Parameters.AddWithValue("$driver", string.IsNullOrWhiteSpace(driver) ? DBNull.Value : $"%{driver.Trim()}%");
        command.Parameters.AddWithValue("$plate", string.IsNullOrWhiteSpace(plate) ? DBNull.Value : $"%{plate.Trim()}%");
        command.Parameters.AddWithValue("$material", string.IsNullOrWhiteSpace(material) ? DBNull.Value : material);
        command.Parameters.AddWithValue("$movement", string.IsNullOrWhiteSpace(movementType) ? DBNull.Value : movementType);
        command.Parameters.AddWithValue("$dateFrom", dateFrom.HasValue ? dateFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$dateTo", dateTo.HasValue ? dateTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var table = new DataTable();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var fieldType = reader.GetFieldType(i);
            if (reader.GetName(i) == "الوزنة الأولى (كغ)" || fieldType == typeof(byte[]))
                fieldType = typeof(string);
            table.Columns.Add(reader.GetName(i), fieldType);
        }

        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            for (var i = 0; i < values.Length; i++)
                if (values[i] is byte[] bytes)
                    values[i] = Encoding.UTF8.GetString(bytes);
            table.Rows.Add(values);
        }
        return table;
    }

    public List<string> LoadMaterials()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT MaterialName FROM Tickets WHERE MaterialName <> '' ORDER BY MaterialName";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string Format(decimal weight) => weight.ToString(CultureInfo.InvariantCulture);
}

internal sealed record CompletedWeighing(string TicketNumber, decimal Gross, decimal Tare, decimal Net);
internal sealed record ActiveTicketInfo(string TicketNumber, string Status, bool FirstWeightWasLoaded);
internal sealed record TicketDetails(string TicketNumber, string PlateNumber, string DriverName, string CustomerName, string MaterialName, string Notes,
    string FirstWeight, bool FirstWeightWasLoaded, string FirstWeighedAt, string? SecondWeight, string SecondWeighedAt,
    string MovementType, string FromParty, string ToParty, string WeighedBy, string Status);
