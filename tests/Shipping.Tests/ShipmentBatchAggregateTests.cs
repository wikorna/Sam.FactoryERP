using FluentAssertions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;

namespace Shipping.Tests;

/// <summary>
/// Unit tests for <see cref="ShipmentBatch"/> aggregate root.
/// Validates state transitions, invariants, and item management.
/// </summary>
public sealed class ShipmentBatchAggregateTests
{
    private static ShipmentBatch CreateDraftBatch()
        => ShipmentBatch.CreateDraft(
            batchNumber: "SB-20260313-001",
            poReference: "PO-TEST",
            sourceFileName: "test.csv",
            sourceFileSha256: "abc123",
            sourceRowCount: 5,
            createdBy: "test-user");

    private static ShipmentBatch CreateBatchWithItem()
    {
        var batch = CreateDraftBatch();
        batch.AddItem(
            lineNumber: 1,
            customerCode: "CUST-001",
            partNo: "PART-A",
            productName: "Widget",
            description: "A widget",
            quantity: 10,
            poNumber: "PO-TEST",
            poItem: "10",
            dueDate: "2026-04-01",
            runNo: "R1",
            store: "W1",
            qrPayload: null,
            remarks: "test");
        return batch;
    }

    // ── CreateDraft ───────────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_SetsInitialState()
    {
        var batch = CreateDraftBatch();

        batch.Status.Should().Be(ShipmentBatchStatus.Draft);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Pending);
        batch.BatchNumber.Should().Be("SB-20260313-001");
        batch.PoReference.Should().Be("PO-TEST");
        batch.SourceFileName.Should().Be("test.csv");
        batch.Items.Should().BeEmpty();
        batch.RowErrors.Should().BeEmpty();
    }

    // ── AddItem ───────────────────────────────────────────────────────────

    [Fact]
    public void AddItem_InDraft_Succeeds()
    {
        var batch = CreateDraftBatch();
        var item = batch.AddItem(1, "C1", "P1", "Widget", "Desc", 10, null, null, null, null, null, null, null);

        batch.Items.Should().ContainSingle();
        item.CustomerCode.Should().Be("C1");
        item.PartNo.Should().Be("P1");
        item.Quantity.Should().Be(10);
    }

    [Fact]
    public void AddItem_NotInDraft_Throws()
    {
        var batch = CreateBatchWithItem();
        batch.Submit(); // → Submitted

        var act = () => batch.AddItem(2, "C2", "P2", "G", "D", 5, null, null, null, null, null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*add items*Submitted*");
    }

    // ── AddRowError ───────────────────────────────────────────────────────

    [Fact]
    public void AddRowError_InDraft_Succeeds()
    {
        var batch = CreateDraftBatch();
        batch.AddRowError(1, "MISSING_PART_NO", "PartNo is required.");

        batch.RowErrors.Should().ContainSingle();
    }

    // ── Submit ────────────────────────────────────────────────────────────

    [Fact]
    public void Submit_WithItems_TransitionsToSubmitted()
    {
        var batch = CreateBatchWithItem();

        batch.Submit();

        batch.Status.Should().Be(ShipmentBatchStatus.Submitted);
        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Shipping.Domain.Events.ShipmentBatchSubmitted>();
    }

    [Fact]
    public void Submit_EmptyBatch_Throws()
    {
        var batch = CreateDraftBatch();

        var act = () => batch.Submit();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void Submit_NotInDraft_Throws()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();

        var act = () => batch.Submit(); // already submitted

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*submit*Submitted*");
    }

    // ── Review lifecycle ──────────────────────────────────────────────────

    [Fact]
    public void BeginReview_FromSubmitted_TransitionsToUnderReview()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();

        batch.BeginReview();

        batch.Status.Should().Be(ShipmentBatchStatus.UnderReview);
    }

    [Fact]
    public void Approve_FromUnderReview_TransitionsToApproved()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        var reviewerId = Guid.NewGuid();

        batch.Approve(reviewerId, "Looks good");

        batch.Status.Should().Be(ShipmentBatchStatus.Approved);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Approved);
        batch.ReviewedByUserId.Should().Be(reviewerId);
        batch.ReviewComment.Should().Be("Looks good");
    }

    [Fact]
    public void Reject_FromUnderReview_TransitionsToRejected()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        var reviewerId = Guid.NewGuid();

        batch.Reject(reviewerId, "Wrong quantities");

        batch.Status.Should().Be(ShipmentBatchStatus.Rejected);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Rejected);
        batch.ReviewComment.Should().Be("Wrong quantities");
    }

    [Fact]
    public void Reject_RequiresReason()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();

        var act = () => batch.Reject(Guid.NewGuid(), "   ");

        act.Should().Throw<ArgumentException>();
    }

    // ── Print lifecycle ───────────────────────────────────────────────────

    [Fact]
    public void PrepareForPrint_FromApproved_SetsReadyForPrint()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());
        var printerId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        batch.PrepareForPrint(printerId, templateId);

        batch.Status.Should().Be(ShipmentBatchStatus.ReadyForPrint);
        batch.PrinterId.Should().Be(printerId);
        batch.LabelTemplateId.Should().Be(templateId);
    }

    [Fact]
    public void MarkPrintRequested_FromReadyForPrint_Transitions()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());
        batch.PrepareForPrint(Guid.NewGuid(), Guid.NewGuid());

        batch.MarkPrintRequested();

        batch.Status.Should().Be(ShipmentBatchStatus.PrintRequested);
        batch.PrintRequestedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_FromPrintRequested_Transitions()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());
        batch.PrepareForPrint(Guid.NewGuid(), Guid.NewGuid());
        batch.MarkPrintRequested();

        batch.MarkCompleted();

        batch.Status.Should().Be(ShipmentBatchStatus.Completed);
        batch.CompletedAtUtc.Should().NotBeNull();
        batch.IsTerminal.Should().BeTrue();
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromDraft_Succeeds()
    {
        var batch = CreateDraftBatch();

        batch.Cancel();

        batch.Status.Should().Be(ShipmentBatchStatus.Canceled);
        batch.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Cancel_FromCompleted_Throws()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());
        batch.PrepareForPrint(Guid.NewGuid(), Guid.NewGuid());
        batch.MarkPrintRequested();
        batch.MarkCompleted();

        var act = () => batch.Cancel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Completed*");
    }

    // ── RevertToDraft ─────────────────────────────────────────────────────

    [Fact]
    public void RevertToDraft_FromRejected_ClearsReviewState()
    {
        var batch = CreateBatchWithItem();
        batch.Submit();
        batch.BeginReview();
        batch.Reject(Guid.NewGuid(), "Bad data");

        batch.RevertToDraft();

        batch.Status.Should().Be(ShipmentBatchStatus.Draft);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Pending);
        batch.ReviewedByUserId.Should().BeNull();
        batch.ReviewedAtUtc.Should().BeNull();
        batch.ReviewComment.Should().BeNull();
    }
}

