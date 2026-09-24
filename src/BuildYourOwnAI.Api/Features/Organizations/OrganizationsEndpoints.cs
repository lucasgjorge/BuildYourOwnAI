using BuildYourOwnAI.Api.Features.Documents;

namespace BuildYourOwnAI.Api.Features.Organizations;

public static class OrganizationsEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationsEndpoints(this IEndpointRouteBuilder app)
    {
        var organizations = app.MapGroup("/api/organizations").RequireAuthorization().WithTags("Organizations");

        CreateOrganization.Map(organizations);
        ListOrganizations.Map(organizations);
        GetOrganization.Map(organizations);
        DeleteOrganization.Map(organizations);

        UploadDocument.Map(organizations);
        ListDocuments.Map(organizations);
        DeleteDocument.Map(organizations);

        return app;
    }
}
