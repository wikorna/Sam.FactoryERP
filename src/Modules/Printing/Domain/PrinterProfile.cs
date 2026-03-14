namespace Printing.Domain;

public class PrinterProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Dpi { get; set; }
    public int LabelWidthMm { get; set; }
    public int LabelHeightMm { get; set; }
}

