namespace VanAn.CoreHub.Services
{
    public interface IBuildService
    {
        Task<object> GetBuildStatusAsync();
    }
}
