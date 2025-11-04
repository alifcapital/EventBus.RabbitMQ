using System.Diagnostics;

namespace EventBus.RabbitMQ.Extensions;

internal static class ActivityExtensions
{
    /// <summary>
    /// Adds the given tags to the activity.
    /// </summary>
    /// <param name="activity">The activity to attach information to.</param>
    /// <param name="tags">The tags to add to the activity.</param>
    public static void AddTags(this Activity activity, Dictionary<string, object> tags)
    {
        if (activity == null)
            return;

        foreach (var tag in tags)
            activity.SetTag(tag.Key, tag.Value);
    }
}