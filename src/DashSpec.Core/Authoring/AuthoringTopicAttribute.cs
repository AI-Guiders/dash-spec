namespace DashSpec.Core.Authoring;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AuthoringTopicAttribute(string id, int order) : Attribute
{
    public string Id { get; } = id;

    public int Order { get; } = order;
}
