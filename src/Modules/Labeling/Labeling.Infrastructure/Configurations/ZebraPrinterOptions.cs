namespace Labeling.Infrastructure.Configurations;

public class ZebraPrinterOptions
{
    public const string SectionName = "ZebraPrinters";
    public Dictionary<string, PrinterConfig> Printers { get; set; } = new();
}

public class PrinterConfig
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 9100;
    public string Department { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
}
