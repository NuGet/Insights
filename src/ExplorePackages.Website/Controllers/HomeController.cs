using System.Diagnostics;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Website.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Website.Controllers
{
    public class HomeController : Controller
    {
        public ViewResult Index()
        {
            return View(new ExploreViewModel());
        }

        public ActionResult Explore(string id, string version)
        {
            if (!StrictPackageIdValidator.IsValid(id)
                || !NuGetVersion.TryParse(version, out var parsedVersion))
            {
                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), new ExploreViewModel(id, version));
        }

        public async Task<RedirectToActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Index));
        }

        public ViewResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public ViewResult AccessDenied()
        {
            return View();
        }
    }
}
