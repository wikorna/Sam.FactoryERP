using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.ValueObjects;
using Xunit;

namespace EDI.Tests;

public class EdiFileJobTests
{
    [Fact]
    public void CreateReceivedShouldInitializeCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var partner = "TEST";
        var file = "test.csv";
        var path = "/tmp/test.csv";

        // Act
        var job = EdiFileJob.CreateReceived(
            id,
            partner,
            file,
            path,
            1024,
            "sha256",
            EdiFormat.Csv,
            EdiSchemaVersion.V1);

        // Assert
        Assert.Equal(id, job.Id);
        Assert.Equal(EdiFileJobStatus.Received, job.Status);
        Assert.Single(job.DomainEvents);
    }

    [Fact]
    public void MarkParsingShouldChangeStatus()
    {
        // Arrange
        var job = CreateJob();

        // Act
        job.MarkParsing();

        // Assert
        Assert.Equal(EdiFileJobStatus.Parsing, job.Status);
    }

    private static EdiFileJob CreateJob()
    {
        return EdiFileJob.CreateReceived(
            Guid.NewGuid(),
            "TEST",
            "test.csv",
            "/tmp/test.csv",
            1024,
            "sha256",
            EdiFormat.Csv,
            EdiSchemaVersion.V1);
    }
}
