using System.Text.Json;

namespace WeighingDesk;

internal sealed class ScaleSettings
{
    public string PortName { get; set; } = "";
    public string BaudRate { get; set; } = "9600";
    public string Parity { get; set; } = "None";
    public string DataBits { get; set; } = "8";
    public string StopBits { get; set; } = "One";
    public string PollCommand { get; set; } = "";
    public int PollIntervalMs { get; set; } = 1000;
    public string CommunicationMode { get; set; } = "Continuous";
    public int ScaleAddress { get; set; } = 1;
    public int MasterSlaveDecimalPlaces { get; set; }
    public string Language { get; set; } = "العربية";
    public string PrinterName { get; set; } = "";
    public string TicketTemplate { get; set; } = "";
    public string TicketTemplateRtf { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string PaperSizeName { get; set; } = "Default";
    public string OperatorName { get; set; } = "";
    public int PaperWidthMm { get; set; } = 210;
    public int PaperHeightMm { get; set; } = 297;
    public bool Landscape { get; set; }
    public int MarginRightMm { get; set; } = 12;
    public int MarginLeftMm { get; set; } = 12;
    public int MarginTopMm { get; set; } = 12;
    public int MarginBottomMm { get; set; } = 12;

    public static ScaleSettings Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<ScaleSettings>(File.ReadAllText(path)) ?? new ScaleSettings();
        }
        catch { }
        return new ScaleSettings();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
