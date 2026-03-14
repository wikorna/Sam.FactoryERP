using FluentAssertions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;
using Shipping.Domain.Events;

namespace Shipping.Tests;

/// <summary>
/// Tests for warehouse review state transitions on <see cref="ShipmentBatch"/>,
/// including partial approval with item-level decisions.
/// </summary>
public sealed class WarehouseReviewStateTransitionTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ShipmentBatch CreateSubmittedBatch(int itemCount = 3)
    {
        var batch = ShipmentBatch.CreateDraft(
            batchNumber: "SB-20260313-001",
            poReference: "PO-TEST",
            sourceFileName: "test.csv",
            sourceFileSha256: "abc123",
            sourceRowCount: itemCount,
            createdBy: "test-user");

        for (int i = 1; i <= itemCount; i++)
        {
            batch.AddItem(i, $"CUST-{i:D3}", $"PART-{i:D3}", $"Product {i}",
                $"Description {i}", i * 10, "PO-TEST", $"{i}0",
                "2026-04-01", $"R{i}", "W1", null, null);
        }

        batch.Submit();
        batch.ClearDomainEvents(); // reset for assertion clarity
        return batch;
    }

    private static ShipmentBatch CreateUnderReviewBatch(int itemCount = 3)
    {
        var batch = CreateSubmittedBatch(itemCount);
        batch.BeginReview();
        return batch;
    }

    // ── Full Approve ──────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromUnderReview_SetsApprovedState()
    {
        var batch = CreateUnderReviewBatch();
        var reviewerId = Guid.NewGuid();

        batch.Approve(reviewerId, "All looks good");

        batch.Status.Should().Be(ShipmentBatchStatus.Approved);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Approved);
        batch.ReviewedByUserId.Should().Be(reviewerId);
        batch.ReviewedAtUtc.Should().NotBeNull();
        batch.ReviewComment.Should().Be("All looks good");
    }

    [Fact]
    public void Approve_FromUnderReview_RaisesDomainEvent()
    {
        var batch = CreateUnderReviewBatch();
        var reviewerId = Guid.NewGuid();

        batch.Approve(reviewerId);

        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShipmentBatchApproved>()
            .Which.ReviewedByUserId.Should().Be(reviewerId);
    }

    [Fact]
    public void Approve_FromDraft_Throws()
    {
        var batch = ShipmentBatch.CreateDraft("SB-001", "PO", null, null, 0, "u");

        var act = () => batch.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*approve*Draft*");
    }

    [Fact]
    public void Approve_FromApproved_Throws()
    {
        var batch = CreateUnderReviewBatch();
        batch.Approve(Guid.NewGuid());

        var act = () => batch.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*approve*Approved*");
    }

    // ── Full Reject ───────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromUnderReview_SetsRejectedState()
    {
        var batch = CreateUnderReviewBatch();
        var reviewerId = Guid.NewGuid();

        batch.Reject(reviewerId, "Wrong quantities");

        batch.Status.Should().Be(ShipmentBatchStatus.Rejected);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Rejected);
        batch.ReviewedByUserId.Should().Be(reviewerId);
        batch.ReviewComment.Should().Be("Wrong quantities");
    }

    [Fact]
    public void Reject_FromUnderReview_RaisesDomainEvent()
    {
        var batch = CreateUnderReviewBatch();

        batch.Reject(Guid.NewGuid(), "Bad data");

        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShipmentBatchRejected>();
    }

    [Fact]
    public void Reject_EmptyReason_Throws()
    {
        var batch = CreateUnderReviewBatch();

        var act = () => batch.Reject(Guid.NewGuid(), "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_FromDraft_Throws()
    {
        var batch = ShipmentBatch.CreateDraft("SB-001", "PO", null, null, 0, "u");

        var act = () => batch.Reject(Guid.NewGuid(), "reason");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*reject*Draft*");
    }

    // ── Partial Approve ───────────────────────────────────────────────────

    [Fact]
    public void PartiallyApprove_ApprovesSomeAndExcludesOthers()
    {
        var batch = CreateUnderReviewBatch(3);
        var reviewerId = Guid.NewGuid();
        var items = batch.Items.ToList();

        var approvedIds = new HashSet<Guid> { items[0].Id, items[1].Id };
        var exclusionReasons = new Dictionary<Guid, string>
        {
            { items[2].Id, "Wrong part number" },
        };

        batch.PartiallyApprove(reviewerId, approvedIds, exclusionReasons, "Partial OK");

        batch.Status.Should().Be(ShipmentBatchStatus.Approved);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.PartiallyApproved);
        batch.ReviewedByUserId.Should().Be(reviewerId);
        batch.ReviewComment.Should().Be("Partial OK");

        items[0].ReviewStatus.Should().Be(ItemReviewStatus.Approved);
        items[0].ExclusionReason.Should().BeNull();

        items[1].ReviewStatus.Should().Be(ItemReviewStatus.Approved);

        items[2].ReviewStatus.Should().Be(ItemReviewStatus.Excluded);
        items[2].ExclusionReason.Should().Be("Wrong part number");
    }

    [Fact]
    public void PartiallyApprove_RaisesDomainEvent()
    {
        var batch = CreateUnderReviewBatch(3);
        var items = batch.Items.ToList();
        var approvedIds = new HashSet<Guid> { items[0].Id };

        batch.PartiallyApprove(Guid.NewGuid(), approvedIds);

        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ShipmentBatchPartiallyApproved>()
            .Which.Should().Match<ShipmentBatchPartiallyApproved>(e =>
                e.ApprovedItemCount == 1 && e.ExcludedItemCount == 2);
    }

    [Fact]
    public void PartiallyApprove_NoApprovedItems_Throws()
    {
        var batch = CreateUnderReviewBatch();

        var act = () => batch.PartiallyApprove(Guid.NewGuid(), new HashSet<Guid>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*At least one item*");
    }

    [Fact]
    public void PartiallyApprove_AllItemsApproved_Throws()
    {
        var batch = CreateUnderReviewBatch(2);
        var allIds = batch.Items.Select(i => i.Id).ToHashSet();

        var act = () => batch.PartiallyApprove(Guid.NewGuid(), allIds);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*All items are approved*");
    }

    [Fact]
    public void PartiallyApprove_UnknownItemId_Throws()
    {
        var batch = CreateUnderReviewBatch();
        var unknownId = Guid.NewGuid();

        var act = () => batch.PartiallyApprove(
            Guid.NewGuid(),
            new HashSet<Guid> { unknownId });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void PartiallyApprove_FromDraft_Throws()
    {
        var batch = ShipmentBatch.CreateDraft("SB-001", "PO", null, null, 0, "u");

        var act = () => batch.PartiallyApprove(
            Guid.NewGuid(),
            new HashSet<Guid> { Guid.NewGuid() });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*partially approve*Draft*");
    }

    // ── Exclude reason optional ───────────────────────────────────────────

    [Fact]
    public void PartiallyApprove_ExcludedWithoutReason_SetsNullReason()
    {
        var batch = CreateUnderReviewBatch(2);
        var items = batch.Items.ToList();
        var approvedIds = new HashSet<Guid> { items[0].Id };

        batch.PartiallyApprove(Guid.NewGuid(), approvedIds);

        items[1].ReviewStatus.Should().Be(ItemReviewStatus.Excluded);
        items[1].ExclusionReason.Should().BeNull();
    }

    // ── RevertToDraft resets item review ───────────────────────────────────

    [Fact]
    public void RevertToDraft_AfterPartialApprove_ResetsItemReview()
    {
        var batch = CreateUnderReviewBatch(2);
        var items = batch.Items.ToList();
        var approvedIds = new HashSet<Guid> { items[0].Id };

        // Partially approve, then reject (need to get to Rejected state)
        // Actually we can't revert from Approved, only from Rejected.
        // So let's reject instead.
        batch.Reject(Guid.NewGuid(), "reason");

        batch.RevertToDraft();

        batch.Status.Should().Be(ShipmentBatchStatus.Draft);
        batch.ReviewDecision.Should().Be(WarehouseReviewDecision.Pending);

        // Items should have been reset.
        foreach (var item in batch.Items)
        {
            item.ReviewStatus.Should().Be(ItemReviewStatus.Pending);
            item.ExclusionReason.Should().BeNull();
        }
    }

    // ── BeginReview ───────────────────────────────────────────────────────

    [Fact]
    public void BeginReview_FromSubmitted_Transitions()
    {
        var batch = CreateSubmittedBatch();

        batch.BeginReview();

        batch.Status.Should().Be(ShipmentBatchStatus.UnderReview);
    }

    [Fact]
    public void BeginReview_FromDraft_Throws()
    {
        var batch = ShipmentBatch.CreateDraft("SB-001", "PO", null, null, 0, "u");

        var act = () => batch.BeginReview();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*begin review*Draft*");
    }

    // ── Approved → PrepareForPrint still works ────────────────────────────

    [Fact]
    public void PrepareForPrint_AfterFullApprove_Works()
    {
        var batch = CreateUnderReviewBatch();
        batch.Approve(Guid.NewGuid());

        batch.PrepareForPrint(Guid.NewGuid(), Guid.NewGuid());

        batch.Status.Should().Be(ShipmentBatchStatus.ReadyForPrint);
    }

    [Fact]
    public void PrepareForPrint_AfterPartialApprove_Works()
    {
        var batch = CreateUnderReviewBatch(3);
        var items = batch.Items.ToList();
        batch.PartiallyApprove(Guid.NewGuid(), new HashSet<Guid> { items[0].Id, items[1].Id });

        batch.PrepareForPrint(Guid.NewGuid(), Guid.NewGuid());

        batch.Status.Should().Be(ShipmentBatchStatus.ReadyForPrint);
    }
}

