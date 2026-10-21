namespace WeighingDesk;

partial class Form1
{
    private TabControl mainTabs;
    private TabPage homePage;
    private TabPage reportsPage;
    private TabPage settingsPage;
    private TableLayoutPanel homeLayout;
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        AutoScaleMode = AutoScaleMode.Font;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(1500, 1120);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(244, 247, 250);
        Text = "نظام إدارة أوزان المركبات";
        mainTabs = new TabControl { Name = "mainTabs", Dock = DockStyle.Fill };
        homePage = new TabPage("الرئيسية") { Name = "homePage", BackColor = BackColor, Padding = new Padding(0) };
        reportsPage = new TabPage("Reports") { Name = "reportsPage", BackColor = BackColor, Padding = new Padding(12) };
        settingsPage = new TabPage("Settings") { Name = "settingsPage", BackColor = BackColor, Padding = new Padding(12) };
        mainTabs.Controls.Add(homePage);
        mainTabs.Controls.Add(reportsPage);
        mainTabs.Controls.Add(settingsPage);
        homeLayout = new TableLayoutPanel
        {
            Name = "homeLayout", Dock = DockStyle.Fill, Padding = new Padding(18),
            ColumnCount = 1, RowCount = 2
        };
        homeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 500F));
        homeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        homePage.Controls.Add(homeLayout);
        Controls.Add(mainTabs);

    }

    #endregion
}
