// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace NuGet.Insights.Website.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAdminViewModelCache _cache;
        private readonly IOptions<NuGetInsightsWebsiteSettings> _options;

        public HomeController(IAdminViewModelCache cache, IOptions<NuGetInsightsWebsiteSettings> options)
        {
            _cache = cache;
            _options = options;
        }

        public ViewResult Index()
        {
            return View(_options.Value.ShowAdminMetadata ? _cache : null);
        }

        [HttpGet("/healthz")]
        public ContentResult Health()
        {
            return Content("The website is alive.");
        }

        public async Task<RedirectToActionResult> SignOutAndRedirect()
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
