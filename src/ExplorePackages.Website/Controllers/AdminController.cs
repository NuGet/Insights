using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knapcode.ExplorePackages.Website.Controllers
{
    [Authorize(Policy = AllowListAuthorizationHandler.PolicyName)]
    public class AdminController : Controller
    {
        public ViewResult Index()
        {
            return View();
        }
    }
}
