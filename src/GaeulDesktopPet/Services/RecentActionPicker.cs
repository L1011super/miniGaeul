using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public sealed class RecentActionPicker
{
    private readonly Queue<string> _recent = new();
    private readonly Random _random = new();

    public AnimationDefinition Pick(IReadOnlyList<AnimationDefinition> actions)
    {
        if (actions.Count == 0) throw new ArgumentException("At least one action is required.", nameof(actions));
        var candidates = actions.Where(action => !_recent.Contains(action.Name)).ToList();
        if (candidates.Count == 0) candidates = actions.ToList();
        var selected = candidates[_random.Next(candidates.Count)];
        _recent.Enqueue(selected.Name);
        while (_recent.Count > 2) _recent.Dequeue();
        return selected;
    }
}
