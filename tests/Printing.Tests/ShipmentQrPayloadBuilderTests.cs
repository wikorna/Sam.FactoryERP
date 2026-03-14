using FluentAssertions;
using Printing.Application.Models;
using Printing.Infrastructure.Services;

namespace Printing.Tests;

/// <summary>Tests for <see cref="ShipmentQrPayloadBuilder"/>.</summary>
public sealed class ShipmentQrPayloadBuilderTests
{
    private readonly ShipmentQrPayloadBuilder _builder = new();

    private static ShipmentItemLabelData BaseData(
        string? precomputed = null,
        string? poNumber = null,
        string? poItem = null,
        string? dueDate = null) =>
        new()
        {
            BatchId              = Guid.NewGuid(),
            ItemId               = Guid.NewGuid(),
            BatchNumber          = "SB-20260314-001",
            LineNumber           = 3,
            CustomerCode         = "CUST-001",
            PartNo               = "PART-ABC123",
            ProductName          = "Widget Assembly",
            Description          = "A standard widget",
            Quantity             = 25,
            PoNumber             = poNumber,
            PoItem               = poItem,
            DueDate              = dueDate,
            LabelCopies          = 1,
            PrinterId            = Guid.NewGuid(),
            LabelTemplateId      = Guid.NewGuid(),
            RequestedBy          = "test-user",
            IdempotencyKey       = "key-001",
            CorrelationId        = Guid.NewGuid(),
            PrecomputedQrPayload = precomputed,
        };

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Build_AllFields_ProducesV1PipeDelimitedPayload()
    {
        var data = BaseData(poNumber: "PO-2026-001", poItem: "10", dueDate: "2026-03-20");

        var result = _builder.Build(data);

        result.Payload.Should().Be(
            "v1|CUST-001|PART-ABC123|Widget Assembly|25|PO-2026-001|10|2026-03-20|SB-20260314-001|3");
        result.Version.Should().Be("v1");
        result.PartNo.Should().Be("PART-ABC123");
    }

    [Fact]
    public void Build_NullOptionals_ProducesEmptySegments()
    {
        var data = BaseData(); // no PO, no item, no date

        var result = _builder.Build(data);

        // 4th, 5th, 6th segments after quantity should be empty
        var parts = result.Payload.Split('|');
        parts[5].Should().BeEmpty(); // PoNumber
        parts[6].Should().BeEmpty(); // PoItem
        parts[7].Should().BeEmpty(); // DueDate
    }

    [Fact]
    public void Build_PrecomputedPayload_ReturnedAsIs()
    {
        const string existing = "v1|CUST-999|PART-X|Old Name|10|||2026-01-01|SB-OLD-001|1";
        var data = BaseData(precomputed: existing);

        var result = _builder.Build(data);

        result.Payload.Should().Be(existing);
    }

    // ── Escaping ──────────────────────────────────────────────────────────

    [Fact]
    public void Build_PipeInPartNo_IsEscaped()
    {
        var data = BaseData() with { PartNo = "PART|PIPE" };

        var result = _builder.Build(data);

        // The literal pipe in the value is escaped to \| so scanner firmware
        // using backslash-aware split sees exactly 10 logical fields.
        // (A naive string.Split('|') sees 11 — that is expected and correct.)
        result.Payload.Should().Contain(@"PART\|PIPE");
    }

    [Fact]
    public void Build_BackslashInCustomerCode_IsEscaped()
    {
        var data = BaseData() with { CustomerCode = @"CUST\001" };

        var result = _builder.Build(data);

        result.Payload.Should().Contain(@"CUST\\001");
    }

    [Fact]
    public void Build_EmptyPrecomputedPayload_FallsBackToBuilding()
    {
        var data = BaseData(precomputed: "   "); // whitespace only

        var result = _builder.Build(data);

        result.Payload.Should().StartWith("v1|");
    }

    // ── Field order ───────────────────────────────────────────────────────

    [Fact]
    public void Build_FieldOrder_MatchesV1Spec()
    {
        var data = BaseData(poNumber: "PO-X", poItem: "5", dueDate: "2026-12-31");

        var parts = _builder.Build(data).Payload.Split('|');

        parts[0].Should().Be("v1");
        parts[1].Should().Be("CUST-001");   // CustomerCode
        parts[2].Should().Be("PART-ABC123"); // PartNo
        parts[3].Should().Be("Widget Assembly"); // ProductName
        parts[4].Should().Be("25");          // Quantity
        parts[5].Should().Be("PO-X");        // PoNumber
        parts[6].Should().Be("5");           // PoItem
        parts[7].Should().Be("2026-12-31");  // DueDate
        parts[8].Should().Be("SB-20260314-001"); // BatchNumber
        parts[9].Should().Be("3");           // LineNumber
    }
}

