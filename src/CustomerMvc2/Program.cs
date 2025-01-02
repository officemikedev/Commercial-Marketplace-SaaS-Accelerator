using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Marketplace.SaaS.Accelerator.CustomerSite.Controllers;
using Marketplace.SaaS.Accelerator.CustomerSite.WebHook;
using Marketplace.SaaS.Accelerator.DataAccess.Context;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.DataAccess.Services;
using Marketplace.SaaS.Accelerator.Services.Configurations;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Services;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Marketplace.SaaS.Accelerator.Services.WebHook;
using Azure.Identity;
using Microsoft.Marketplace.SaaS;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace CustomerMvc2;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

        var config = new SaaSApiClientConfiguration()
        {
            AdAuthenticationEndPoint = builder.Configuration["SaaSApiConfiguration:AdAuthenticationEndPoint"],
            ClientId = builder.Configuration["SaaSApiConfiguration:ClientId"],
            ClientSecret = builder.Configuration["SaaSApiConfiguration:ClientSecret"],
            MTClientId = builder.Configuration["SaaSApiConfiguration:MTClientId"],
            FulFillmentAPIBaseURL = builder.Configuration["SaaSApiConfiguration:FulFillmentAPIBaseURL"],
            FulFillmentAPIVersion = builder.Configuration["SaaSApiConfiguration:FulFillmentAPIVersion"],
            GrantType = builder.Configuration["SaaSApiConfiguration:GrantType"],
            Resource = builder.Configuration["SaaSApiConfiguration:Resource"],
            SaaSAppUrl = builder.Configuration["SaaSApiConfiguration:SaaSAppUrl"],
            SignedOutRedirectUri = builder.Configuration["SaaSApiConfiguration:SignedOutRedirectUri"],
            TenantId = builder.Configuration["SaaSApiConfiguration:TenantId"],
            Environment = builder.Configuration["SaaSApiConfiguration:Environment"]
        };
        var creds = new ClientSecretCredential(config.TenantId.ToString(), config.ClientId.ToString(), config.ClientSecret);
        builder.Services
            .AddTransient<IClaimsTransformation, CustomClaimsTransformation>()
            .AddScoped<ExceptionHandlerAttribute>()
            .AddScoped<RequestLoggerActionFilter>();

        if (!Uri.TryCreate(config.FulFillmentAPIBaseURL, UriKind.Absolute, out var fulfillmentBaseApi))
        {
            fulfillmentBaseApi = new Uri("https://marketplaceapi.microsoft.com/api");
        }

        builder.Services
            .AddSingleton<IFulfillmentApiService>(new FulfillmentApiService(new MarketplaceSaaSClient(fulfillmentBaseApi, creds), config, new FulfillmentApiClientLogger()))
            .AddSingleton<SaaSApiClientConfiguration>(config)
            .AddSingleton<ValidateJwtToken>();

        // Add the assembly version
        builder.Services.AddSingleton<IAppVersionService>(new AppVersionService(Assembly.GetExecutingAssembly()?.GetName()?.Version));

        InitializeRepositoryServices(builder.Services);
        builder.Services.AddDbContext<SaasKitContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddControllersWithViews(options =>
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.Filters.Add(new AuthorizeFilter(policy));
        });
        builder.Services.AddRazorPages()
            .AddMicrosoftIdentityUI();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        app.Run();
    }

    private static void InitializeRepositoryServices(IServiceCollection services)
    {
        services.AddScoped<ISubscriptionsRepository, SubscriptionsRepository>();
        services.AddScoped<IPlansRepository, PlansRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<ISubscriptionLogRepository, SubscriptionLogRepository>();
        services.AddScoped<IApplicationLogRepository, ApplicationLogRepository>();
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();
        services.AddScoped<IWebhookHandler, WebHookHandler>();
        services.AddScoped<IApplicationConfigRepository, ApplicationConfigRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IOffersRepository, OffersRepository>();
        services.AddScoped<IOfferAttributesRepository, OfferAttributesRepository>();
        services.AddScoped<IPlanEventsMappingRepository, PlanEventsMappingRepository>();
        services.AddScoped<IEventsRepository, EventsRepository>();
        services.AddScoped<IEmailService, SMTPEmailService>();
        services.AddScoped<SaaSClientLogger<HomeController>>();
        services.AddScoped<IWebNotificationService, WebNotificationService>();
    }
}
