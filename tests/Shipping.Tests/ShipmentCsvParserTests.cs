using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Shipping.Infrastructure.Parsers;

namespace Shipping.Tests;

/// <summary>
/// Unit tests for <see cref="ShipmentCsvParser"/>.
/// Validates CSV parsing, field extraction, quoting, validation, and error handling.
/// </summary>
public sealed class ShipmentCsvParserTests
{
    private readonly ShipmentCsvParser _parser = new(NullLogger<ShipmentCsvParser>.Instance);

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MemoryStream ToStream(string csv)
        => new(Encoding.UTF8.GetBytes(csv));

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ValidCsv_ReturnsAllRows()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity,PoNumber,PoItem,DueDate,RunNo,Store,Remarks,LabelCopies
            CUST-001,ABC-123,Widget A,A great widget,100,PO-001,10,2026-04-01,RUN-01,W1,Rush order,2
            CUST-002,DEF-456,Widget B,Another widget,50,PO-002,20,2026-04-15,RUN-02,W2,,1
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeTrue();
        result.TotalRows.Should().Be(2);
        result.ValidRows.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();

        var row1 = result.ValidRows[0];
        row1.RowNumber.Should().Be(1);
        row1.CustomerCode.Should().Be("CUST-001");
        row1.PartNo.Should().Be("ABC-123");
        row1.ProductName.Should().Be("Widget A");
        row1.Description.Should().Be("A great widget");
        row1.Quantity.Should().Be(100);
        row1.PoNumber.Should().Be("PO-001");
        row1.PoItem.Should().Be("10");
        row1.DueDate.Should().Be("2026-04-01");
        row1.RunNo.Should().Be("RUN-01");
        row1.Store.Should().Be("W1");
        row1.Remarks.Should().Be("Rush order");
        row1.LabelCopies.Should().Be(2);

        var row2 = result.ValidRows[1];
        row2.RowNumber.Should().Be(2);
        row2.CustomerCode.Should().Be("CUST-002");
        row2.LabelCopies.Should().Be(1);
        row2.Remarks.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_MinimalColumns_ReturnsRows()
    {
        // Arrange — only 5 required columns
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            CUST-A,PART-01,Gadget,A gadget,10
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeTrue();
        result.ValidRows.Should().HaveCount(1);

        var row = result.ValidRows[0];
        row.CustomerCode.Should().Be("CUST-A");
        row.PartNo.Should().Be("PART-01");
        row.Quantity.Should().Be(10);
        row.PoNumber.Should().BeNull();
        row.LabelCopies.Should().Be(1); // default
    }

    // ── Quoted fields ─────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_QuotedFields_HandlesCommasInValues()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            CUST-A,PART-01,"Widget, Large","A widget, with comma",25
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeTrue();
        var row = result.ValidRows[0];
        row.ProductName.Should().Be("Widget, Large");
        row.Description.Should().Be("A widget, with comma");
    }

    [Fact]
    public async Task ParseAsync_EscapedQuotes_HandlesDoubleQuotes()
    {
        // Arrange — raw string literal cannot contain "" so we build it manually
        var csv = "CustomerCode,PartNo,ProductName,Description,Quantity\n"
                + "CUST-A,PART-01,\"Widget \"\"Pro\"\"\",\"The \"\"best\"\" widget\",10\n";

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeTrue();
        var row = result.ValidRows[0];
        row.ProductName.Should().Be("Widget \"Pro\"");
        row.Description.Should().Be("The \"best\" widget");
    }

    // ── Validation errors ─────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_MissingRequiredFields_RecordsErrors()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            ,PART-01,Widget,Desc,10
            CUST-A,,Widget,Desc,10
            CUST-A,PART-01,,Desc,10
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidRows.Should().BeEmpty();
        result.Errors.Should().HaveCount(3);
        result.Errors[0].ErrorCode.Should().Be("MISSING_CUSTOMER_CODE");
        result.Errors[1].ErrorCode.Should().Be("MISSING_PART_NO");
        result.Errors[2].ErrorCode.Should().Be("MISSING_PRODUCT_NAME");
    }

    [Fact]
    public async Task ParseAsync_InvalidQuantity_RecordsError()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            CUST-A,PART-01,Widget,Desc,abc
            CUST-B,PART-02,Widget2,Desc2,0
            CUST-C,PART-03,Widget3,Desc3,-5
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidRows.Should().BeEmpty();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().AllSatisfy(e => e.ErrorCode.Should().Be("INVALID_QUANTITY"));
    }

    [Fact]
    public async Task ParseAsync_InsufficientColumns_RecordsError()
    {
        // Arrange — only 3 columns, need at least 5
        const string csv = """
            CustomerCode,PartNo,ProductName
            CUST-A,PART-01,Widget
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorCode.Should().Be("INSUFFICIENT_COLUMNS");
    }

    // ── LabelCopies ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_InvalidLabelCopies_DefaultsToOneWithWarning()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity,PoNumber,PoItem,DueDate,RunNo,Store,Remarks,LabelCopies
            CUST-A,PART-01,Widget,Desc,10,,,,,,,-1
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert — row still parses successfully, but an error is recorded for LabelCopies
        result.ValidRows.Should().HaveCount(1);
        result.ValidRows[0].LabelCopies.Should().Be(1); // default
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorCode.Should().Be("INVALID_LABEL_COPIES");
    }

    [Fact]
    public async Task ParseAsync_LabelCopiesOver100_DefaultsToOneWithWarning()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity,PoNumber,PoItem,DueDate,RunNo,Store,Remarks,LabelCopies
            CUST-A,PART-01,Widget,Desc,10,,,,,,,999
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.ValidRows.Should().HaveCount(1);
        result.ValidRows[0].LabelCopies.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorCode.Should().Be("INVALID_LABEL_COPIES");
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsZeroRows()
    {
        // Arrange — header only
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.TotalRows.Should().Be(0);
        result.ValidRows.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_BlankLinesIgnored()
    {
        // Arrange
        const string csv = "CustomerCode,PartNo,ProductName,Description,Quantity\n\nCUST-A,PART-01,Widget,Desc,10\n\n\nCUST-B,PART-02,Widget2,Desc2,20\n";

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.TotalRows.Should().Be(2);
        result.ValidRows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_MixedValidAndInvalidRows_ReturnsBoth()
    {
        // Arrange
        const string csv = """
            CustomerCode,PartNo,ProductName,Description,Quantity
            CUST-A,PART-01,Widget,Desc,10
            ,PART-02,Widget2,Desc2,20
            CUST-C,PART-03,Widget3,Desc3,30
            """;

        // Act
        var result = await _parser.ParseAsync(ToStream(csv));

        // Assert
        result.TotalRows.Should().Be(3);
        result.ValidRows.Should().HaveCount(2);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].RowNumber.Should().Be(2);
        result.Errors[0].ErrorCode.Should().Be("MISSING_CUSTOMER_CODE");
    }
}

