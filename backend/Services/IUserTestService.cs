using Org.OpenAPITools.Models;

namespace Backend.Services;

public interface IUserTestService
{
    List<UserData> GetLatestUsers();
}
