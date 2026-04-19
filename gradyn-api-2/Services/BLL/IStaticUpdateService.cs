namespace gradyn_api_2.Services.BLL;

public interface IStaticUpdateService
{
    Task PerformStaticUpdateAsync(string webhookId);
}