using System.Diagnostics;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Website.Models;
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

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
