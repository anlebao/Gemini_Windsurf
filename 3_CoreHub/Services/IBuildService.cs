using System.Threading.Tasks;

namespace VanAn.CoreHub.Services
{
    public interface IBuildService
    {
        Task<object> GetBuildStatusAsync();
    }
}
