namespace AppDomain
{
    public interface IHealthCheckService
    {
        Task<string> CheckHealthAsync();
    }
}
