using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Knapcode.ExplorePackages.Website.Controllers
{
    [Route("api/[controller]")]
    public class AutocompleteController : Controller
    {
        private readonly AutocompleteClient _client;

        public AutocompleteController(AutocompleteClient client)
        {
            _client = client;
        }

        [HttpGet]
        public async Task<object> Get(string q, string id, int? skip, int? take, bool? prerelease, string semVerLevel)
        {
            if (id != null)
            {
                return await _client.GetVersionsAsync(id, prerelease, semVerLevel);
            }
            else
            {
                return await _client.GetIdsAsync(q, skip, take, prerelease, semVerLevel);
            }
        }
    }
}
