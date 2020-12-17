using System.Text.Json.Serialization;
using Knapcode.ExplorePackages.Website.Logic;
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
            services.Configure<ExplorePackagesWebsiteSettings>(Configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));

            services.AddExplorePackages("Knapcode.ExplorePackages.Website");

            services.AddSingleton<IAuthorizationHandler, AllowListAuthorizationHandler>();

            services.AddLogging();
            services.AddMvc();
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
                    options.Instance = "https://login.microsoftonline.com/";
                    options.ClientId = "3182a756-a40a-41a9-851b-68d16b92e373";
                    options.TenantId = "common";
                });

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
