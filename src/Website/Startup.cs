using System;
using System.Text.Json.Serialization;
using Knapcode.ExplorePackages.Website.Logic;
using Knapcode.ExplorePackages.Worker;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

namespace Knapcode.ExplorePackages.Website
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ExplorePackagesSettings>(Configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));
            services.Configure<ExplorePackagesWorkerSettings>(Configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));
            services.Configure<ExplorePackagesWebsiteSettings>(Configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));

            services.AddExplorePackages("Knapcode.ExplorePackages.Website");
            services.AddExplorePackagesWorker();

            services.AddScoped<IAuthorizationHandler, AllowListAuthorizationHandler>();
            services.AddScoped<AllowListAuthorizationHandler>();

            services.AddApplicationInsightsTelemetry(options =>
            {
                options.InstrumentationKey = Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
                options.EnableAdaptiveSampling = false;
            });

            services
                .AddMvc()
                .AddRazorRuntimeCompilation();

            services
                .AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            services
                .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(options =>
                {
                    Configuration.GetSection(Constants.AzureAd).Bind(options);
                    options.Events.OnTokenValidated = async context =>
                    {
                        await context
                            .HttpContext
                            .RequestServices
                            .GetRequiredService<AllowListAuthorizationHandler>()
                            .AddAllowedGroupClaimsAsync(context);
                    };
                },
                options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromHours(1);
                    options.SlidingExpiration = false;
                    options.AccessDeniedPath = "/Home/AccessDenied";
                })
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches()
                .AddMicrosoftGraph();

            services
                .AddAuthorization(options =>
                {
                    options.AddPolicy(
                        AllowListAuthorizationHandler.PolicyName,
                        policy => policy.Requirements.Add(new AllowListRequirement()));
                });

            services
                .AddRazorPages()
                .AddMicrosoftIdentityUI();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(routes =>
            {
                routes.MapHub<PackageReportHub>(PackageReportHub.Path);

                routes.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                routes.MapControllerRoute(
                    name: "explore",
                    pattern: "{controller=Home}/{action=Explore}/{id}/{version}");
            });
        }
    }
}
