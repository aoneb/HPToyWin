namespace HPToy.Core.Objects;

public sealed class PostAction : IPostProcess
{
    private readonly Action _action;

    public PostAction(Action action) => _action = action;

    public void OnPostProcess() => _action();
}
