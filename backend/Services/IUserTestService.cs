using System.Runtime.Serialization;

namespace Backend.Services;

[DataContract]
public sealed class UserTestData
{
    [DataMember(Name = "id")]
    public long Id { get; init; }

    [DataMember(Name = "name")]
    public string Name { get; init; } = string.Empty;

    [DataMember(Name = "createdAt")]
    public DateTime? CreatedAt { get; init; }
}

public interface IUserTestService
{
    List<UserTestData> GetLatestUsers();
}
