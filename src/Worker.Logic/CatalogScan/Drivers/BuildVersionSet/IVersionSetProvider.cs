using System.Threading.Tasks;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public interface IVersionSetProvider
    {
        Task<IVersionSet> GetAsync();
    }
}