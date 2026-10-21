using System.Globalization;
using System.Data;
using System.IO.Ports;
using System.Drawing.Printing;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WeighingDesk;

public sealed partial class Form1 : Form
{
    private const string DefaultTicketTemplate = "نظام إدارة أوزان المركبات\r\nالتسلسل: {Serial}\r\nرقم التذكرة: {TicketNumber}\r\nالتاريخ: {FirstWeighedAt}\r\nFrom: {From}\r\nTo: {To}\r\nلوحة المركبة: {PlateNumber}\r\nالسائق: {DriverName}\r\nمنفذ الوزن: {WeighedBy}\r\nالعميل: {CustomerName}\r\nالمادة: {MaterialName}\r\nحركة الحمولة: {MovementType}\r\nالوزنة الأولى: {FirstWeight} كغ\r\nالوزنة الثانية: {SecondWeight} كغ\r\nالقائم: {GrossWeight} كغ\r\nالفارغ: {TareWeight} كغ\r\nالصافي: {NetWeight} كغ\r\nملاحظات: {Notes}\r\n";
    private readonly ComboBox _ports = new();
    private readonly ComboBox _baud = new();
    private readonly ComboBox _parity = new();
    private readonly ComboBox _dataBits = new();
    private readonly ComboBox _stopBits = new();
    private readonly NumericUpDown _scaleAddress = new();
    private readonly NumericUpDown _masterSlaveDecimals = new();
    private readonly TextBox _serialDiagnostics = new();
    private readonly TextBox _lastCommandSentBox = new();
    private string _lastTxHex = "—";
    private readonly Label _readOnDemandHint = new();
    private readonly Button _tunaylarPreset = new();
    private readonly Button _refreshPortsButton = new();
    private readonly Button _connect = new();
    private readonly Button _simulate = new();
    private readonly Label _connection = new();
    private readonly TextBox _plate = new();
    private readonly TextBox _driver = new();
    private readonly TextBox _customer = new();
    private readonly TextBox _weighedBy = new();
    private readonly TextBox _from = new();
    private readonly TextBox _to = new();
    private readonly ComboBox _material = new();
    private readonly ComboBox _language = new();
    private readonly ComboBox _printer = new();
    private readonly ComboBox _paperSize = new();
    private readonly NumericUpDown _paperWidth = new();
    private readonly NumericUpDown _paperHeight = new();
    private readonly RichTextBox _ticketTemplate = new();
    private readonly ComboBox _pageOrientation = new();
    private readonly NumericUpDown _marginRight = new();
    private readonly NumericUpDown _marginLeft = new();
    private readonly NumericUpDown _marginTop = new();
    private readonly NumericUpDown _marginBottom = new();
    private readonly Button _alignRight = new();
    private readonly Button _alignCenter = new();
    private readonly Button _alignLeft = new();
    private readonly Button _toggleBold = new();
    private readonly Button _insertTable = new();
    private readonly TextBox _logoPath = new();
    private readonly PictureBox _logoPreview = new();
    private readonly Button _savePrintSettings = new();
    private readonly Button _previewTicketTemplate = new();
    private readonly TextBox _otherMaterial = new();
    private readonly Label _otherMaterialLabel = new();
    private TableLayoutPanel _ticketGrid = null!;
    private readonly TextBox _notes = new();
    private readonly ComboBox _firstLoadState = new();
    private readonly ComboBox _movementType = new();
    private readonly TextBox _filterDriver = new();
    private readonly TextBox _filterPlate = new();
    private readonly ComboBox _filterMaterial = new();
    private readonly ComboBox _filterMovement = new();
    private readonly DateTimePicker _filterDateFrom = new();
    private readonly DateTimePicker _filterDateTo = new();
    private readonly Button _applyFilters = new();
    private readonly Button _clearFilters = new();
    private readonly Button _printSelectedTicket = new();
    private readonly Button _printFilteredReport = new();
    private readonly Button _exportFilteredReport = new();
    private readonly Button _printAllOperations = new();
    private readonly Button _exportAllOperations = new();
    private readonly Label _ticketHint = new();
    private readonly Button _firstWeigh = new();
    private readonly Button _secondWeigh = new();
    private readonly Button _openTicketButton = new();
    private readonly Button _closeTicketButton = new();
    private readonly Button _cancelTicket = new();
    private readonly Button _changeSimulatedWeight = new();
    private readonly DataGridView _history = new();
    private readonly DataGridView _homeHistory = new();
    private readonly Button _printHomeTicket = new();
    private readonly Button _deleteHomeTicket = new();
    private readonly Button _deleteReportTicket = new();
    private readonly Label _incomingTotal = new();
    private readonly Label _outgoingTotal = new();
    private readonly Label _remainingTotal = new();
    private readonly System.Windows.Forms.Timer _weighCaptureTimer = new() { Interval = 8000 };
    private readonly string _databasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeighingDesk", "weighings.db");
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeighingDesk", "scale-settings.json");
    private readonly string _logoDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeighingDesk", "TicketLogo");
    private readonly WeighingTicketRepository _tickets;
    private readonly TunaylarMasterSlaveProtocol _masterSlaveProtocol = new();
    private readonly TunaylarManualTransmitProtocol _manualTransmitProtocol = new();
    private Control? _reportsContent;
    private Control? _settingsContent;
    private ScaleSettings _settings = new();
    private SerialPort? _serial;
    private decimal? _currentWeight;
    private bool _simulationEnabled;
    private bool _applyingLanguage;
    private bool _hasScaleReading;
    private volatile int _configuredScaleAddress = 1;
    private volatile int _configuredMasterSlaveDecimals;
    private long _serialBytesReceived;
    private string _lastRxHex = "—";
    private string? _activeTicketNumber;
    private string? _pendingTicketNumber;
    private PendingWeighingCapture _pendingWeighingCapture;

    private enum PendingWeighingCapture { None, First, Second }

    private bool IsMasterSlaveMode => true;

    public Form1()
    {
        _tickets = new WeighingTicketRepository(_databasePath);
        InitializeComponent();
        var appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WeighingDesk.ico");
        if (File.Exists(appIconPath)) Icon = new Icon(appIconPath);
        BuildUi();
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
        WindowState = FormWindowState.Maximized;
        Resize += (_, _) => UpdateResponsiveLayout();
        DpiChanged += (_, _) => UpdateResponsiveLayout();
        LoadSettings();
        RefreshPrinters();
        _language.SelectedIndexChanged += (_, _) =>
        {
            if (!_applyingLanguage) ApplyLanguage();
        };
        _tickets.Initialize();
        RefreshPorts();
        RefreshMaterialFilterOptions();
        RefreshHistory();
        UpdateTicketHint();
        MarkOriginalTexts(this);
        ApplyLanguage();
        _weighCaptureTimer.Tick += (_, _) => CancelPendingWeighingCapture(showMessage: true);
        Shown += (_, _) =>
        {
            UpdateResponsiveLayout();
            TryConnectAutomatically();
        };
        FormClosing += (_, _) =>
        {
            SaveSettings();
            _weighCaptureTimer.Stop();
            Disconnect();
            _weighCaptureTimer.Dispose();
        };
    }

    [Category("Layout"), Description("ارتفاع مساحة بيانات المركبة والتذكرة وأزرار الوزن."), DefaultValue(500)]
    public int TicketSectionHeight
    {
        get => GetHomeRowHeight(0, 500);
        set => SetHomeRowHeight(0, value);
    }

    private int GetHomeRowHeight(int row, int defaultValue) =>
        homeLayout is not null && homeLayout.RowStyles.Count > row ? (int)homeLayout.RowStyles[row].Height : defaultValue;

    private void SetHomeRowHeight(int row, int value)
    {
        if (homeLayout is null || homeLayout.RowStyles.Count <= row) return;
        homeLayout.RowStyles[row].SizeType = SizeType.Absolute;
        homeLayout.RowStyles[row].Height = Math.Clamp(value, 64, 1000);
    }

