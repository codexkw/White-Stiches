using Microsoft.Extensions.DependencyInjection;
using WhiteStiches.Core.Interfaces.Admin;
using WhiteStiches.Infrastructure.Services.Admin;

namespace WhiteStiches.Infrastructure;

public static class AdminServicesRegistration
{
    /// <summary>
    /// Back-office-only services. Called by WhiteStiches.Admin on top of
    /// AddWhiteStichesInfrastructure; the storefront never needs these.
    /// </summary>
    public static IServiceCollection AddWhiteStichesAdminServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderAdminService, OrderAdminService>();
        services.AddScoped<IReturnAdminService, ReturnAdminService>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<ICollectionAdminService, CollectionAdminService>();
        services.AddScoped<IStaffAdminService, StaffAdminService>();

        return services;
    }
}
