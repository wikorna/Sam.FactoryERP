using System.Globalization;
using System.Text;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Strategies;

/// <summary>
/// Renders ZPL labels from templates that use <c>{{Placeholder}}</c> token syntax.
/// Handles all templates with <c>Version = "v1"</c> (or any unrecognised version as a
/// safe default), making it the fallback strategy in the selector.
/// </summary>
/// <remarks>
/// <para><b>Supported placeholders:</b></para>
/// <list type="table">
///   <listheader><term>Token</term><description>Source field</description></listheader>
///   <item><term><c>{{CustomerCode}}</c></term><description><see cref="ShipmentItemLabelData.CustomerCode"/></description></item>
///   <item><term><c>{{PartNo}}</c></term><description><see cref="ShipmentItemLabelData.PartNo"/></description></item>
///   <item><term><c>{{ProductName}}</c></term><description><see cref="ShipmentItemLabelData.ProductName"/></description></item>
///   <item><term><c>{{Description}}</c></term><description><see cref="ShipmentItemLabelData.Description"/></description></item>
///   <item><term><c>{{Quantity}}</c></term><description><see cref="ShipmentItemLabelData.Quantity"/></description></item>
///   <item><term><c>{{PoNumber}}</c></term><description><see cref="ShipmentItemLabelData.PoNumber"/></description></item>
///   <item><term><c>{{PoItem}}</c></term><description><see cref="ShipmentItemLabelData.PoItem"/></description></item>
///   <item><term><c>{{DueDate}}</c></term><description><see cref="ShipmentItemLabelData.DueDate"/></description></item>
///   <item><term><c>{{RunNo}}</c></term><description><see cref="ShipmentItemLabelData.RunNo"/></description></item>
///   <item><term><c>{{Store}}</c></term><description><see cref="ShipmentItemLabelData.Store"/></description></item>
///   <item><term><c>{{Remarks}}</c></term><description><see cref="ShipmentItemLabelData.Remarks"/></description></item>
///   <item><term><c>{{BatchNumber}}</c></term><description><see cref="ShipmentItemLabelData.BatchNumber"/></description></item>
///   <item><term><c>{{LineNumber}}</c></term><description><see cref="ShipmentItemLabelData.LineNumber"/></description></item>
///   <item><term><c>{{QrPayload}}</c></term><description><see cref="QrPayloadData.Payload"/></description></item>
/// </list>
/// <para>
/// ZPL special characters (caret <c>^</c> and tilde <c>~</c>) inside data values are
/// escaped to underscores so they cannot accidentally trigger ZPL commands.
/// </para>
/// </remarks>
public sealed class V1ShipmentLabelStrategy : ITemplatePrintStrategy
{
    /// <inheritdoc />
    /// <remarks>"v1" is the only explicitly claimed version.  The selector also routes
    /// any unrecognised version here as a safe fallback.</remarks>
    public IReadOnlyCollection<string> SupportedVersions { get; } = ["v1"];

    /// <inheritdoc />
    public PrintDocument Render(
        ShipmentItemLabelData data,
        QrPayloadData qr,
        LabelTemplateSpec templateSpec)
    {
        var zpl = ApplyTokens(templateSpec.ZplBody, data, qr);

        return new PrintDocument
        {
            ZplContent     = zpl,
            Copies         = data.LabelCopies,
            CorrelationId  = data.CorrelationId,
            IdempotencyKey = data.IdempotencyKey,
            RenderedDpi    = templateSpec.DesignDpi,
        };
    }

    // ── Token substitution ────────────────────────────────────────────────

    private static string ApplyTokens(string zplBody, ShipmentItemLabelData d, QrPayloadData qr)
    {
        var sb = new StringBuilder(zplBody);

        sb.Replace("{{CustomerCode}}", SanitizeZpl(d.CustomerCode));
        sb.Replace("{{PartNo}}",       SanitizeZpl(d.PartNo));
        sb.Replace("{{ProductName}}", SanitizeZpl(d.ProductName));
        sb.Replace("{{Description}}", SanitizeZpl(d.Description));
        sb.Replace("{{Quantity}}",    d.Quantity.ToString(CultureInfo.InvariantCulture));
        sb.Replace("{{PoNumber}}",    SanitizeZpl(d.PoNumber));
        sb.Replace("{{PoItem}}",      SanitizeZpl(d.PoItem));
        sb.Replace("{{DueDate}}",     SanitizeZpl(d.DueDate));
        sb.Replace("{{RunNo}}",       SanitizeZpl(d.RunNo));
        sb.Replace("{{Store}}",       SanitizeZpl(d.Store));
        sb.Replace("{{Remarks}}",     SanitizeZpl(d.Remarks));
        sb.Replace("{{BatchNumber}}", SanitizeZpl(d.BatchNumber));
        sb.Replace("{{LineNumber}}",  d.LineNumber.ToString(CultureInfo.InvariantCulture));
        sb.Replace("{{QrPayload}}",   qr.Payload);

        return sb.ToString();
    }

    /// <summary>
    /// Removes ZPL control characters from user-supplied strings.
    /// <c>^</c> starts a ZPL field command; <c>~</c> starts an immediate command.
    /// Replacing them with safe look-alikes prevents injection.
    /// </summary>
    private static string SanitizeZpl(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace('^', '_').Replace('~', '-');
    }
}

