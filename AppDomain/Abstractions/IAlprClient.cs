namespace AppDomain.Abstractions
{
    public interface IAlprClient
    {
        Task StartProcessingAsync(Func<string, Task> processResult, RelativeRectangle roi,  CancellationToken cancellationToken);
    }
}
