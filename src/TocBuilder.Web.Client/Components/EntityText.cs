using TocBuilder.Domain;

namespace TocBuilder.Web.Client.Components;

// How an Entity is named in a list: its Code, then its Summary, or its statement when it has no Summary.
public static class EntityText
{
    public static string Label(Entity entity) => $"{entity.Code}: {entity.Summary ?? entity.Statement}";
}
