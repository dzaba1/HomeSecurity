namespace Dzaba.Org.Contracts;

public interface IOrganizationsService
{
    Task<Organization> CreateOrgAsync(CreateOrganization organization);
}