    private void BuildUi()
    {
        var root = homeLayout;
        homePage.AutoScroll = true;
        root.Dock = DockStyle.Top;
        root.RowCount = 2;
        root.RowStyles.Clear();
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 520));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 540));
        _connection.Text = "غير متصل";
        _connection.AutoEllipsis = true;
        _connection.TextAlign = ContentAlignment.MiddleCenter;
        _connection.ForeColor = Color.Firebrick;
        root.Controls.Add(CreateAdjustableSection(BuildTicketPanel(), 300, 1100, 520, height => SetAndResizeRow(root, 0, height)), 0, 0);
        root.Controls.Add(CreateAdjustableSection(BuildHomeHistory(), 250, 1500, 540, height => SetAndResizeRow(root, 1, height)), 0, 1);
        ResizeLayoutToRows(root);
        homePage.Controls.Add(root);
        reportsPage.AutoScroll = true;
        _reportsContent = BuildReportsPage();
        _reportsContent.Dock = DockStyle.Top;
        reportsPage.Controls.Add(_reportsContent);
        settingsPage.AutoScroll = true;
        _settingsContent = BuildSettingsPage();
        _settingsContent.Dock = DockStyle.Top;
        settingsPage.Controls.Add(_settingsContent);
        var appShell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 8, 12, 8) };
        appShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        appShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        appShell.Controls.Add(BuildHeader(), 0, 0);
        mainTabs.Dock = DockStyle.Fill;
        appShell.Controls.Add(mainTabs, 0, 1);
        Controls.Remove(mainTabs);
        Controls.Add(appShell);
        UpdateResponsiveLayout();
        NormalizeButtonSizing(this);

    }

    private void UpdateResponsiveLayout()
    {
        ResizeLayoutToRows(homeLayout);
        if (_reportsContent is TableLayoutPanel reportsLayout) ResizeLayoutToRows(reportsLayout);
        if (_settingsContent is TableLayoutPanel settingsLayout) ResizeLayoutToRows(settingsLayout);
    }

    private static Control CreateAdjustableSection(Control section, int minimum, int maximum, int initial, Action<int> setHeight)
    {
        var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
        wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(2), Padding = new Padding(2, 0, 2, 0), BackColor = Color.FromArgb(244, 247, 250) };
        var label = new Label { Text = "ارتفاع القسم", AutoSize = false, Size = new Size(104, 30), TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(8, 5, 4, 0), ForeColor = Color.FromArgb(60, 75, 90) };
        var slider = new TrackBar { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(initial, minimum, maximum), TickFrequency = 100, SmallChange = 10, LargeChange = 50, Width = 190, Height = 36, AutoSize = false, TickStyle = TickStyle.None, Margin = new Padding(4, 0, 4, 0) };
        var number = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = slider.Value, Width = 82, Height = 30, TextAlign = HorizontalAlignment.Center, Margin = new Padding(4, 5, 4, 0) };
        slider.ValueChanged += (_, _) =>
        {
            if (number.Value != slider.Value) number.Value = slider.Value;
            setHeight(slider.Value);
        };
        number.ValueChanged += (_, _) =>
        {
            var value = (int)number.Value;
            if (slider.Value != value) slider.Value = value;
            setHeight(value);
        };
        toolbar.Controls.Add(label);
        toolbar.Controls.Add(slider);
        toolbar.Controls.Add(number);
        section.Dock = DockStyle.Fill;
        wrapper.Controls.Add(toolbar, 0, 0);
        wrapper.Controls.Add(section, 0, 1);
        return wrapper;
    }

    private static void SetAndResizeRow(TableLayoutPanel layout, int row, int height)
    {
        if (row < 0 || row >= layout.RowStyles.Count) return;
        layout.RowStyles[row].SizeType = SizeType.Absolute;
        layout.RowStyles[row].Height = height;
        ResizeLayoutToRows(layout);
    }

    private static void ResizeLayoutToRows(TableLayoutPanel layout)
    {
        if (layout.RowStyles.Count == 0) return;
        var height = 0;
        for (var row = 0; row < layout.RowStyles.Count; row++)
        {
            if (layout.RowStyles[row].SizeType != SizeType.Absolute) layout.RowStyles[row].SizeType = SizeType.Absolute;
            height += (int)layout.RowStyles[row].Height;
        }
        layout.Height = height;
    }

    private static void NormalizeButtonSizing(Control parent)
    {
        if (parent is TableLayoutPanel table)
        {
            foreach (Control child in table.Controls)
            {
                var row = table.GetRow(child);
                if (child is Button && row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType == SizeType.Absolute && table.RowStyles[row].Height < 72)
                    table.RowStyles[row].Height = 72;
            }
        }

        foreach (Control child in parent.Controls)
        {
            if (child is Button button)
            {
                button.AutoSize = false;
                button.MinimumSize = new Size(0, 64);
                button.Padding = new Padding(10);
                button.Font = new Font("Segoe UI", 11.5F, FontStyle.Regular);
            }
            NormalizeButtonSizing(child);
        }
    }

    private Control BuildReportsPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 700));
        var filters = new GroupBox { Text = "خيارات البحث", Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 4 };
        for (var i = 0; i < 8; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        AddLabeled(grid, "السائق", _filterDriver, 0, 0);
        AddLabeled(grid, "لوحة المركبة", _filterPlate, 2, 0);
        ConfigureDropDown(_filterMaterial);
        AddLabeled(grid, "المادة", _filterMaterial, 4, 0);
        ConfigureDropDown(_filterMovement);
        _filterMovement.Items.AddRange(new object[] { "الكل", "داخلة للمعمل", "خارجة من المعمل", "مرتجع" });
        _filterMovement.SelectedIndex = 0;
        AddLabeled(grid, "الحركة", _filterMovement, 6, 0);
        ConfigureDateFilter(_filterDateFrom);
        AddLabeled(grid, "من تاريخ", _filterDateFrom, 0, 1);
        ConfigureDateFilter(_filterDateTo);
        AddLabeled(grid, "إلى تاريخ", _filterDateTo, 2, 1);
        _applyFilters.Text = "بحث";
        _applyFilters.Dock = DockStyle.Fill;
        _applyFilters.Click += (_, _) => RefreshHistory();
        grid.Controls.Add(_applyFilters, 4, 1);
        grid.SetColumnSpan(_applyFilters, 2);
        _clearFilters.Text = "مسح الفلاتر";
        _clearFilters.Dock = DockStyle.Fill;
        _clearFilters.Click += (_, _) => ClearFilters();
        grid.Controls.Add(_clearFilters, 6, 1);
        grid.SetColumnSpan(_clearFilters, 2);
        filters.Controls.Add(grid);
        layout.Controls.Add(CreateAdjustableSection(filters, 220, 900, 360, height => SetAndResizeRow(layout, 0, height)), 0, 0);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = BackColor, Padding = new Padding(0, 4, 0, 4) };
        for (var i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        ConfigureActionButton(_printSelectedTicket, "طباعة التذكرة المحددة", PrintSelectedTicket);
        ConfigureActionButton(_printFilteredReport, "طباعة نتائج البحث", PrintFilteredReport);
        ConfigureActionButton(_exportFilteredReport, "تصدير النتائج إلى Excel", ExportFilteredReport);
        ConfigureActionButton(_printAllOperations, "طباعة كل عمليات الوزن", PrintAllOperations);
        ConfigureActionButton(_exportAllOperations, "تصدير كل العمليات إلى Excel", ExportAllOperations);
        ConfigureActionButton(_deleteReportTicket, "حذف التذكرة المحددة", () => DeleteSelectedTicket(_history));
        actions.Controls.Add(_printSelectedTicket, 0, 0);
        actions.Controls.Add(_printFilteredReport, 1, 0);
        actions.Controls.Add(_exportFilteredReport, 2, 0);
        actions.Controls.Add(_printAllOperations, 0, 1);
        actions.Controls.Add(_exportAllOperations, 1, 1);
        actions.Controls.Add(_deleteReportTicket, 2, 1);
        layout.Controls.Add(CreateAdjustableSection(actions, 120, 500, 190, height => SetAndResizeRow(layout, 1, height)), 0, 1);

        var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(2) };
        for (var i = 0; i < 3; i++) totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        ConfigureTotalLabel(_incomingTotal, "الكمية الداخلة للمعمل");
        ConfigureTotalLabel(_outgoingTotal, "الكمية الخارجة من المعمل");
        ConfigureTotalLabel(_remainingTotal, "الكمية المتبقية في المعمل");
        totals.Controls.Add(_incomingTotal, 0, 0);
        totals.Controls.Add(_outgoingTotal, 1, 0);
        totals.Controls.Add(_remainingTotal, 2, 0);
        layout.Controls.Add(CreateAdjustableSection(totals, 70, 300, 90, height => SetAndResizeRow(layout, 2, height)), 0, 2);

        var historyGroup = new GroupBox { Text = "سجل التذاكر — آخر 300 نتيجة", Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        ConfigureHistoryGrid(_history);
        historyGroup.Controls.Add(_history);
        layout.Controls.Add(CreateAdjustableSection(historyGroup, 250, 1600, 700, height => SetAndResizeRow(layout, 3, height)), 0, 3);
        ResizeLayoutToRows(layout);
        return layout;
    }

    private Control BuildHomeHistory()
    {
        var group = new GroupBox { Text = "سجل الأوزان الأخير", Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ConfigureActionButton(_printHomeTicket, "طباعة التذكرة المحددة", () => PrintSelectedTicket(_homeHistory));
        ConfigureActionButton(_deleteHomeTicket, "حذف التذكرة المحددة", () => DeleteSelectedTicket(_homeHistory));
        layout.Controls.Add(_printHomeTicket, 0, 0);
        layout.Controls.Add(_deleteHomeTicket, 1, 0);
        ConfigureHistoryGrid(_homeHistory);
        layout.Controls.Add(_homeHistory, 0, 1);
        layout.SetColumnSpan(_homeHistory, 2);
        group.Controls.Add(layout);
        return group;
    }

    private static void ConfigureTotalLabel(Label label, string title)
    {
        label.Text = $"{title}: 0 كغ";
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        label.ForeColor = Color.FromArgb(35, 56, 78);
        label.BackColor = Color.White;
        label.Margin = new Padding(4);
        label.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void ConfigureHistoryGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(229, 234, 240);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 241, 247);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(35, 56, 78);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 42;
        grid.RowTemplate.Height = 34;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(216, 232, 246);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 42, 58);
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
    }

    private Control BuildSettingsPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 850));
        var languageGroup = new GroupBox { Text = "اللغة", Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var languageLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
        languageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        languageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        var languageLabel = new Label { Text = "اللغة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        ConfigureDropDown(_language);
        _language.Items.AddRange(new object[] { "العربية", "English", "کوردی" });
        _language.SelectedIndex = 0;
        languageLayout.Controls.Add(languageLabel, 0, 0);
        languageLayout.Controls.Add(_language, 1, 0);
        languageGroup.Controls.Add(languageLayout);
        layout.Controls.Add(CreateAdjustableSection(languageGroup, 70, 300, 90, height => SetAndResizeRow(layout, 0, height)), 0, 0);
        layout.Controls.Add(CreateAdjustableSection(CreateScrollableSection(BuildConnectionPanel(), 390), 180, 1000, 430, height => SetAndResizeRow(layout, 1, height)), 0, 1);

        var printGroup = new GroupBox { Text = "إعدادات الطباعة ونموذج التذكرة", Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var printLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 8 };
        printLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        printLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        printLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        printLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        printLayout.Padding = new Padding(6);
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        printLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        AddLabeled(printLayout, "الطابعة", _printer, 0, 0);
        ConfigureDropDown(_printer);
        _printer.Items.AddRange(PrinterSettings.InstalledPrinters.Cast<object>().ToArray());
        var refreshPrinters = new Button { Text = "تحديث الطابعات", Dock = DockStyle.Fill };
        refreshPrinters.Click += (_, _) => RefreshPrinters();
        printLayout.Controls.Add(refreshPrinters, 2, 0);
        _logoPreview.Dock = DockStyle.Fill;
        _logoPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _logoPreview.BorderStyle = BorderStyle.FixedSingle;
        printLayout.Controls.Add(_logoPreview, 3, 0);
        AddLabeled(printLayout, "حجم الورقة", _paperSize, 0, 1);
        ConfigureDropDown(_paperSize);
        _paperSize.Items.AddRange(new object[] { "افتراضي", "A4", "A5", "Letter", "Legal", "A3", "إيصال 80 مم", "مخصص" });
        _paperSize.SelectedIndex = 0;
        AddLabeled(printLayout, "العرض (مم)", _paperWidth, 2, 1);
        ConfigurePaperDimension(_paperWidth);
        AddLabeled(printLayout, "الارتفاع (مم)", _paperHeight, 0, 2);
        ConfigurePaperDimension(_paperHeight);
        AddLabeled(printLayout, "اتجاه الصفحة", _pageOrientation, 2, 2);
        ConfigureDropDown(_pageOrientation);
        _pageOrientation.Items.AddRange(new object[] { "طولي", "عرضي" });
        _pageOrientation.SelectedIndex = 0;
        _paperSize.SelectedIndexChanged += (_, _) => UpdatePaperDimensionState();
        printLayout.Controls.Add(new Label { Text = "الهوامش (مم)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
        var margins = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, Margin = new Padding(0) };
        for (var i = 0; i < 8; i++) margins.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        AddMarginEditor(margins, "يمين", _marginRight, 0);
        AddMarginEditor(margins, "يسار", _marginLeft, 2);
        AddMarginEditor(margins, "أعلى", _marginTop, 4);
        AddMarginEditor(margins, "أسفل", _marginBottom, 6);
        printLayout.Controls.Add(margins, 1, 3);
        printLayout.SetColumnSpan(margins, 3);
        printLayout.Controls.Add(new Label { Text = "شعار التذكرة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 4);
        _logoPath.Dock = DockStyle.Fill;
        _logoPath.ReadOnly = true;
        printLayout.Controls.Add(_logoPath, 1, 4);
        _logoPath.ReadOnly = true;
        var chooseLogo = new Button { Text = "اختيار صورة الشعار", Dock = DockStyle.Fill };
        chooseLogo.Click += (_, _) => ChooseLogo();
        printLayout.Controls.Add(chooseLogo, 2, 4);
        var removeLogo = new Button { Text = "إزالة", Dock = DockStyle.Fill };
        removeLogo.Click += (_, _) => SetLogo("");
        printLayout.Controls.Add(removeLogo, 3, 4);
        var editorToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoScroll = true, Margin = new Padding(0) };
        ConfigureEditorButton(_alignRight, "يمين", () => _ticketTemplate.SelectionAlignment = HorizontalAlignment.Right);
        ConfigureEditorButton(_alignCenter, "وسط", () => _ticketTemplate.SelectionAlignment = HorizontalAlignment.Center);
        ConfigureEditorButton(_alignLeft, "يسار", () => _ticketTemplate.SelectionAlignment = HorizontalAlignment.Left);
        ConfigureEditorButton(_toggleBold, "عريض", ToggleTicketBold);
        ConfigureEditorButton(_insertTable, "إدراج صفوف جدول", InsertTicketTable);
        editorToolbar.Controls.AddRange(new Control[] { _alignRight, _alignCenter, _alignLeft, _toggleBold, _insertTable });
        printLayout.Controls.Add(editorToolbar, 0, 5);
        printLayout.SetColumnSpan(editorToolbar, 4);
        _ticketTemplate.Multiline = true;
        _ticketTemplate.ScrollBars = RichTextBoxScrollBars.Both;
        _ticketTemplate.AcceptsTab = true;
        _ticketTemplate.Dock = DockStyle.Fill;
        _ticketTemplate.Font = new Font("Segoe UI", 10F);
        _ticketTemplate.Text = DefaultTicketTemplate;
        printLayout.Controls.Add(_ticketTemplate, 0, 6);
        printLayout.SetColumnSpan(_ticketTemplate, 4);
        ConfigureActionButton(_savePrintSettings, "حفظ إعدادات الطباعة", SavePrintSettings);
        ConfigureActionButton(_previewTicketTemplate, "تجربة نموذج الطباعة", PreviewTicketTemplate);
        printLayout.Controls.Add(_savePrintSettings, 0, 7);
        printLayout.SetColumnSpan(_savePrintSettings, 2);
        printLayout.Controls.Add(_previewTicketTemplate, 2, 7);
        printLayout.SetColumnSpan(_previewTicketTemplate, 2);
        printGroup.Controls.Add(printLayout);
        layout.Controls.Add(CreateAdjustableSection(CreateScrollableSection(printGroup, 760), 500, 2000, 850, height => SetAndResizeRow(layout, 2, height)), 0, 2);
        ResizeLayoutToRows(layout);
        return layout;
    }

    private static Panel CreateScrollableSection(Control section, int contentHeight)
    {
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0), Padding = new Padding(0) };
        section.Dock = DockStyle.Top;
        section.Height = contentHeight;
        section.Margin = new Padding(0);
        scrollPanel.Controls.Add(section);
        scrollPanel.SizeChanged += (_, _) => section.Width = scrollPanel.ClientSize.Width;
        section.Width = scrollPanel.ClientSize.Width;
        return scrollPanel;
    }

    private static void AddMarginEditor(TableLayoutPanel panel, string caption, NumericUpDown control, int column)
    {
        panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, column, 0);
        control.Dock = DockStyle.Fill;
        control.Minimum = 0;
        control.Maximum = 100;
        control.Value = 12;
        control.DecimalPlaces = 0;
        control.Increment = 1;
        panel.Controls.Add(control, column + 1, 0);
    }

    private static void ConfigureEditorButton(Button button, string text, Action action)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(86, 40);
        button.Margin = new Padding(3);
        button.Click += (_, _) => action();
    }

    private void ToggleTicketBold()
    {
        var current = _ticketTemplate.SelectionFont ?? _ticketTemplate.Font;
        var style = current.Style ^ FontStyle.Bold;
        _ticketTemplate.SelectionFont = new Font(current.FontFamily, current.Size, style);
        _ticketTemplate.Focus();
    }

    private void InsertTicketTable()
    {
        var table = "الحقل 1: {TicketNumber}\r\nالحقل 2: {PlateNumber}\r\nالحقل 3: {NetWeight}\r\n";
        _ticketTemplate.SelectedText = table;
        _ticketTemplate.Focus();
    }
    private void RefreshPrinters()
    {
        var selected = _printer.SelectedItem?.ToString() ?? _settings.PrinterName;
        _printer.Items.Clear();
        _printer.Items.AddRange(PrinterSettings.InstalledPrinters.Cast<object>().ToArray());
        if (selected.Length > 0 && _printer.Items.Contains(selected)) _printer.SelectedItem = selected;
        else if (_printer.Items.Count > 0) _printer.SelectedIndex = 0;
    }

    private static void ConfigurePaperDimension(NumericUpDown control)
    {
        control.Dock = DockStyle.Fill;
        control.Minimum = 50;
        control.Maximum = 2000;
        control.DecimalPlaces = 0;
        control.Increment = 1;
        control.Value = 210;
    }

    private void UpdatePaperDimensionState()
    {
        var custom = _paperSize.SelectedIndex == 7;
        _paperWidth.Enabled = custom;
        _paperHeight.Enabled = custom;
    }

    private void SavePrintSettings()
    {
        SaveSettings();
        MessageBox.Show(this, T("تم حفظ إعدادات الطباعة"), T("تم الحفظ"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ChooseLogo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = T("اختيار صورة الشعار"),
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetLogo(dialog.FileName);
    }

    private void SetLogo(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logoPath.Text = "";
        }
        else
        {
            try
            {
                using var image = Image.FromFile(path);
                Directory.CreateDirectory(_logoDirectory);
                var extension = Path.GetExtension(path);
                if (string.IsNullOrWhiteSpace(extension) || extension.Length > 8) extension = ".png";
                var storedPath = Path.Combine(_logoDirectory, "ticket-logo" + extension.ToLowerInvariant());
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(storedPath), StringComparison.OrdinalIgnoreCase))
                    File.Copy(path, storedPath, overwrite: true);
                _logoPath.Text = storedPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"تعذر حفظ صورة الشعار: {ex.Message}", T("خطأ"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        UpdateLogoPreview();
        SaveSettings();
    }

    private void UpdateLogoPreview()
    {
        var previous = _logoPreview.Image;
        _logoPreview.Image = null;
        previous?.Dispose();
        if (!File.Exists(_logoPath.Text)) return;
        try
        {
            using var source = Image.FromFile(_logoPath.Text);
            _logoPreview.Image = new Bitmap(source);
        }
        catch { }
    }

    private static void ConfigureActionButton(Button button, string text, Action action)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(4);
        button.AutoSize = false;
        button.MinimumSize = new Size(0, 64);
        button.Padding = new Padding(10);
        button.Font = new Font("Segoe UI", 11.5F, FontStyle.Regular);
        button.Click += (_, _) => action();
    }

    private static void ConfigureDateFilter(DateTimePicker picker)
    {
        picker.Dock = DockStyle.Fill;
        picker.Format = DateTimePickerFormat.Short;
        picker.ShowCheckBox = true;
        picker.Checked = false;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, RightToLeft = RightToLeft.Yes, BackColor = Color.White, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 8) };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 82));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

        var manaraPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = new Padding(4) };
        manaraPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 76));
        manaraPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        manaraPanel.Controls.Add(CreateLogo("Picture2.png"), 0, 0);
        manaraPanel.Controls.Add(new Label
        {
            Text = "نظام إدارة أوزان المركبات", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.FromArgb(105, 105, 105),
            BackColor = Color.White
        }, 0, 1);
        header.Controls.Add(manaraPanel, 0, 0);

        var shenPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(4) };
        shenPanel.Controls.Add(CreateLogo("Picture3.png"));
        header.Controls.Add(shenPanel, 2, 0);

        header.Controls.Add(new Label
        {
            Text = "نظام إدارة أوزان المركبات", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(24, 44, 65)
        }, 1, 0);
        _connection.Dock = DockStyle.Fill;
        _connection.Font = new Font("Segoe UI", 9F);
        header.Controls.Add(_connection, 0, 1);
        header.SetColumnSpan(_connection, 3);
        return header;
    }

    private static PictureBox CreateLogo(string fileName)
    {
        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Padding = new Padding(6)
        };
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (File.Exists(path))
        {
            using var source = Image.FromFile(path);
            picture.Image = new Bitmap(source);
        }
        return picture;
    }

    private Control BuildConnectionPanel()
    {
        var group = new GroupBox { Text = "اتصال الميزان وإعدادات RS-232", Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 5 };
        for (var i = 0; i < 8; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        ConfigureDropDown(_ports);
        AddLabeled(grid, "منفذ COM", _ports, 0, 0);
        ConfigureDropDown(_baud);
        _baud.Items.AddRange(new object[] { "300", "600", "1200", "2400", "4800", "9600", "19200", "38400" });
        AddLabeled(grid, "السرعة", _baud, 2, 0);
        ConfigureDropDown(_parity);
        _parity.Items.AddRange(Enum.GetNames<Parity>());
        AddLabeled(grid, "Parity", _parity, 4, 0);
        ConfigureDropDown(_dataBits);
        _dataBits.Items.AddRange(new object[] { "5", "6", "7", "8" });
        AddLabeled(grid, "Data bits", _dataBits, 6, 0);

        ConfigureDropDown(_stopBits);
        _stopBits.Items.AddRange(new object[] { "One", "OnePointFive", "Two" });
        AddLabeled(grid, "Stop bits", _stopBits, 0, 1);
        _connect.Text = "اتصال";
        _connect.Dock = DockStyle.Fill;
        _connect.Click += (_, _) => ToggleConnection();
        grid.Controls.Add(_connect, 0, 1);
        grid.SetColumnSpan(_connect, 4);
        _simulate.Text = "وضع تجريبي";
        _simulate.Dock = DockStyle.Fill;
        _simulate.Click += (_, _) => ToggleSimulation();
        grid.Controls.Add(_simulate, 4, 1);
        grid.SetColumnSpan(_simulate, 4);
        _refreshPortsButton.Text = "تحديث منافذ COM";
        _refreshPortsButton.Dock = DockStyle.Fill;
        _refreshPortsButton.Margin = new Padding(3, 5, 3, 0);
        _refreshPortsButton.Click += (_, _) => RefreshPorts();
        grid.Controls.Add(_refreshPortsButton, 0, 2);
        grid.SetColumnSpan(_refreshPortsButton, 4);

        _tunaylarPreset.Text = "إعدادات Tunaylar: 9600 / 8-N-1";
        _tunaylarPreset.Dock = DockStyle.Fill;
        _tunaylarPreset.Margin = new Padding(3, 5, 3, 0);
        _tunaylarPreset.Click += (_, _) => ApplyTunaylarConnectionPreset();
        grid.Controls.Add(_tunaylarPreset, 4, 2);
        grid.SetColumnSpan(_tunaylarPreset, 4);
        _serialDiagnostics.ReadOnly = true;
        _serialDiagnostics.BorderStyle = BorderStyle.FixedSingle;
        _serialDiagnostics.BackColor = Color.FromArgb(247, 249, 251);
        _serialDiagnostics.Font = new Font("Consolas", 9F);
        _serialDiagnostics.TextAlign = HorizontalAlignment.Left;
        _serialDiagnostics.RightToLeft = RightToLeft.No;
        _serialDiagnostics.Dock = DockStyle.Fill;
        _serialDiagnostics.Margin = new Padding(3, 2, 3, 2);
        var serialStatusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        serialStatusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        serialStatusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        var commandCell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        commandCell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        commandCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var lastCommandLabel = new Label { Text = T("آخر أمر مرسل للجهاز"), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, ForeColor = Color.FromArgb(60, 75, 90) };
        commandCell.Controls.Add(lastCommandLabel, 0, 0);
        _lastCommandSentBox.ReadOnly = true;
        _lastCommandSentBox.BorderStyle = BorderStyle.FixedSingle;
        _lastCommandSentBox.BackColor = Color.FromArgb(247, 249, 251);
        _lastCommandSentBox.Font = new Font("Consolas", 11F, FontStyle.Bold);
        _lastCommandSentBox.TextAlign = HorizontalAlignment.Center;
        _lastCommandSentBox.RightToLeft = RightToLeft.No;
        _lastCommandSentBox.Dock = DockStyle.Fill;
        commandCell.Controls.Add(_lastCommandSentBox, 1, 0);
        serialStatusRow.Controls.Add(commandCell, 0, 0);
        serialStatusRow.Controls.Add(_serialDiagnostics, 1, 0);
        grid.Controls.Add(serialStatusRow, 0, 3);
        grid.SetColumnSpan(serialStatusRow, 8);

        _scaleAddress.Minimum = 1;
        _scaleAddress.Maximum = 32;
        _scaleAddress.Value = 1;
        AddLabeled(grid, "عنوان الميزان", _scaleAddress, 0, 4);
        _masterSlaveDecimals.Minimum = 0;
        _masterSlaveDecimals.Maximum = 4;
        _masterSlaveDecimals.Value = 0;
        AddLabeled(grid, "خانات الكسور", _masterSlaveDecimals, 2, 4);
        _readOnDemandHint.Text = "قراءة الوزن عند تسجيل الوزنة فقط";
        _readOnDemandHint.Dock = DockStyle.Fill;
        _readOnDemandHint.TextAlign = ContentAlignment.MiddleCenter;
        grid.Controls.Add(_readOnDemandHint, 4, 4);
        grid.SetColumnSpan(_readOnDemandHint, 4);

        group.Controls.Add(grid);
        UpdateSerialDiagnostics();

        return group;
    }

    private Control BuildTicketPanel()
    {
        var group = new GroupBox { Text = "بيانات المركبة والتذكرة", Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8), BackColor = Color.White, ForeColor = Color.FromArgb(35, 56, 78) };
        var grid = _ticketGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 7 };
        for (var i = 0; i < 8; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        for (var row = 0; row < 6; row++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        _plate.PlaceholderText = "مثال: 12-34567";
        AddLabeled(grid, "لوحة المركبة", _plate, 0, 0);
        AddLabeled(grid, "السائق", _driver, 2, 0);
        AddLabeled(grid, "العميل / الجهة", _customer, 4, 0);
        ConfigureDropDown(_material);
        _otherMaterial.Dock = DockStyle.Fill;
        _otherMaterial.Visible = false;
        _otherMaterial.PlaceholderText = "اكتب اسم المادة";
        _material.Dock = DockStyle.Fill;
        _material.Items.AddRange(MaterialEnglish.Select(GetMaterialArabic).Cast<object>().ToArray());
        _material.SelectedIndex = 0;
        _material.SelectedIndexChanged += (_, _) => UpdateMaterialEditor();
        AddLabeled(grid, "المادة", _material, 6, 0);

        ConfigureDropDown(_firstLoadState);
        _firstLoadState.Items.AddRange(new object[] { "محمل", "فارغ" });
        _firstLoadState.SelectedIndex = 0;
        AddLabeled(grid, "From", _from, 0, 2);
        AddLabeled(grid, "To", _to, 4, 2);
        AddLabeled(grid, "حالة أول وزنة", _firstLoadState, 0, 3);
        ConfigureDropDown(_movementType);
        _movementType.Items.AddRange(new object[] { "داخلة للمعمل", "خارجة من المعمل", "مرتجع" });
        _movementType.SelectedIndex = 0;
        AddLabeled(grid, "حركة الحمولة", _movementType, 0, 1);
        _ticketHint.Text = "أدخل لوحة المركبة لعرض حالتها";
        _ticketHint.Dock = DockStyle.Fill;
        _ticketHint.TextAlign = ContentAlignment.MiddleCenter;
        grid.Controls.Add(_ticketHint, 2, 3);
        grid.SetColumnSpan(_ticketHint, 2);

        _firstWeigh.Text = "تسجيل الوزنة الأولى";
        _firstWeigh.Click += (_, _) => RecordFirstWeighing();
        _secondWeigh.Text = "تسجيل الوزنة الثانية";
        _secondWeigh.Click += (_, _) => RecordSecondWeighing();

        _notes.PlaceholderText = "ملاحظات اختيارية";
        AddLabeled(grid, "منفذ الوزن", _weighedBy, 0, 4);
        _openTicketButton.Text = "فتح تذكرة جديدة";
        _openTicketButton.Click += (_, _) => StartNewTicket();
        AddLabeled(grid, "ملاحظات", _notes, 0, 5);
        grid.SetColumnSpan(_notes, 3);
        _closeTicketButton.Text = "إغلاق التذكرة";
        _closeTicketButton.Click += (_, _) => CloseActiveTicket();
        _cancelTicket.Text = "إلغاء التذكرة المفتوحة";
        _cancelTicket.Click += (_, _) => CancelOpenTicket();
        _changeSimulatedWeight.Text = "بدّل الوزن التجريبي";
        _changeSimulatedWeight.Enabled = false;
        _changeSimulatedWeight.Click += (_, _) => ChangeSimulatedWeight();
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = new Padding(0) };
        for (var column = 0; column < 6; column++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6));
        var ticketActions = new[] { _firstWeigh, _secondWeigh, _openTicketButton, _closeTicketButton, _cancelTicket, _changeSimulatedWeight };
        for (var index = 0; index < ticketActions.Length; index++)
        {
            ticketActions[index].Dock = DockStyle.Fill;
            ticketActions[index].Margin = new Padding(5, 4, 5, 4);
            actions.Controls.Add(ticketActions[index], index, 0);
        }
        grid.Controls.Add(actions, 0, 6);
        grid.SetColumnSpan(actions, 8);

        _otherMaterialLabel.Text = "اسم المادة الأخرى";
        _otherMaterialLabel.Dock = DockStyle.Fill;
        _otherMaterialLabel.TextAlign = ContentAlignment.MiddleRight;
        _otherMaterialLabel.Visible = false;
        _otherMaterial.Dock = DockStyle.Fill;
        _otherMaterial.Visible = false;
        grid.Controls.Add(_otherMaterialLabel, 4, 1);
        grid.Controls.Add(_otherMaterial, 5, 1);
        grid.SetColumnSpan(_otherMaterial, 3);

        _plate.TextChanged += (_, _) => UpdateTicketHint();
        _firstLoadState.SelectedIndexChanged += (_, _) => UpdateTicketHint();
        _homeHistory.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) RestoreTicketFromRow(_homeHistory); };
        _history.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) RestoreTicketFromRow(_history); };
        group.Controls.Add(grid);
        return group;
    }

    private static void ConfigureDropDown(ComboBox combo)
    {
        combo.Dock = DockStyle.Fill;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
    }

    private static void AddLabeled(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var labelControl = new Label { Text = label, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, Margin = new Padding(3), Padding = new Padding(0), ForeColor = Color.FromArgb(60, 75, 90) };
        panel.Controls.Add(labelControl, column, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3);
        panel.Controls.Add(control, column + 1, row);
    }

    private void LoadSettings()
    {
        _settings = ScaleSettings.Load(_settingsPath);
        SelectValue(_language, _settings.Language);
        SelectValue(_printer, _settings.PrinterName);
        SelectValue(_ports, _settings.PortName);
        SelectValue(_baud, _settings.BaudRate);
        SelectValue(_parity, _settings.Parity);
        SelectValue(_dataBits, _settings.DataBits);
        SelectValue(_stopBits, _settings.StopBits);
        _settings.CommunicationMode = "MasterSlave";
        _scaleAddress.Value = Math.Clamp(_settings.ScaleAddress, (int)_scaleAddress.Minimum, (int)_scaleAddress.Maximum);
        _masterSlaveDecimals.Value = Math.Clamp(_settings.MasterSlaveDecimalPlaces, (int)_masterSlaveDecimals.Minimum, (int)_masterSlaveDecimals.Maximum);
        _configuredScaleAddress = (int)_scaleAddress.Value;
        _configuredMasterSlaveDecimals = (int)_masterSlaveDecimals.Value;
        if (!string.IsNullOrWhiteSpace(_settings.TicketTemplate))
        {
            if (!string.IsNullOrWhiteSpace(_settings.TicketTemplateRtf))
            {
                try { _ticketTemplate.Rtf = _settings.TicketTemplateRtf; }
                catch { _ticketTemplate.Text = _settings.TicketTemplate; }
            }
            else _ticketTemplate.Text = _settings.TicketTemplate;
            if (!_ticketTemplate.Text.Contains("{WeighedBy}", StringComparison.OrdinalIgnoreCase))
                _ticketTemplate.AppendText($"\r\nمنفذ الوزن: {{WeighedBy}}\r\n");
        }
        _logoPath.Text = _settings.LogoPath;
        if (File.Exists(_logoPath.Text) && !Path.GetFullPath(_logoPath.Text).StartsWith(Path.GetFullPath(_logoDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            SetLogo(_logoPath.Text);
        _weighedBy.Text = _settings.OperatorName;
        var paperIndex = _settings.PaperSizeName switch { "A4" => 1, "A5" => 2, "Letter" => 3, "Legal" => 4, "A3" => 5, "Receipt80" => 6, "Custom" => 7, _ => 0 };
        _paperSize.SelectedIndex = Math.Clamp(paperIndex, 0, _paperSize.Items.Count - 1);
        _paperWidth.Value = Math.Clamp(_settings.PaperWidthMm, (int)_paperWidth.Minimum, (int)_paperWidth.Maximum);
        _paperHeight.Value = Math.Clamp(_settings.PaperHeightMm, (int)_paperHeight.Minimum, (int)_paperHeight.Maximum);
        _pageOrientation.SelectedIndex = _settings.Landscape ? 1 : 0;
        _marginRight.Value = Math.Clamp(_settings.MarginRightMm, (int)_marginRight.Minimum, (int)_marginRight.Maximum);
        _marginLeft.Value = Math.Clamp(_settings.MarginLeftMm, (int)_marginLeft.Minimum, (int)_marginLeft.Maximum);
        _marginTop.Value = Math.Clamp(_settings.MarginTopMm, (int)_marginTop.Minimum, (int)_marginTop.Maximum);
        _marginBottom.Value = Math.Clamp(_settings.MarginBottomMm, (int)_marginBottom.Minimum, (int)_marginBottom.Maximum);
        UpdatePaperDimensionState();
        UpdateLogoPreview();
    }

    private static void SelectValue(ComboBox combo, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && combo.Items.Contains(value)) combo.SelectedItem = value;
        else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void SaveSettings()
    {
        _settings = new ScaleSettings
        {
            PortName = _ports.SelectedItem?.ToString() ?? "",
            BaudRate = _baud.SelectedItem?.ToString() ?? "9600",
            Parity = _parity.SelectedItem?.ToString() ?? nameof(Parity.None),
            DataBits = _dataBits.SelectedItem?.ToString() ?? "8",
            StopBits = _stopBits.SelectedItem?.ToString() ?? nameof(StopBits.One),
            PollCommand = "",
            PollIntervalMs = 1000,
            CommunicationMode = "MasterSlave",
            ScaleAddress = (int)_scaleAddress.Value,
            MasterSlaveDecimalPlaces = (int)_masterSlaveDecimals.Value,
            Language = _language.SelectedItem?.ToString() ?? "العربية",
            PrinterName = _printer.SelectedItem?.ToString() ?? "",
            TicketTemplate = _ticketTemplate.Text,
            TicketTemplateRtf = _ticketTemplate.Rtf ?? "",
            LogoPath = _logoPath.Text,
            PaperSizeName = _paperSize.SelectedIndex switch { 1 => "A4", 2 => "A5", 3 => "Letter", 4 => "Legal", 5 => "A3", 6 => "Receipt80", 7 => "Custom", _ => "Default" },
            OperatorName = _weighedBy.Text.Trim(),
            PaperWidthMm = (int)_paperWidth.Value,
            PaperHeightMm = (int)_paperHeight.Value,
            Landscape = _pageOrientation.SelectedIndex == 1,
            MarginRightMm = (int)_marginRight.Value,
            MarginLeftMm = (int)_marginLeft.Value,
            MarginTopMm = (int)_marginTop.Value,
            MarginBottomMm = (int)_marginBottom.Value
        };
        _configuredScaleAddress = _settings.ScaleAddress;
        _configuredMasterSlaveDecimals = _settings.MasterSlaveDecimalPlaces;
        _settings.Save(_settingsPath);
    }

    private void RefreshPorts()
    {
        var selected = _ports.SelectedItem?.ToString() ?? _settings.PortName;
        var detectedPorts = SerialPort.GetPortNames();
        _ports.Items.Clear();
        var portNames = Enumerable.Range(1, 32).Select(number => $"COM{number}")
            .Concat(detectedPorts)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => int.TryParse(name.AsSpan(3), out var number) ? number : int.MaxValue)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
        _ports.Items.AddRange(portNames);
        if (selected.Length > 0 && _ports.Items.Contains(selected)) _ports.SelectedItem = selected;
        else if (detectedPorts.Contains("COM1", StringComparer.OrdinalIgnoreCase)) _ports.SelectedItem = detectedPorts.First(port => string.Equals(port, "COM1", StringComparison.OrdinalIgnoreCase));
        else if (detectedPorts.Length == 1 && _ports.Items.Contains(detectedPorts[0])) _ports.SelectedItem = detectedPorts[0];
        else _ports.SelectedIndex = -1;
    }

    private void ToggleConnection()
    {
        if (_simulationEnabled) StopSimulation();
        if (_serial?.IsOpen == true) { Disconnect(); return; }
        ConnectToScale(showError: true);
    }

    private void ApplyTunaylarConnectionPreset()
    {
        // The Load Line 2 manual specifies 8 data bits and no parity, with
        // 9600 as the default baud rate. Continuous transmission needs no poll command.
        SelectValue(_baud, "9600");
        SelectValue(_parity, nameof(Parity.None));
        SelectValue(_dataBits, "8");
        SelectValue(_stopBits, nameof(StopBits.One));
        SaveSettings();
        if (_serial?.IsOpen == true) Disconnect();
        ConnectToScale(showError: false);
    }

    private void UpdateSerialDiagnostics()
    {
        _serialDiagnostics.Text = $"{T("عدد البايتات المستلمة")}: {Interlocked.Read(ref _serialBytesReceived)} | {T("آخر بايتات (HEX)")}: {_lastRxHex}";
        _lastCommandSentBox.Text = _lastTxHex;
    }

    private void TryConnectAutomatically()
    {
        if (_serial?.IsOpen == true) return;
        if (_ports.Items.Count == 0)
        {
            SetDisconnected(T("لم يتم العثور على منفذ COM"));
            return;
        }
        if (_ports.SelectedItem is null)
        {
            SetDisconnected(T("اختر منفذ COM من الإعدادات"));
            return;
        }

        _connection.Text = T("جاري الاتصال بالميزان");
        _connection.ForeColor = Color.DarkGoldenrod;
        ConnectToScale(showError: false);
    }

    private void ConnectToScale(bool showError)
    {
        RefreshPorts();
        if (_ports.SelectedItem is null)
        {
            var message = _ports.Items.Count == 0 ? T("لم يتم العثور على منفذ COM") : T("اختر منفذ COM من الإعدادات");
            SetDisconnected(message);
            if (showError) MessageBox.Show(this, message, T("منفذ غير متوفر"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var portName = _ports.SelectedItem.ToString()!;
        try
        {
            SaveSettings();
            _serial = new SerialPort(portName, int.Parse(_baud.Text, CultureInfo.InvariantCulture),
                Enum.Parse<Parity>(_parity.Text), int.Parse(_dataBits.Text, CultureInfo.InvariantCulture), Enum.Parse<StopBits>(_stopBits.Text))
            {
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = false,
                RtsEnable = false
            };
            _serial.DataReceived += SerialDataReceived;
            _serial.Open();
            _connect.Text = T("قطع الاتصال");
            _hasScaleReading = false;
            _masterSlaveProtocol.Reset();
            _manualTransmitProtocol.Reset();
            Interlocked.Exchange(ref _serialBytesReceived, 0);
            _lastRxHex = "—";
            _lastTxHex = "—";
            UpdateSerialDiagnostics();
            _connection.Text = $"{T("منفذ الميزان مفتوح، جاهز لطلب قراءة")}: {_serial.PortName}";
            _connection.ForeColor = Color.DarkGoldenrod;
        }
        catch (Exception ex)
        {
            Disconnect();
            var status = $"{T("تعذر الاتصال بالميزان")}: {portName}";
            SetDisconnected(status);
            if (showError)
                MessageBox.Show(this, $"{status}\n\n{ex.Message}", T("خطأ اتصال"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RequestWeighingCapture(PendingWeighingCapture capture)
    {
        if (_pendingWeighingCapture != PendingWeighingCapture.None)
        {
            MessageBox.Show(this, T("يوجد طلب قراءة قيد الانتظار"), T("انتظر رد الميزان"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_serial?.IsOpen != true)
        {
            MessageBox.Show(this, T("اتصل بالميزان أولاً لتسجيل الوزنة"), T("الميزان غير متصل"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveSettings();
            _masterSlaveProtocol.Reset();
            _manualTransmitProtocol.Reset();
            _pendingWeighingCapture = capture;
            _pendingTicketNumber = _activeTicketNumber;
            var request = TunaylarMasterSlaveProtocol.CreateRequest((byte)_scaleAddress.Value);
            _serial.Write(request, 0, request.Length);
            _lastTxHex = BitConverter.ToString(request);
            UpdateSerialDiagnostics();
            _connection.Text = $"{T("تم إرسال طلب قراءة، بانتظار الميزان")}: {_serial.PortName}";
            _connection.ForeColor = Color.DarkGoldenrod;
            _weighCaptureTimer.Stop();
            _weighCaptureTimer.Start();
        }
        catch (Exception ex)
        {
            _pendingWeighingCapture = PendingWeighingCapture.None;
            _pendingTicketNumber = null;
            MessageBox.Show(this, $"{T("تعذر إرسال طلب القراءة")}: {ex.Message}", T("خطأ اتصال"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CancelPendingWeighingCapture(bool showMessage)
    {
        _weighCaptureTimer.Stop();
        if (_pendingWeighingCapture == PendingWeighingCapture.None) return;
        _pendingWeighingCapture = PendingWeighingCapture.None;
        _pendingTicketNumber = null;
        if (_serial?.IsOpen == true)
        {
            _connection.Text = $"{T("لم يصل رد طلب القراءة")}: {_serial.PortName}";
            _connection.ForeColor = Color.Firebrick;
        }
        UpdateTicketHint();
        if (showMessage)
            MessageBox.Show(this, T("لم يصل رد من الميزان. تحقق من نمط Master-Slave وعنوان الميزان."), T("انتهت مهلة القراءة"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ToggleSimulation()
    {
        if (_simulationEnabled) { StopSimulation(); return; }
        if (_serial?.IsOpen == true) Disconnect();
        _simulationEnabled = true;
        _hasScaleReading = false;
        UpdateWeight(25000m);
        _connection.Text = T("وضع تجريبي — الوزن غير حقيقي");
        _connection.ForeColor = Color.DarkGoldenrod;
        _simulate.Text = "إيقاف التجربة";
        _changeSimulatedWeight.Text = "بدّل إلى 10,000 كغ";
        _changeSimulatedWeight.Enabled = true;
    }

    private void ChangeSimulatedWeight()
    {
        if (!_simulationEnabled) return;
        var nextWeight = _currentWeight == 25000m ? 10000m : 25000m;
        UpdateWeight(nextWeight);
        _changeSimulatedWeight.Text = nextWeight == 25000m ? "بدّل إلى 10,000 كغ" : "بدّل إلى 25,000 كغ";
    }

    private void StopSimulation()
    {
        _simulationEnabled = false;
        _simulate.Text = "وضع تجريبي";
        _changeSimulatedWeight.Enabled = false;
        if (_serial?.IsOpen != true)
        {
            _currentWeight = null;
            _connection.Text = T("غير متصل");
            _connection.ForeColor = Color.Firebrick;
            UpdateTicketHint();
        }
    }

    private void SerialDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port) return;
            var bytesToRead = port.BytesToRead;
            if (bytesToRead <= 0) return;
            var buffer = new byte[bytesToRead];
            var count = port.Read(buffer, 0, buffer.Length);
            if (count > 0)
            {
                Interlocked.Add(ref _serialBytesReceived, count);
                var hex = BitConverter.ToString(buffer, 0, count);
                if (hex.Length > 150) hex = "…" + hex[^150..];
                BeginInvoke(new Action(() =>
                {
                    _lastRxHex = hex;
                    UpdateSerialDiagnostics();
                }));
            }
            if (_pendingWeighingCapture != PendingWeighingCapture.None)
            {
                var requestedReadings = _masterSlaveProtocol.Push(buffer.AsSpan(0, count), (byte)_configuredScaleAddress, _configuredMasterSlaveDecimals);
                if (requestedReadings.Count > 0)
                {
                    foreach (var weight in requestedReadings)
                        BeginInvoke(new Action(() => HandleIncomingWeight(weight, _configuredMasterSlaveDecimals, null, responseToRequest: true)));
                }
                else
                {
                    foreach (var weight in _manualTransmitProtocol.Push(buffer.AsSpan(0, count)))
                        BeginInvoke(new Action(() => HandleIncomingWeight(weight, _configuredMasterSlaveDecimals, null, responseToRequest: true)));
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch (IOException) { BeginInvoke(new Action(() => SetDisconnected("انقطع الاتصال بالميزان"))); }
    }

    private void HandleIncomingWeight(decimal value, int decimalPlaces, TunaylarLoadLine2Reading? reading, bool responseToRequest = false)
    {
        UpdateWeight(value, reading, responseToRequest, decimalPlaces);
        if (_pendingWeighingCapture == PendingWeighingCapture.None) return;

        var capture = _pendingWeighingCapture;
        var ticketNumber = _pendingTicketNumber;
        _pendingWeighingCapture = PendingWeighingCapture.None;
        _pendingTicketNumber = null;
        _weighCaptureTimer.Stop();
        if (capture == PendingWeighingCapture.First) SaveFirstWeighing(value, ticketNumber);
        else SaveSecondWeighing(value, ticketNumber);
    }

    private void UpdateWeight(decimal value, TunaylarLoadLine2Reading? reading = null, bool responseToRequest = false, int? responseDecimalPlaces = null)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => UpdateWeight(value, reading, responseToRequest, responseDecimalPlaces))); return; }
        if (reading is not null && _serial?.IsOpen != true) return;
        _currentWeight = value;
        var decimalPlaces = reading?.DecimalPlaces ?? responseDecimalPlaces ?? 3;
        var weightFormat = $"N{decimalPlaces}";
        if ((reading is not null || responseToRequest) && _serial?.IsOpen == true)
        {
            _hasScaleReading = true;
            if (reading is not null)
            {
                var mode = reading.IsNet ? "صافي" : "الوزن المعروض";
                var presetTare = reading.HasPresetTare ? " — طرح تاري مفعّل" : "";
                _connection.Text = $"متصل: {_serial.PortName} | {mode} | تاري الميزان {reading.TareWeight.ToString(weightFormat, CultureInfo.CurrentCulture)} كغ{presetTare}";
            }
            else _connection.Text = $"{T("وصل رد قراءة من الميزان")}: {_serial.PortName}";
            _connection.ForeColor = Color.DarkGreen;
        }
        UpdateTicketHint();
    }

    private void Disconnect()
    {
        _weighCaptureTimer.Stop();
        _pendingWeighingCapture = PendingWeighingCapture.None;
        _pendingTicketNumber = null;
        try
        {
            if (_serial is not null)
            {
                _serial.DataReceived -= SerialDataReceived;
                if (_serial.IsOpen) _serial.Close();
                _serial.Dispose();
            }
        }
        catch { }
        _serial = null;
        _hasScaleReading = false;
        _masterSlaveProtocol.Reset();
        _manualTransmitProtocol.Reset();
        SetDisconnected(T("غير متصل"));
    }

    private void SetDisconnected(string message)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => SetDisconnected(message))); return; }
        _weighCaptureTimer.Stop();
        _pendingWeighingCapture = PendingWeighingCapture.None;
        _pendingTicketNumber = null;
        _connect.Text = T("اتصال");
        _connection.Text = message;
        _connection.ForeColor = Color.Firebrick;
        _hasScaleReading = false;
        _currentWeight = null;
        UpdateTicketHint();
    }

    private void StartNewTicket()
    {
        if (string.IsNullOrWhiteSpace(_plate.Text)) { MessageBox.Show(this, "أدخل لوحة المركبة أولاً.", "بيانات ناقصة"); _plate.Focus(); return; }
        var material = _material.SelectedIndex == MaterialEnglish.Length - 1 ? _otherMaterial.Text.Trim() : _material.SelectedIndex >= 0 ? MaterialEnglish[_material.SelectedIndex] : "";
        if (string.IsNullOrWhiteSpace(material)) { MessageBox.Show(this, T("اكتب اسم المادة الأخرى"), T("بيانات ناقصة")); _otherMaterial.Focus(); return; }
        try
        {
            _activeTicketNumber = _tickets.StartTicket(_plate.Text, _driver.Text, _customer.Text, material, _notes.Text, MovementCode(_movementType.SelectedIndex), _from.Text, _to.Text, _weighedBy.Text, _firstLoadState.SelectedIndex == 0);
            RefreshHistory(); UpdateTicketHint();
            MessageBox.Show(this, $"تم فتح التذكرة {_activeTicketNumber}. سجّل الوزنة الأولى عند جاهزية الميزان.", "تم فتح الوزنة");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر فتح الوزنة"); }
    }

    private void RecordFirstWeighing()
    {
        if (string.IsNullOrWhiteSpace(_activeTicketNumber)) { MessageBox.Show(this, "اضغط «فتح تذكرة جديدة» أولاً أو اختر تذكرة مفتوحة من السجل.", "لا توجد وزنة نشطة"); return; }
        if (_tickets.GetTicket(_activeTicketNumber)?.Status != "Draft") { MessageBox.Show(this, "هذه التذكرة تجاوزت مرحلة الوزنة الأولى.", "حالة التذكرة"); return; }
        if (IsMasterSlaveMode && !_simulationEnabled) { RequestWeighingCapture(PendingWeighingCapture.First); return; }
        if (_currentWeight is null) { MessageBox.Show(this, "ما في قراءة وزن حالية. اتصل بالميزان أو شغّل الوضع التجريبي.", "الوزن غير متوفر"); return; }
        SaveFirstWeighing(_currentWeight.Value);
    }

    private void SaveFirstWeighing(decimal weight, string? ticketNumber = null)
    {
        try
        {
            ticketNumber ??= _activeTicketNumber;
            if (string.IsNullOrWhiteSpace(ticketNumber)) return;
            _tickets.RecordFirstWeighing(ticketNumber, weight);
            RefreshHistory(); UpdateTicketHint();
            MessageBox.Show(this, $"تم حفظ الوزنة الأولى للتذكرة {ticketNumber}.\nالوزن: {weight:N3} كغ", "تم الحفظ");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر تسجيل الوزنة"); }
    }

    private void RecordSecondWeighing()
    {
        if (string.IsNullOrWhiteSpace(_activeTicketNumber)) { MessageBox.Show(this, "افتح تذكرة جديدة أو اختر تذكرة من السجل أولاً.", "لا توجد وزنة نشطة"); return; }
        if (_tickets.GetTicket(_activeTicketNumber)?.Status != "Open") { MessageBox.Show(this, "سجّل الوزنة الأولى قبل تسجيل الوزنة الثانية.", "حالة التذكرة"); return; }
        if (IsMasterSlaveMode && !_simulationEnabled) { RequestWeighingCapture(PendingWeighingCapture.Second); return; }
        if (_currentWeight is null) { MessageBox.Show(this, "ما في قراءة وزن حالية. اتصل بالميزان أو شغّل الوضع التجريبي.", "الوزن غير متوفر"); return; }
        SaveSecondWeighing(_currentWeight.Value);
    }

    private void SaveSecondWeighing(decimal weight, string? ticketNumber = null)
    {
        try
        {
            ticketNumber ??= _activeTicketNumber;
            if (string.IsNullOrWhiteSpace(ticketNumber)) return;
            var result = _tickets.RecordSecondWeighing(ticketNumber, weight);
            RefreshHistory();
            UpdateTicketHint();
            MessageBox.Show(this,
                $"تم تسجيل الوزنة الثانية.\nالقائم: {result.Gross:N3} كغ\nالفارغ: {result.Tare:N3} كغ\nالصافي: {result.Net:N3} كغ\nأغلق التذكرة بزر الإغلاق عند الانتهاء.",
                "الوزنتان جاهزتان");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر تسجيل الوزنة الثانية"); }
    }

    private void CloseActiveTicket()
    {
        if (string.IsNullOrWhiteSpace(_activeTicketNumber)) { MessageBox.Show(this, "ما في تذكرة نشطة لإغلاقها.", "لا توجد تذكرة نشطة"); return; }
        if (MessageBox.Show(this, $"إغلاق التذكرة {_activeTicketNumber}؟ يمكنك إعادتها بالنقر المزدوج عليها في السجل.", "تأكيد الإغلاق", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            _tickets.CloseTicket(_activeTicketNumber);
            _activeTicketNumber = null;
            RefreshHistory(); UpdateTicketHint();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر إغلاق التذكرة"); }
    }

    private void CancelOpenTicket()
    {
        if (string.IsNullOrWhiteSpace(_activeTicketNumber)) { MessageBox.Show(this, "ما في تذكرة نشطة لإلغائها.", "لا توجد تذكرة نشطة"); return; }
        if (MessageBox.Show(this, $"إلغاء التذكرة {_activeTicketNumber}؟", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { _tickets.CancelOpenTicket(_activeTicketNumber); _activeTicketNumber = null; RefreshHistory(); UpdateTicketHint(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر إلغاء التذكرة"); }
    }

    private void RestoreTicketFromRow(DataGridView grid)
    {
        if (grid.CurrentRow is null) return;
        var ticketColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.DataPropertyName == "رقم التذكرة");
        if (ticketColumn is null) return;
        var number = Convert.ToString(grid.CurrentRow.Cells[ticketColumn.Index].Value)?.Trim();
        if (string.IsNullOrWhiteSpace(number)) return;
        var details = _tickets.GetTicket(number);
        if (details is null) return;
        try
        {
            if (details.Status is "Closed" or "Complete")
            {
                if (MessageBox.Show(this, $"إعادة فتح التذكرة {number}؟", "إعادة فتح تذكرة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _tickets.ReopenTicket(number);
                details = _tickets.GetTicket(number)!;
            }
            if (details.Status == "Cancelled") { MessageBox.Show(this, "التذكرة ملغاة ولا يمكن استئنافها.", "تذكرة ملغاة"); return; }
            _activeTicketNumber = details.TicketNumber;
            _plate.Text = details.PlateNumber; _driver.Text = details.DriverName; _customer.Text = details.CustomerName;
            _notes.Text = details.Notes; _from.Text = details.FromParty; _to.Text = details.ToParty; _weighedBy.Text = details.WeighedBy;
            _movementType.SelectedIndex = details.MovementType switch { "Incoming" => 0, "Outgoing" => 1, "Return" => 2, _ => 0 };
            _firstLoadState.SelectedIndex = details.FirstWeightWasLoaded ? 0 : 1;
            var materialIndex = Array.IndexOf(MaterialEnglish, details.MaterialName);
            if (materialIndex < 0) { _material.SelectedIndex = MaterialEnglish.Length - 1; _otherMaterial.Text = details.MaterialName; }
            else { _material.SelectedIndex = materialIndex; _otherMaterial.Clear(); }
            RefreshHistory(); UpdateTicketHint();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تعذر استعادة التذكرة"); }
    }

    private void UpdateTicketHint()
    {
        var plate = _plate.Text.Trim();
        var active = string.IsNullOrWhiteSpace(plate) ? null : _tickets.GetActiveTicket(plate);
        if (active is not null) _activeTicketNumber = active.TicketNumber;
        else if (_activeTicketNumber is not null && _tickets.GetTicket(_activeTicketNumber)?.PlateNumber != plate) _activeTicketNumber = null;
        if (!string.IsNullOrWhiteSpace(_activeTicketNumber))
        {
            var selected = _tickets.GetTicket(_activeTicketNumber);
            if (selected is not null)
            {
                var state = selected.Status switch { "Draft" => "بانتظار الوزنة الأولى", "Open" => "بانتظار الوزنة الثانية", "ReadyToClose" => "جاهزة للإغلاق", _ => "تذكرة مفتوحة" };
                _ticketHint.Text = $"{T(state)}: {selected.TicketNumber}";
                _ticketHint.ForeColor = Color.DarkGreen;
                _openTicketButton.Enabled = false;
                _closeTicketButton.Enabled = selected.Status == "ReadyToClose";
                _cancelTicket.Enabled = true;
                _firstLoadState.Enabled = selected.Status == "Draft";
                return;
            }
        }
        _activeTicketNumber = null;
        _openTicketButton.Enabled = true; _closeTicketButton.Enabled = false; _cancelTicket.Enabled = false; _firstLoadState.Enabled = true;
        _ticketHint.Text = string.IsNullOrWhiteSpace(plate) ? T("أدخل لوحة المركبة لعرض حالتها") : _currentWeight is null
            ? T(_serial?.IsOpen == true ? "الميزان متصل، بانتظار قراءة وزن صالحة" : "لا توجد قراءة وزن حالية؛ اتصل بالميزان أولاً")
            : T("لا توجد تذكرة مفتوحة لهذه المركبة");
        _ticketHint.ForeColor = Color.DimGray;
    }

    private static string MovementCode(int index) => index switch { 0 => "Incoming", 1 => "Outgoing", 2 => "Return", _ => "Unspecified" };

    private void RefreshMaterialFilterOptions()
    {
        var selectedIndex = _filterMaterial.SelectedIndex;
        var previousKeys = _filterMaterial.Tag as string[];
        var selectedKey = selectedIndex > 0 && previousKeys is not null && selectedIndex < previousKeys.Length
            ? previousKeys[selectedIndex] : null;
        var materials = _tickets.LoadMaterials();
        _filterMaterial.Items.Clear();
        _filterMaterial.Items.Add(T("الكل"));
        var language = _language.SelectedItem?.ToString() ?? "العربية";
        foreach (var material in materials) _filterMaterial.Items.Add(GetMaterialName(material, language));
        var restoreIndex = selectedKey is null ? 0 : materials.IndexOf(selectedKey) + 1;
        _filterMaterial.SelectedIndex = restoreIndex > 0 ? restoreIndex : 0;
        _filterMaterial.Tag = new[] { "الكل" }.Concat(materials).ToArray();
    }

    private void RefreshHistory()
    {
        RefreshMaterialFilterOptions();
        var materialKeys = _filterMaterial.Tag as string[];
        var material = _filterMaterial.SelectedIndex > 0 && materialKeys is not null && _filterMaterial.SelectedIndex < materialKeys.Length
            ? materialKeys[_filterMaterial.SelectedIndex] : null;
        var movement = _filterMovement.SelectedIndex > 0 ? MovementCode(_filterMovement.SelectedIndex - 1) : null;
        var dateFrom = _filterDateFrom.Checked ? _filterDateFrom.Value.Date : (DateTime?)null;
        var dateTo = _filterDateTo.Checked ? _filterDateTo.Value.Date : (DateTime?)null;
        var table = _tickets.LoadHistory(_filterDriver.Text, _filterPlate.Text, material, movement, dateFrom, dateTo, int.MaxValue);
        UpdateReportTotals(table);
        _history.DataSource = _tickets.LoadHistory(_filterDriver.Text, _filterPlate.Text, material, movement, dateFrom, dateTo);
        var language = _language.SelectedItem?.ToString() ?? "العربية";
        TranslateHistory(_history, language);
        _homeHistory.DataSource = _tickets.LoadHistory(limit: 300);
        TranslateHistory(_homeHistory, language);
    }

    private void UpdateReportTotals(DataTable table)
    {
        decimal incoming = 0, outgoing = 0;
        foreach (DataRow row in table.Rows)
        {
            if (Convert.ToString(row["الحالة"]) is not ("مكتملة" or "مغلقة" or "جاهزة للإغلاق")) continue;
            if (!decimal.TryParse(Convert.ToString(row["الصافي (كغ)"], CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var net)) continue;
            var movement = Convert.ToString(row["حركة الحمولة"]);
            if (movement is "داخلة للمعمل" or "مرتجع") incoming += net;
            else if (movement == "خارجة من المعمل") outgoing += net;
        }
        var language = _language.SelectedItem?.ToString() ?? "العربية";
        _incomingTotal.Text = $"{T("الكمية الداخلة للمعمل")}: {incoming:N3} {T("كغ")}";
        _outgoingTotal.Text = $"{T("الكمية الخارجة من المعمل")}: {outgoing:N3} {T("كغ")}";
        _remainingTotal.Text = $"{T("الكمية المتبقية في المعمل")}: {(incoming - outgoing):N3} {T("كغ")}";
        _incomingTotal.RightToLeft = _outgoingTotal.RightToLeft = _remainingTotal.RightToLeft = language == "English" ? RightToLeft.No : RightToLeft.Yes;
    }

    private void ClearFilters()
    {
        _filterDriver.Clear();
        _filterPlate.Clear();
        _filterMaterial.SelectedIndex = 0;
        _filterMovement.SelectedIndex = 0;
        _filterDateFrom.Checked = false;
        _filterDateTo.Checked = false;
        RefreshHistory();
    }

    private DataTable LoadFilteredHistory(int limit = int.MaxValue)
    {
        var materialKeys = _filterMaterial.Tag as string[];
        var material = _filterMaterial.SelectedIndex > 0 && materialKeys is not null && _filterMaterial.SelectedIndex < materialKeys.Length
            ? materialKeys[_filterMaterial.SelectedIndex] : null;
        var movement = _filterMovement.SelectedIndex > 0 ? MovementCode(_filterMovement.SelectedIndex - 1) : null;
        return _tickets.LoadHistory(_filterDriver.Text, _filterPlate.Text, material, movement,
            _filterDateFrom.Checked ? _filterDateFrom.Value.Date : null,
            _filterDateTo.Checked ? _filterDateTo.Value.Date : null,
            limit);
    }

    private void PrintSelectedTicket() => PrintSelectedTicket(_history);

    private void PrintSelectedTicket(DataGridView grid)
    {
        if (grid.CurrentRow is null)
        {
            MessageBox.Show(this, T("حدد تذكرة من السجل أولاً"), T("لا توجد تذكرة محددة"));
            return;
        }
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewCell cell in grid.CurrentRow.Cells)
            values[cell.OwningColumn.DataPropertyName] = Convert.ToString(cell.Value, CultureInfo.CurrentCulture) ?? "";
        var placeholders = new Dictionary<string, string>
        {
            ["Serial"] = values.GetValueOrDefault("التسلسل", ""), ["TicketNumber"] = values.GetValueOrDefault("رقم التذكرة", ""),
            ["PlateNumber"] = values.GetValueOrDefault("لوحة المركبة", ""), ["DriverName"] = values.GetValueOrDefault("السائق", ""),
            ["CustomerName"] = values.GetValueOrDefault("العميل", ""), ["MaterialName"] = values.GetValueOrDefault("المادة", ""),
            ["From"] = values.GetValueOrDefault("From", ""), ["To"] = values.GetValueOrDefault("To", ""),
            ["WeighedBy"] = values.GetValueOrDefault("اسم منفذ الوزن", ""),
            ["MovementType"] = values.GetValueOrDefault("حركة الحمولة", ""), ["FirstWeight"] = values.GetValueOrDefault("الوزنة الأولى (كغ)", ""),
            ["SecondWeight"] = values.GetValueOrDefault("الوزنة الثانية (كغ)", ""), ["GrossWeight"] = values.GetValueOrDefault("القائم (كغ)", ""),
            ["TareWeight"] = values.GetValueOrDefault("الفارغ (كغ)", ""), ["NetWeight"] = values.GetValueOrDefault("الصافي (كغ)", ""),
            ["FirstWeighedAt"] = values.GetValueOrDefault("وقت الوزنة الأولى", ""), ["SecondWeighedAt"] = values.GetValueOrDefault("وقت الوزنة الثانية", ""),
            ["Status"] = values.GetValueOrDefault("الحالة", ""), ["Notes"] = values.GetValueOrDefault("ملاحظات", "")
        };
        PrintTicketTemplate(placeholders);
    }

    private void DeleteSelectedTicket(DataGridView grid)
    {
        if (grid.CurrentRow is null) { MessageBox.Show(this, T("حدد تذكرة من السجل أولاً"), T("لا توجد تذكرة محددة")); return; }
        var ticketColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.DataPropertyName == "رقم التذكرة");
        if (ticketColumn is null) return;
        var ticketNumber = Convert.ToString(grid.CurrentRow.Cells[ticketColumn.Index].Value)?.Trim();
        if (string.IsNullOrWhiteSpace(ticketNumber)) return;
        var details = _tickets.GetTicket(ticketNumber);
        if (details is null) { RefreshHistory(); return; }
        var prompt = string.Format(T("هل تريد حذف التذكرة {0} للمركبة {1}؟ هذا الحذف نهائي."), ticketNumber, details.PlateNumber);
        if (MessageBox.Show(this, prompt, T("تأكيد حذف التذكرة"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            _tickets.DeleteTicket(ticketNumber);
            if (string.Equals(_activeTicketNumber, ticketNumber, StringComparison.Ordinal)) _activeTicketNumber = null;
            RefreshHistory();
            UpdateTicketHint();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("تعذر حذف التذكرة"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void PrintFilteredReport() => PrintOperations(LoadFilteredHistory());

    private void PrintAllOperations() => PrintOperations(_tickets.LoadHistory(limit: int.MaxValue));

    private void PrintOperations(DataTable table)
    {
        var lines = new List<string> { T("تقرير عمليات الوزن"), $"{T("عدد العمليات")}: {table.Rows.Count}", "" };
        foreach (DataRow row in table.Rows)
        {
            var parts = new[] { "التسلسل", "رقم التذكرة", "لوحة المركبة", "السائق", "اسم منفذ الوزن", "المادة", "حركة الحمولة", "القائم (كغ)", "الصافي (كغ)", "وقت الوزنة الأولى", "الحالة" }
                .Where(table.Columns.Contains)
                .Select(column => $"{column}: {row[column]}");
            lines.Add(string.Join("    |    ", parts));
        }
        PrintLines(lines);
    }

    private void PrintLines(IReadOnlyList<string> lines)
    {
        SaveSettings();
        using var document = new PrintDocument();
        ConfigurePrintDocument(document);
        using var bodyFont = new Font("Segoe UI", 10F);
        using var titleFont = new Font("Segoe UI", 15F, FontStyle.Bold);
        var lineIndex = 0;
        document.PrintPage += (_, e) =>
        {
            var graphics = e.Graphics;
            if (graphics is null) return;
            var bounds = e.MarginBounds;
            var y = (float)bounds.Top;
            if (File.Exists(_logoPath.Text))
            {
                try
                {
                    using var logo = Image.FromFile(_logoPath.Text);
                    var scale = Math.Min(240f / logo.Width, 64f / logo.Height);
                    var width = logo.Width * scale;
                    var height = logo.Height * scale;
                    var x = bounds.Left + (bounds.Width - width) / 2f;
                    graphics.DrawImage(logo, x, y, width, height);
                    y += height + 12;
                }
                catch { }
            }
            using var format = new StringFormat(StringFormatFlags.DirectionRightToLeft) { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
            while (lineIndex < lines.Count)
            {
                var line = lines[lineIndex];
                var font = lineIndex == 0 ? titleFont : bodyFont;
                var size = graphics.MeasureString(line.Length == 0 ? " " : line, font, bounds.Width, format);
                if (y + size.Height > bounds.Bottom)
                {
                    e.HasMorePages = true;
                    return;
                }
                graphics.DrawString(line, font, Brushes.Black, new RectangleF(bounds.Left, y, bounds.Width, size.Height + 2), format);
                y += size.Height + (line.Length == 0 ? 8 : 4);
                lineIndex++;
            }
            e.HasMorePages = false;
            lineIndex = 0;
        };
        using var dialog = new PrintDialog { Document = document, UseEXDialog = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) document.Print();
    }

    private void ConfigurePrintDocument(PrintDocument document)
    {
        var printerName = _printer.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(printerName) && PrinterSettings.InstalledPrinters.Cast<string>().Contains(printerName))
            document.PrinterSettings.PrinterName = printerName;
        var requestedPaper = _paperSize.SelectedIndex switch
        {
            1 => PaperKind.A4, 2 => PaperKind.A5, 3 => PaperKind.Letter, 4 => PaperKind.Legal, 5 => PaperKind.A3, _ => PaperKind.Custom
        };
        if (_paperSize.SelectedIndex is >= 1 and <= 5)
        {
            var selectedPaper = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(paper => paper.Kind == requestedPaper);
            if (selectedPaper is not null) document.DefaultPageSettings.PaperSize = selectedPaper;
        }
        else if (_paperSize.SelectedIndex == 6)
        {
            var receipt = document.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(paper => paper.Width is >= 300 and <= 330);
            if (receipt is not null) document.DefaultPageSettings.PaperSize = receipt;
            else document.DefaultPageSettings.PaperSize = new PaperSize("80 mm receipt", 315, 1100);
        }
        else if (_paperSize.SelectedIndex == 7)
        {
            static int ToHundredthsOfAnInch(decimal millimeters) => (int)Math.Round((double)(millimeters / 25.4m * 100m));
            document.DefaultPageSettings.PaperSize = new PaperSize("Custom", ToHundredthsOfAnInch(_paperWidth.Value), ToHundredthsOfAnInch(_paperHeight.Value));
        }
        document.DefaultPageSettings.Landscape = _pageOrientation.SelectedIndex == 1;
        static int MmToHundredths(decimal mm) => (int)Math.Round((double)(mm / 25.4m * 100m));
        document.DefaultPageSettings.Margins = new Margins(MmToHundredths(_marginLeft.Value), MmToHundredths(_marginRight.Value),
            MmToHundredths(_marginTop.Value), MmToHundredths(_marginBottom.Value));
    }

    private string FormatTicketTemplate(IReadOnlyDictionary<string, string> values)
    {
        var template = string.IsNullOrWhiteSpace(_ticketTemplate.Text) ? DefaultTicketTemplate : _ticketTemplate.Text;
        foreach (var (key, value) in values) template = template.Replace("{" + key + "}", value, StringComparison.OrdinalIgnoreCase);
        return template;
    }

    private void PreviewTicketTemplate()
    {
        SaveSettings();
        var sample = new Dictionary<string, string>
        {
            ["Serial"]="1", ["TicketNumber"]="W-20261019-00001", ["PlateNumber"]="12-34567", ["DriverName"]="أحمد محمد",
            ["CustomerName"]="عميل تجريبي", ["MaterialName"]="Aluminum Cans", ["MovementType"]="داخلة للمعمل",
            ["FirstWeight"]="25,000.000", ["SecondWeight"]="10,000.000", ["GrossWeight"]="25,000.000", ["TareWeight"]="10,000.000",
            ["NetWeight"]="15,000.000", ["FirstWeighedAt"]="2026-10-19 10:30", ["SecondWeighedAt"]="2026-10-19 10:45", ["Status"]="مكتملة", ["Notes"]="", ["From"]="Supplier", ["To"]="Destination", ["WeighedBy"]="Operator name"
        };
        PrintTicketTemplate(sample);
    }

    private void PrintTicketTemplate(IReadOnlyDictionary<string, string> values)
    {
        SaveSettings();
        var lines = FormatTicketTemplate(values).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        using var printable = new RichTextBox { DetectUrls = false, RightToLeft = RightToLeft.Yes };
        printable.Rtf = BuildBorderlessTicketRtf(lines);

        using var document = new PrintDocument();
        ConfigurePrintDocument(document);
        var nextCharacter = 0;
        document.PrintPage += (_, e) =>
        {
            if (e.Graphics is null) return;
            var graphics = e.Graphics;
            var marginBounds = e.MarginBounds;
            var bodyTop = marginBounds.Top;
            if (nextCharacter == 0 && File.Exists(_logoPath.Text))
            {
                try
                {
                    using var logo = Image.FromFile(_logoPath.Text);
                    var scale = Math.Min(Math.Min(180f / logo.Width, 72f / logo.Height), marginBounds.Width / (float)logo.Width);
                    var width = logo.Width * scale;
                    var height = logo.Height * scale;
                    graphics.DrawImage(logo, marginBounds.Left, bodyTop, width, height);
                    bodyTop += (int)Math.Ceiling(height + 10);
                }
                catch { }
            }

            var hdc = graphics.GetHdc();
            try
            {
                var formatRange = new NativeFormatRange
                {
                    Hdc = hdc,
                    HdcTarget = hdc,
                    Area = new NativeRect
                    {
                        Left = PixelsToTwips(marginBounds.Left), Top = PixelsToTwips(bodyTop),
                        Right = PixelsToTwips(marginBounds.Right), Bottom = PixelsToTwips(marginBounds.Bottom)
                    },
                    Page = new NativeRect { Left = 0, Top = 0, Right = PixelsToTwips(e.PageBounds.Width), Bottom = PixelsToTwips(e.PageBounds.Height) },
                    Characters = new NativeCharacterRange { Minimum = nextCharacter, Maximum = -1 }
                };
                nextCharacter = SendMessage(printable.Handle, EmFormatRange, (IntPtr)1, ref formatRange).ToInt32();
                e.HasMorePages = nextCharacter < printable.TextLength;
            }
            finally
            {
                SendMessage(printable.Handle, EmFormatRange, IntPtr.Zero, IntPtr.Zero);
                graphics.ReleaseHdc(hdc);
            }
        };
        using var dialog = new PrintDialog { Document = document, UseEXDialog = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try { document.Print(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"تعذرت طباعة التذكرة. تحقق من اختيار الطابعة وحالتها.\n\n{ex.Message}",
                    T("خطأ"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static string BuildBorderlessTicketRtf(IEnumerable<string> lines)
    {
        var rtf = new System.Text.StringBuilder(@"{\rtf1\ansi\deff0\uc1\viewkind4\pard\sa100\fs20 ");
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) { rtf.Append(@"\pard\sa80\par "); continue; }
            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                rtf.Append(@"\pard\qc\b\fs28 ").Append(EscapeRtf(line)).Append(@"\b0\fs20\par ");
                continue;
            }

            const int labelColumn = 3600;
            const int fullWidth = 9000;
            rtf.Append(@"\trowd\trgaph90\trleft0\rtlrow\trkeep")
                .Append(@"\clvertalc\clbrdrl\brdrnil\clbrdrr\brdrnil\clbrdrt\brdrnil\clbrdrb\brdrnil\cellx").Append(labelColumn)
                .Append(@"\clvertalc\clbrdrl\brdrnil\clbrdrr\brdrnil\clbrdrt\brdrnil\clbrdrb\brdrnil\cellx").Append(fullWidth)
                .Append(@"\pard\intbl\rtlpar\qr\b ").Append(EscapeRtf(line[..colon].Trim())).Append(@":\b0\cell")
                .Append(@"\pard\intbl\rtlpar\qr ").Append(EscapeRtf(line[(colon + 1)..].Trim())).Append(@"\cell\row ");
        }
        rtf.Append('}');
        return rtf.ToString();
    }

    private static string EscapeRtf(string value)
    {
        var escaped = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\\' or '{' or '}') escaped.Append('\\').Append(character);
            else if (character <= 0x7f) escaped.Append(character);
            else escaped.Append(@"\u").Append(unchecked((short)character).ToString(CultureInfo.InvariantCulture)).Append('?');
        }
        return escaped.ToString();
    }

    private static int PixelsToTwips(int hundredthsOfAnInch) => (int)Math.Round(hundredthsOfAnInch * 14.4d);
    private const int EmFormatRange = 0x0439;
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeCharacterRange { public int Minimum, Maximum; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeFormatRange
    {
        public IntPtr Hdc, HdcTarget;
        public NativeRect Area, Page;
        public NativeCharacterRange Characters;
    }
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, ref NativeFormatRange lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    private void ExportFilteredReport() => ExportOperations(LoadFilteredHistory(), "Weighing-search-results");

    private void ExportAllOperations() => ExportOperations(_tickets.LoadHistory(limit: int.MaxValue), "All-weighing-operations");

    private void ExportOperations(DataTable table, string fileName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = T("تصدير إلى Excel"),
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"{fileName}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            AddExtension = true,
            DefaultExt = "xlsx",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            XlsxExporter.Save(table, dialog.FileName);
            MessageBox.Show(this, T("تم تصدير الملف بنجاح"), T("اكتمل التصدير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{T("تعذر تصدير الملف")}: {ex.Message}", T("خطأ في التصدير"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TranslateHistory(DataGridView grid, string language)
    {
        var headers = language == "English" ? HistoryHeadersEnglish : language == "کوردی" ? HistoryHeadersKurdish : null;
        if (headers is not null)
            foreach (DataGridViewColumn column in grid.Columns)
                if (headers.TryGetValue(column.DataPropertyName, out var value)) column.HeaderText = value;
                else column.HeaderText = column.DataPropertyName;
        else
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = column.DataPropertyName;
        var movementColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.DataPropertyName == "حركة الحمولة");
        if (movementColumn is not null)
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Cells[movementColumn.Index].Value is string value)
                    row.Cells[movementColumn.Index].Value = language switch
                {
                    "English" => value switch { "داخلة للمعمل" => "Incoming", "خارجة من المعمل" => "Outgoing", "مرتجع" => "Return", "غير محدد" => "Unspecified", _ => value },
                    "کوردی" => value switch { "داخلة للمعمل" => "داخلە بۆ کارگە", "خارجة من المعمل" => "دەرچوو لە کارگە", "مرتجع" => "گەڕاوە", "غير محدد" => "دیارینەکراو", _ => value },
                    _ => value
                };
                var statusColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.DataPropertyName == "الحالة");
                if (statusColumn is not null && row.Cells[statusColumn.Index].Value is string status)
                    row.Cells[statusColumn.Index].Value = language switch
                    {
                        "English" => status switch { "بانتظار الوزنة الأولى" => "Awaiting first weighing", "بانتظار الوزنة الثانية" => "Awaiting second weighing", "جاهزة للإغلاق" => "Ready to close", "مغلقة" => "Closed", "مفتوحة" => "Open", "ملغاة" => "Cancelled", "مكتملة" => "Complete", _ => status },
                        "کوردی" => status switch { "بانتظار الوزنة الأولى" => "چاوەڕێی کێشی یەکەم", "بانتظار الوزنة الثانية" => "چاوەڕێی کێشی دووەم", "جاهزة للإغلاق" => "ئامادەی داخستنە", "مغلقة" => "داخراوە", "مفتوحة" => "کراوە", "ملغاة" => "هەڵوەشاوە", "مكتملة" => "تەواو", _ => status },
                        _ => status
                    };
                var materialColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.DataPropertyName == "المادة");
                if (materialColumn is not null && row.Cells[materialColumn.Index].Value is string material)
                    row.Cells[materialColumn.Index].Value = GetMaterialName(material, language);
            }
    }

    private static readonly IReadOnlyDictionary<string, string> HistoryHeadersEnglish = new Dictionary<string, string>
    {
        ["التسلسل"]="Serial", ["رقم التذكرة"]="Ticket #", ["لوحة المركبة"]="Plate", ["السائق"]="Driver", ["العميل"]="Customer", ["المادة"]="Material", ["From"]="From", ["To"]="To", ["اسم منفذ الوزن"]="Weighed by", ["حركة الحمولة"]="Movement", ["الوزنة الأولى (كغ)"]="First weight (kg)", ["الوزنة الثانية (كغ)"]="Second weight (kg)", ["القائم (كغ)"]="Gross (kg)", ["الفارغ (كغ)"]="Tare (kg)", ["الصافي (كغ)"]="Net (kg)", ["وقت الوزنة الأولى"]="First weighed at", ["وقت الوزنة الثانية"]="Second weighed at", ["الحالة"]="Status", ["ملاحظات"]="Notes"
    };
    private static readonly IReadOnlyDictionary<string, string> HistoryHeadersKurdish = new Dictionary<string, string>
    {
        ["التسلسل"]="زنجیرە", ["رقم التذكرة"]="ژمارەی پسووڵە", ["لوحة المركبة"]="پلاک", ["السائق"]="شۆفێر", ["العميل"]="کڕیار", ["المادة"]="مادە", ["From"]="لە", ["To"]="بۆ", ["اسم منفذ الوزن"]="کێشیار", ["حركة الحمولة"]="جوڵەی بار", ["الوزنة الأولى (كغ)"]="کێشی یەکەم (کگ)", ["الوزنة الثانية (كغ)"]="کێشی دووەم (کگ)", ["القائم (كغ)"]="کێشی گشتی (کگ)", ["الفارغ (كغ)"]="بەتاڵ (کگ)", ["الصافي (كغ)"]="خاوێن (کگ)", ["وقت الوزنة الأولى"]="کاتی کێشی یەکەم", ["وقت الوزنة الثانية"]="کاتی کێشی دووەم", ["الحالة"]="دۆخ", ["ملاحظات"]="تێبینی"
    };

    private void MarkOriginalTexts(Control parent)
    {
        parent.Tag = parent is ComboBox combo ? combo.Items.Cast<object>().Select(item => item.ToString() ?? "").ToArray() : parent.Text;
        foreach (Control child in parent.Controls) MarkOriginalTexts(child);
    }

    private void ApplyLanguage()
    {
        if (_applyingLanguage) return;
        _applyingLanguage = true;
        try
        {
            var language = _language.SelectedItem?.ToString() ?? "العربية";
            var translations = language == "English" ? EnglishText : language == "کوردی" ? KurdishText : ArabicText;
            RightToLeft = language == "English" ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = language != "English";
            ApplyTranslations(this, translations);
            UpdateSerialDiagnostics();
            // TabPages live in TabControl.TabPages rather than Controls, so the
            // recursive control translator never visits their captions.
            homePage.Text = language switch { "English" => "Home", "کوردی" => "سەرەکی", _ => "الرئيسية" };
            reportsPage.Text = language switch { "English" => "Reports", "کوردی" => "ڕاپۆرتەکان", _ => "التقارير" };
            settingsPage.Text = language switch { "English" => "Settings", "کوردی" => "ڕێکخستنەکان", _ => "الإعدادات" };
            _readOnDemandHint.Text = language switch { "English" => "Weight is read only when recording a weighing", "کوردی" => "کێش تەنها کاتی تۆمارکردنی کێش دەخوێندرێتەوە", _ => "قراءة الوزن عند تسجيل الوزنة فقط" };
            _connect.Text = _serial?.IsOpen == true ? T("قطع الاتصال") : T("اتصال");
            if (_simulationEnabled)
            {
                _connection.Text = T("وضع تجريبي — الوزن غير الحقيقي");
                _connection.ForeColor = Color.DarkGoldenrod;
            }
            else if (_serial?.IsOpen == true)
            {
                if (_hasScaleReading)
                {
                    _connection.Text = $"{T("الميزان متصل ويستقبل البيانات")}: {_serial.PortName}";
                    _connection.ForeColor = Color.DarkGreen;
                }
                else
                {
                    _connection.Text = $"{T("منفذ الميزان مفتوح، جاهز لطلب قراءة")}: {_serial.PortName}";
                    _connection.ForeColor = Color.DarkGoldenrod;
                }
            }
            else
            {
                _connection.ForeColor = Color.Firebrick;
            }
            var materialIndex = _material.SelectedIndex;
            _material.Items.Clear();
            foreach (var material in MaterialEnglish)
                _material.Items.Add(GetMaterialName(material, language));
            _material.SelectedIndex = materialIndex < 0 ? 0 : materialIndex;
            _otherMaterial.PlaceholderText = language == "English" ? "Enter material name" : language == "کوردی" ? "ناوی مادەکە بنووسە" : "اكتب اسم المادة";
            UpdateMaterialEditor();
            Text = translations.GetValueOrDefault("نظام إدارة أوزان المركبات", "نظام إدارة أوزان المركبات");
            SaveSettings();
            RefreshHistory();
        }
        finally { _applyingLanguage = false; }
    }

    private string T(string arabic)
    {
        var language = _language.SelectedItem?.ToString() ?? "العربية";
        var translations = language == "English" ? EnglishText : language == "کوردی" ? KurdishText : ArabicText;
        return translations.GetValueOrDefault(arabic, arabic);
    }

    private static void ApplyTranslations(Control parent, IReadOnlyDictionary<string, string> translations)
    {
        if (parent is not ComboBox && parent.Tag is string original && original.Length > 0)
            parent.Text = translations.GetValueOrDefault(original, original);
        if (parent is ComboBox combo && parent.Tag is string[] originalItems)
        {
            var selected = combo.SelectedIndex;
            combo.Items.Clear();
            foreach (var item in originalItems)
            {
                combo.Items.Add(translations.GetValueOrDefault(item, item));
            }
            if (selected >= 0 && selected < combo.Items.Count) combo.SelectedIndex = selected;
        }
        foreach (Control child in parent.Controls) ApplyTranslations(child, translations);
    }

    private static IReadOnlyDictionary<string, string> ArabicText => EnglishText.Keys.Concat(KurdishText.Keys).Distinct().ToDictionary(key => key, key => key);
    private static readonly string[] MaterialEnglish = { "Aluminum Cans", "Aluminum Foil", "Aluminum Scrap Bales", "Baled Aluminum Wire Scrap", "Aluminum Wheel Scrap", "Free Aluminum Scrap", "Fuel", "Gasoil", "Others" };
    private static string GetMaterialArabic(string name) => GetMaterialName(name, "العربية");
    private static string GetMaterialName(string name, string language) => name switch
    {
        "Aluminum Cans" => language == "English" ? name : language == "کوردی" ? "قوطیی ئالیمنیۆم" : "علب ألمنيوم",
        "Aluminum Foil" => language == "English" ? name : language == "کوردی" ? "فۆیلی ئالیمنیۆم" : "رقائق ألمنيوم",
        "Aluminum Scrap Bales" => language == "English" ? name : language == "کوردی" ? "باڵەی پاشماوەی ئالیمنیۆم" : "بالات خردة ألمنيوم",
        "Baled Aluminum Wire Scrap" => language == "English" ? name : language == "کوردی" ? "پاشماوەی وایەری ئالیمنیۆم بەڵکراو" : "خردة أسلاك ألمنيوم مكبوسة",
        "Aluminum Wheel Scrap" => language == "English" ? name : language == "کوردی" ? "پاشماوەی جەنطی ئالیمنیۆم" : "خردة جنوط ألمنيوم",
        "Free Aluminum Scrap" => language == "English" ? name : language == "کوردی" ? "پاشماوەی ئازادی ئالیمنیۆم" : "خردة ألمنيوم سائبة",
        "Fuel" => language == "English" ? name : language == "کوردی" ? "سووتەمەنی" : "وقود",
        "Gasoil" => language == "English" ? name : language == "کوردی" ? "گازۆیل" : "مازوت",
        "Others" => language == "English" ? name : language == "کوردی" ? "هی تر" : "أخرى",
        _ => name
    };

    private void UpdateMaterialEditor()
    {
        var isOther = _material.SelectedIndex == MaterialEnglish.Length - 1;
        _material.Visible = true;
        _otherMaterial.Visible = isOther;
        _otherMaterial.Enabled = isOther;
        _otherMaterialLabel.Visible = isOther;
    }
    private static readonly IReadOnlyDictionary<string, string> EnglishText = new Dictionary<string, string>
    {
        ["آخر أمر مرسل للجهاز"] = "Last command sent to scale",
        ["يمين"]="Right", ["يسار"]="Left", ["أعلى"]="Top", ["أسفل"]="Bottom", ["طولي"]="Portrait", ["عرضي"]="Landscape", ["إدراج جدول 3×3"]="Insert 3×3 table", ["وسط"]="Center", ["عريض"]="Bold", ["اتجاه الصفحة"]="Page orientation", ["الهوامش (مم)"]="Margins (mm)",
        ["فتح تذكرة جديدة"]="Open new ticket", ["إغلاق التذكرة"]="Close ticket", ["حذف التذكرة المحددة"]="Delete selected ticket", ["تأكيد حذف التذكرة"]="Confirm ticket deletion", ["هل تريد حذف التذكرة {0} للمركبة {1}؟ هذا الحذف نهائي."]="Delete ticket {0} for vehicle {1}? This cannot be undone.", ["تعذر حذف التذكرة"]="Could not delete ticket", ["بانتظار الوزنة الأولى"]="Awaiting first weighing", ["بانتظار الوزنة الثانية"]="Awaiting second weighing", ["جاهزة للإغلاق"]="Ready to close", ["مغلقة"]="Closed",
        ["نظام إدارة أوزان المركبات"]="Vehicle Weighing Management System", ["الرئيسية"]="Home", ["Reports"]="Reports", ["اللغة"]="Language", ["ارتفاع القسم"]="Section height", ["الوزن الحالي من الميزان"]="Current scale weight", ["كغ"]="kg", ["غير متصل"]="Disconnected", ["جاري الاتصال بالميزان"]="Connecting to scale", ["اختر منفذ COM من الإعدادات"]="Choose a COM port in Settings", ["لم يتم العثور على منفذ COM"]="No COM port found", ["الميزان متصل"]="Scale connected", ["بانتظار قراءة الوزن"]="Waiting for a weight reading", ["يستقبل قراءة الوزن"]="Receiving weight data", ["تعذر الاتصال بالميزان"]="Could not connect to scale", ["منفذ غير متوفر"]="Port unavailable", ["خطأ اتصال"]="Connection error", ["تذكرة مفتوحة"]="Open ticket", ["الوزنة الثانية ستكون"]="Second weighing will be", ["أدخل لوحة المركبة لعرض حالتها"]="Enter plate number to see status", ["لا توجد تذكرة مفتوحة لهذه المركبة"]="No open ticket for this vehicle", ["الميزان متصل، بانتظار قراءة وزن صالحة"]="Scale connected; waiting for a valid weight reading", ["لا توجد قراءة وزن حالية؛ اتصل بالميزان أولاً"]="No current weight reading; connect to the scale first", ["From"]="From", ["To"]="To", ["منفذ الوزن"]="Weighed by", ["حجم الورقة"]="Paper size", ["العرض (مم)"]="Width (mm)", ["الارتفاع (مم)"]="Height (mm)", ["افتراضي"]="Printer default", ["إيصال 80 مم"]="80 mm receipt", ["مخصص"]="Custom", ["اختيار صورة الشعار"]="Choose logo image", ["شعار التذكرة"]="Ticket logo", ["إزالة"]="Remove", ["الكمية الداخلة للمعمل"]="Incoming quantity", ["الكمية الخارجة من المعمل"]="Outgoing quantity", ["الكمية المتبقية في المعمل"]="Remaining in factory",
        ["تم فتح المنفذ، بانتظار استجابة الميزان"]="Serial port open; waiting for scale response", ["لم تصل بيانات من الميزان"]="No data received from the scale", ["الميزان متصل ويستقبل البيانات"]="Scale connected and receiving data",
        ["اتصال الميزان وإعدادات RS-232"]="Scale connection and RS-232 settings", ["إعدادات Tunaylar: 9600 / 8-N-1"]="Tunaylar settings: 9600 / 8-N-1", ["تحديث منافذ COM"]="Refresh COM ports", ["عدد البايتات المستلمة"]="Bytes received", ["آخر بايتات (HEX)"]="Last bytes (HEX)", ["منفذ COM"]="COM port", ["السرعة"]="Baud rate", ["أمر القراءة"]="Read command", ["نمط الاتصال"]="Communication mode", ["قراءة مستمرة"]="Continuous transmission", ["طلب عند الوزنة (Master-Slave)"]="Request on each weighing (Master-Slave)", ["عنوان الميزان"]="Scale address", ["خانات الكسور"]="Decimal places", ["منفذ الميزان مفتوح، جاهز لطلب قراءة"]="Scale port open; ready for a read request", ["يوجد طلب قراءة قيد الانتظار"]="A read request is already pending", ["انتظر رد الميزان"]="Waiting for scale response", ["اتصل بالميزان أولاً لتسجيل الوزنة"]="Connect to the scale before recording a weighing", ["الميزان غير متصل"]="Scale is disconnected", ["تم إرسال طلب قراءة، بانتظار الميزان"]="Read request sent; waiting for the scale", ["وصل رد قراءة من الميزان"]="Received a weighing response", ["تعذر إرسال طلب القراءة"]="Could not send the read request", ["لم يصل رد طلب القراءة"]="No response to the read request", ["لم يصل رد من الميزان. تحقق من نمط Master-Slave وعنوان الميزان."]="No scale response. Check Master-Slave mode and the scale address.", ["انتهت مهلة القراءة"]="Read timed out", ["الفاصل (ms)"]="Interval (ms)", ["اتصال"]="Connect", ["قطع الاتصال"]="Disconnect", ["وضع تجريبي"]="Simulation mode", ["إيقاف التجربة"]="Stop simulation",
        ["بيانات المركبة والتذكرة"]="Vehicle and ticket details", ["لوحة المركبة"]="Plate number", ["السائق"]="Driver", ["العميل / الجهة"]="Customer / Company", ["المادة"]="Material", ["اسم المادة الأخرى"]="Other material name", ["حالة أول وزنة"]="First weighing state", ["حركة الحمولة"]="Load movement", ["داخلة للمعمل"]="Incoming to factory", ["خارجة من المعمل"]="Outgoing from factory", ["مرتجع"]="Returned", ["محمل"]="Loaded", ["فارغ"]="Empty", ["تسجيل الوزنة الأولى"]="Record first weighing", ["تسجيل الوزنة الثانية"]="Record second weighing", ["ملاحظات"]="Notes", ["إلغاء التذكرة المفتوحة"]="Cancel open ticket", ["بدّل الوزن التجريبي"]="Change simulated weight", ["سجل التذاكر (آخر 300 تذكرة)"]="Ticket history (last 300 tickets)", ["سجل الأوزان الأخير"]="Recent weighings", ["—  كغ"]="—  kg", ["خيارات البحث"]="Search filters", ["الحركة"]="Movement", ["الكل"]="All", ["من تاريخ"]="From date", ["إلى تاريخ"]="To date", ["بحث"]="Search", ["مسح الفلاتر"]="Clear filters", ["سجل التذاكر — آخر 300 نتيجة"]="Ticket records — latest 300", ["إعدادات الطباعة ونموذج التذكرة"]="Print settings and ticket template", ["الطابعة"]="Printer", ["تحديث الطابعات"]="Refresh printers", ["قالب التذكرة — عدّل النص والحقول بين الأقواس كما تريد"]="Ticket template — edit the text and placeholders as needed", ["حفظ إعدادات الطباعة"]="Save print settings", ["تجربة نموذج الطباعة"]="Test ticket template", ["تم حفظ إعدادات الطباعة"]="Print settings saved", ["تم الحفظ"]="Saved", ["Settings"]="Settings", ["بيانات ناقصة"]="Missing information", ["اكتب اسم المادة الأخرى"]="Enter the other material name", ["طباعة التذكرة المحددة"]="Print selected ticket", ["طباعة نتائج البحث"]="Print search results", ["تصدير النتائج إلى Excel"]="Export results to Excel", ["طباعة كل عمليات الوزن"]="Print all weighings", ["تصدير كل العمليات إلى Excel"]="Export all operations to Excel", ["تذكرة وزن مركبة"]="Vehicle weighing ticket", ["حدد تذكرة من السجل أولاً"]="Select a ticket from the report first", ["لا توجد تذكرة محددة"]="No ticket selected", ["تقرير عمليات الوزن"]="Weighing operations report", ["عدد العمليات"]="Operations", ["تصدير إلى Excel"]="Export to Excel", ["تم تصدير الملف بنجاح"]="File exported successfully", ["اكتمل التصدير"]="Export complete", ["تعذر تصدير الملف"]="Could not export file", ["خطأ في التصدير"]="Export error"
    };
    private static readonly IReadOnlyDictionary<string, string> KurdishText = new Dictionary<string, string>
    {
        ["آخر أمر مرسل للجهاز"] = "دوا فەرمانی نێردراو بۆ تەرازوو",
        ["يمين"]="ڕاست", ["يسار"]="چەپ", ["أعلى"]="سەرەوە", ["أسفل"]="خوارەوە", ["طولي"]="ستوونی", ["عرضي"]="ئاسۆیی", ["إدراج جدول 3×3"]="خشتەی ٣×٣ زیاد بکە", ["وسط"]="ناوەڕاست", ["عريض"]="قەڵەو", ["اتجاه الصفحة"]="ئاراستەی پەڕە", ["الهوامش (مم)"]="حاشیە (ملم)",
        ["فتح تذكرة جديدة"]="کردنەوەی پسووڵەیەکی نوێ", ["إغلاق التذكرة"]="داخستنی پسووڵە", ["حذف التذكرة المحددة"]="سڕینەوەی پسووڵەی دیاریکراو", ["تأكيد حذف التذكرة"]="پشتڕاستکردنەوەی سڕینەوەی پسووڵە", ["هل تريد حذف التذكرة {0} للمركبة {1}؟ هذا الحذف نهائي."]="دڵنیایت لە سڕینەوەی پسووڵەی {0} بۆ ئۆتۆمبێلی {1}؟ ئەم سڕینەوەیە ناگەڕێتەوە.", ["تعذر حذف التذكرة"]="نەتوانرا پسووڵەکە بسڕدرێتەوە", ["بانتظار الوزنة الأولى"]="چاوەڕێی کێشی یەکەم", ["بانتظار الوزنة الثانية"]="چاوەڕێی کێشی دووەم", ["جاهزة للإغلاق"]="ئامادەی داخستنە", ["مغلقة"]="داخراوە",
        ["نظام إدارة أوزان المركبات"]="سیستەمی بەڕێوەبردنی کێشی ئۆتۆمبێل", ["الرئيسية"]="سەرەکی", ["Reports"]="ڕاپۆرتەکان", ["اللغة"]="زمان", ["ارتفاع القسم"]="بەرزی بەش", ["الوزن الحالي من الميزان"]="کێشی ئێستای تەرازوو", ["كغ"]="کگ", ["غير متصل"]="پەیوەست نییە", ["جاري الاتصال بالميزان"]="پەیوەندی بە تەرازووەوە دەکرێت", ["اختر منفذ COM من الإعدادات"]="لە ڕێکخستنەکان دەرچەیەکی COM هەڵبژێرە", ["لم يتم العثور على منفذ COM"]="هیچ دەرچەی COM نەدۆزرایەوە", ["الميزان متصل"]="تەرازوو پەیوەستە", ["بانتظار قراءة الوزن"]="چاوەڕێی خوێندنەوەی کێشە", ["يستقبل قراءة الوزن"]="داتای کێش وەردەگرێت", ["تعذر الاتصال بالميزان"]="نەتوانرا پەیوەندی بە تەرازووەوە بکرێت", ["منفذ غير متوفر"]="دەرچە بەردەست نییە", ["خطأ اتصال"]="هەڵەی پەیوەندی", ["تذكرة مفتوحة"]="پسووڵەی کراوە", ["الوزنة الثانية ستكون"]="کێشی دووەم دەبێت", ["أدخل لوحة المركبة لعرض حالتها"]="ژمارەی پلاک بنووسە بۆ بینینی دۆخەکە", ["لا توجد تذكرة مفتوحة لهذه المركبة"]="هیچ پسووڵەیەکی کراوە بۆ ئەم ئۆتۆمبێلە نییە", ["الميزان متصل، بانتظار قراءة وزن صالحة"]="تەرازوو پەیوەستە؛ چاوەڕێی خوێندنەوەی کێشی دروستە", ["لا توجد قراءة وزن حالية؛ اتصل بالميزان أولاً"]="هیچ خوێندنەوەیەکی کێشی ئێستا نییە؛ سەرەتا تەرازوو پەیوەست بکە", ["From"]="لە", ["To"]="بۆ", ["منفذ الوزن"]="کێشیار", ["حجم الورقة"]="قەبارەی کاغەز", ["العرض (مم)"]="پانی (ملم)", ["الارتفاع (مم)"]="بەرزی (ملم)", ["افتراضي"]="بنەڕەتی چاپکەر", ["إيصال 80 مم"]="پسووڵەی ٨٠ ملم", ["مخصص"]="تایبەت", ["اختيار صورة الشعار"]="هەڵبژاردنی وێنەی لۆگۆ", ["شعار التذكرة"]="لۆگۆی پسووڵە", ["إزالة"]="لابردن", ["الكمية الداخلة للمعمل"]="بڕی هاتوو بۆ کارگە", ["الكمية الخارجة من المعمل"]="بڕی چووەدەرەوە لە کارگە", ["الكمية المتبقية في المعمل"]="بڕی ماوە لە کارگە",
        ["تم فتح المنفذ، بانتظار استجابة الميزان"]="دەرچە کراوەتەوە؛ چاوەڕێی وەڵامی تەرازوو", ["لم تصل بيانات من الميزان"]="هیچ داتایەک لە تەرازووەوە نەگەیشتووە", ["الميزان متصل ويستقبل البيانات"]="تەرازوو پەیوەستە و داتا وەردەگرێت",
        ["اتصال الميزان وإعدادات RS-232"]="پەیوەندی تەرازوو و ڕێکخستنەکانی RS-232", ["إعدادات Tunaylar: 9600 / 8-N-1"]="ڕێکخستنەکانی Tunaylar: 9600 / 8-N-1", ["تحديث منافذ COM"]="نوێکردنەوەی دەرچەکانی COM", ["عدد البايتات المستلمة"]="ژمارەی بایتە وەرگیراوەکان", ["آخر بايتات (HEX)"]="دوا بایتەکان (HEX)", ["منفذ COM"]="دەرچەی COM", ["السرعة"]="خێرایی", ["أمر القراءة"]="فەرمانی خوێندنەوە", ["نمط الاتصال"]="شێوازی پەیوەندی", ["قراءة مستمرة"]="خوێندنەوەی بەردەوام", ["طلب عند الوزنة (Master-Slave)"]="داواکاری لە هەر کێشێکدا (Master-Slave)", ["عنوان الميزان"]="ناونیشانی تەرازوو", ["خانات الكسور"]="ژمارەی خانەی دەهەمی", ["منفذ الميزان مفتوح، جاهز لطلب قراءة"]="دەرچەی تەرازوو کراوەتەوە؛ ئامادەی داواکارییەکە", ["يوجد طلب قراءة قيد الانتظار"]="داواکارییەکی خوێندنەوە چاوەڕوانە", ["انتظر رد الميزان"]="چاوەڕێی وەڵامی تەرازوو", ["اتصل بالميزان أولاً لتسجيل الوزنة"]="بۆ تۆمارکردنی کێش سەرەتا تەرازوو پەیوەست بکە", ["الميزان غير متصل"]="تەرازوو پەیوەست نییە", ["تم إرسال طلب قراءة، بانتظار الميزان"]="داواکاری خوێندنەوە نێردرا؛ چاوەڕێی تەرازوو", ["وصل رد قراءة من الميزان"]="وەڵامی خوێندنەوە لە تەرازوو گەیشت", ["تعذر إرسال طلب القراءة"]="نەتوانرا داواکاری خوێندنەوە بنێردرێت", ["لم يصل رد طلب القراءة"]="وەڵامی داواکاری خوێندنەوە نەگەیشت", ["لم يصل رد من الميزان. تحقق من نمط Master-Slave وعنوان الميزان."]="وەڵام لە تەرازوو نەگەیشت؛ شێوازی Master-Slave و ناونیشانی تەرازوو بپشکنە.", ["انتهت مهلة القراءة"]="ماوەی چاوەڕوانی خوێندنەوە تەواو بوو", ["الفاصل (ms)"]="ماوە (ms)", ["اتصال"]="پەیوەستن", ["قطع الاتصال"]="بڕینی پەیوەندی", ["وضع تجريبي"]="دۆخی تاقیکردنەوە", ["إيقاف التجربة"]="وەستاندنی تاقیکردنەوە",
        ["بيانات المركبة والتذكرة"]="زانیاری ئۆتۆمبێل و پسووڵە", ["لوحة المركبة"]="ژمارەی پلاک", ["السائق"]="شۆفێر", ["العميل / الجهة"]="کڕیار / لایەن", ["المادة"]="مادە", ["اسم المادة الأخرى"]="ناوی مادەی تر", ["حالة أول وزنة"]="دۆخی کێشی یەکەم", ["حركة الحمولة"]="جوڵەی بار", ["داخلة للمعمل"]="داخلە بۆ کارگە", ["خارجة من المعمل"]="دەرچوو لە کارگە", ["مرتجع"]="گەڕاوە", ["محمل"]="بارکراو", ["فارغ"]="بەتاڵ", ["تسجيل الوزنة الأولى"]="تۆمارکردنی کێشی یەکەم", ["تسجيل الوزنة الثانية"]="تۆمارکردنی کێشی دووەم", ["ملاحظات"]="تێبینی", ["إلغاء التذكرة المفتوحة"]="هەڵوەشاندنەوەی پسووڵەی کراوە", ["بدّل الوزن التجريبي"]="گۆڕینی کێشی تاقیکردنەوە", ["سجل التذاكر (آخر 300 تذكرة)"]="مێژووی پسووڵەکان (دوا ٣٠٠)", ["سجل الأوزان الأخير"]="دوا کێشەکان", ["—  كغ"]="—  کگ", ["خيارات البحث"]="هەڵبژاردەکانی گەڕان", ["الحركة"]="جوڵە", ["الكل"]="هەموو", ["من تاريخ"]="لە بەرواری", ["إلى تاريخ"]="تا بەرواری", ["بحث"]="گەڕان", ["مسح الفلاتر"]="پاککردنەوەی فلتەرەکان", ["سجل التذاكر — آخر 300 نتيجة"]="تۆمارەکان — دوا ٣٠٠", ["إعدادات الطباعة ونموذج التذكرة"]="ڕێکخستنەکانی چاپ و نموونەی پسووڵە", ["الطابعة"]="چاپکەر", ["تحديث الطابعات"]="نوێکردنەوەی چاپکەرەکان", ["قالب التذكرة — عدّل النص والحقول بين الأقواس كما تريد"]="نموونەی پسووڵە — دەق و خانەکان بگۆڕە", ["حفظ إعدادات الطباعة"]="پاشەکەوتکردنی ڕێکخستنەکانی چاپ", ["تجربة نموذج الطباعة"]="تاقیکردنەوەی نموونەی چاپ", ["تم حفظ إعدادات الطباعة"]="ڕێکخستنەکانی چاپ پاشەکەوت کران", ["تم الحفظ"]="پاشەکەوت کرا", ["Settings"]="Settings", ["بيانات ناقصة"]="زانیاری تەواو نییە", ["اكتب اسم المادة الأخرى"]="ناوی مادەی تر بنووسە", ["طباعة التذكرة المحددة"]="چاپکردنی پسووڵەی دیاریکراو", ["طباعة نتائج البحث"]="چاپکردنی ئەنجامی گەڕان", ["تصدير النتائج إلى Excel"]="هەناردەکردنی ئەنجامەکان بۆ Excel", ["طباعة كل عمليات الوزن"]="چاپکردنی هەموو کێشەکان", ["تصدير كل العمليات إلى Excel"]="هەناردەکردنی هەموو کردارەکان بۆ Excel", ["تذكرة وزن مركبة"]="پسووڵەی کێشی ئۆتۆمبێل", ["حدد تذكرة من السجل أولاً"]="سەرەتا پسووڵەیەک لە تۆمارەکان دیاری بکە", ["لا توجد تذكرة محددة"]="هیچ پسووڵەیەک دیاری نەکراوە", ["تقرير عمليات الوزن"]="ڕاپۆرتی کردارەکانی کێش", ["عدد العمليات"]="ژمارەی کردارەکان", ["تصدير إلى Excel"]="هەناردە بۆ Excel", ["تم تصدير الملف بنجاح"]="فایلەکە بە سەرکەوتوویی هەناردە کرا", ["اكتمل التصدير"]="هەناردە تەواو بوو", ["تعذر تصدير الملف"]="نەتوانرا فایلەکە هەناردە بکرێت", ["خطأ في التصدير"]="هەڵەی هەناردەکردن"
    };
}


