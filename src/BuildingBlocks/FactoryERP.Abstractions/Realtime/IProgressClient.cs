namespace FactoryERP.Abstractions.Realtime;

public interface IProgressClient
{
    Task ReceiveProgress(JobProgressDto progress);
    Task JobCompleted(JobCompletedDto completed);
    Task JobFailed(JobFailedDto failed);
}
