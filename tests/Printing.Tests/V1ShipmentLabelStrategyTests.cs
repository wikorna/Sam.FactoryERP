using FluentAssertions;
using Printing.Application.Models;
using Printing.Infrastructure.Strategies;

namespace Printing.Tests;

/// <summary>Tests for <see cref="V1ShipmentLabelStrategy"/>.</summary>
public sealed class V1ShipmentLabelStrategyTests
{
    private readonly V1ShipmentLabelStrategy _strategy = new();

    private static LabelTemplateSpec MakeTemplate(string? body = null) => new()
    {
        Id            = Guid.NewGuid(),
        TemplateKey   = "ShipmentQrLabel",
        Version       = "v1",
        ZplBody       = body ?? "^XA^FD{{ProductName}}^FS^FD{{QrPayload}}^FS^XZ",
        DesignDpi     = 300,
        LabelWidthMm  = 90,
        LabelHeightMm = 55,
    };

    private static ShipmentItemLabelData MakeData() => new()
    {
        BatchId        = Guid.NewGuid(),
        ItemId         = Guid.NewGuid(),
        BatchNumber    = "SB-20260314-001",
        LineNumber     = 1,
        CustomerCode   = "CUST-A",
        PartNo         = "PART-X",
        ProductName    = "Widget",
        Description    = "A widget",
        Quantity       = 10,
        PoNumber       = "PO-1",
        PoItem         = "05",
        DueDate        = "2026-04-01",
        LabelCopies    = 2,
        PrinterId      = Guid.NewGuid(),
        LabelTemplateId = Guid.NewGuid(),
        RequestedBy    = "tester",
        IdempotencyKey = "batch:item",
        CorrelationId  = Guid.NewGuid(),
    };

    private static QrPayloadData MakeQr(string payload = "v1|QR-DATA") => new()
    {
        Payload = payload,
        Version = "v1",
        PartNo  = "PART-X",
    };

    // ── SupportedVersions ─────────────────────────────────────────────────

    [Fact]
    public void SupportedVersions_ContainsV1()
    {
        _strategy.SupportedVersions.Should().Contain("v1");
    }

    // ── Token substitution ────────────────────────────────────────────────

    [Fact]
    public void Render_AllTokens_AreSubstituted()
    {
        const string body =
            "{{CustomerCode}}|{{PartNo}}|{{ProductName}}|{{Description}}|{{Quantity}}" +
            "|{{PoNumber}}|{{PoItem}}|{{DueDate}}|{{BatchNumber}}|{{LineNumber}}|{{QrPayload}}";

        var data = MakeData();
        var qr   = MakeQr("v1|TEST-QR");

        var doc = _strategy.Render(data, qr, MakeTemplate(body));

        doc.ZplContent.Should().Be(
            "CUST-A|PART-X|Widget|A widget|10|PO-1|05|2026-04-01|SB-20260314-001|1|v1|TEST-QR");
    }

    [Fact]
    public void Render_NullOptionalFields_SubstitutedAsEmpty()
    {
        const string body = "{{RunNo}}|{{Store}}|{{Remarks}}";
        var data = MakeData() with { RunNo = null, Store = null, Remarks = null };

        var doc = _strategy.Render(data, MakeQr(), MakeTemplate(body));

        doc.ZplContent.Should().Be("||");
    }

    // ── ZPL injection prevention ──────────────────────────────────────────

    [Fact]
    public void Render_CaretInProductName_IsReplacedWithUnderscore()
    {
        var data = MakeData() with { ProductName = "Widget^Hack" };
        const string body = "{{ProductName}}";

        var doc = _strategy.Render(data, MakeQr(), MakeTemplate(body));

        doc.ZplContent.Should().Be("Widget_Hack");
        doc.ZplContent.Should().NotContain("^");
    }

    [Fact]
    public void Render_TildeInDescription_IsReplacedWithDash()
    {
        var data = MakeData() with { Description = "Reset~Cmd" };
        const string body = "{{Description}}";

        var doc = _strategy.Render(data, MakeQr(), MakeTemplate(body));

        doc.ZplContent.Should().Be("Reset-Cmd");
    }

    [Fact]
    public void Render_QrPayload_IsInjectedVerbatim()
    {
        // QR payload must NOT be sanitised — it contains pipe chars from v1 format
        const string payload = "v1|CUST|PART|Name|10|PO||2026-01-01|SB-001|1";
        const string body    = "{{QrPayload}}";

        var doc = _strategy.Render(MakeData(), MakeQr(payload), MakeTemplate(body));

        doc.ZplContent.Should().Be(payload);
    }

    // ── PrintDocument properties ──────────────────────────────────────────

    [Fact]
    public void Render_ReturnsCopiesFromLabelData()
    {
        var data = MakeData() with { LabelCopies = 3 };

        var doc = _strategy.Render(data, MakeQr(), MakeTemplate());

        doc.Copies.Should().Be(3);
    }

    [Fact]
    public void Render_ReturnsDesignDpiFromTemplate()
    {
        var doc = _strategy.Render(MakeData(), MakeQr(), MakeTemplate());

        doc.RenderedDpi.Should().Be(300);
    }

    [Fact]
    public void Render_CarriesIdempotencyKeyAndCorrelation()
    {
        var data = MakeData() with
        {
            IdempotencyKey = "batch-001:item-007",
            CorrelationId  = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        };

        var doc = _strategy.Render(data, MakeQr(), MakeTemplate());

        doc.IdempotencyKey.Should().Be("batch-001:item-007");
        doc.CorrelationId.Should().Be(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
    }
}

