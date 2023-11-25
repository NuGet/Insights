// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Humanizer;
using Microsoft.AspNetCore.Mvc.Rendering;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Source: https://stackoverflow.com/a/20411015
        /// </summary>
        public static string IsSelected(this IHtmlHelper htmlHelper, string controllers, string actions, string cssClass = "active")
        {
            var currentAction = htmlHelper.ViewContext.RouteData.Values["action"] as string;
            var currentController = htmlHelper.ViewContext.RouteData.Values["controller"] as string;

            IEnumerable<string> acceptedActions = (actions ?? currentAction).Split(',');
            IEnumerable<string> acceptedControllers = (controllers ?? currentController).Split(',');

            return acceptedActions.Contains(currentAction) && acceptedControllers.Contains(currentController) ?
                cssClass : string.Empty;
        }

        public static string GetRuntime(this DateTimeOffset? completed, DateTimeOffset? started)
        {
            if (!started.HasValue)
            {
                return string.Empty;
            }

            var runtime = completed.GetValueOrDefault(DateTimeOffset.UtcNow) - started.Value;
            var runtimeStr = runtime.ToString("d\\.hh\\:mm\\:ss", CultureInfo.InvariantCulture);
            if (runtimeStr.StartsWith("0.", StringComparison.Ordinal))
            {
                runtimeStr = runtimeStr.Substring(2);
            }

            return runtimeStr;
        }

        public static string GetTitle(this CatalogScanDriverType type)
        {
            var title = type.ToString().Humanize();

            title = title.Replace(" csv", " CSV", StringComparison.Ordinal);

            title = title.Replace("Nu get package explorer", "NuGet Package Explorer", StringComparison.Ordinal);

            return title;
        }
    }
}
