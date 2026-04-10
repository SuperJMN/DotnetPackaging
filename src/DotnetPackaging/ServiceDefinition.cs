namespace DotnetPackaging;

public class ServiceDefinition
{
    public Maybe<ServiceType> Type { get; private set; } = Maybe<ServiceType>.None;
    public Maybe<RestartPolicy> Restart { get; private set; } = Maybe<RestartPolicy>.None;
    public Maybe<int> RestartSec { get; private set; } = Maybe<int>.None;
    public Maybe<string> User { get; private set; } = Maybe<string>.None;
    public Maybe<string> Group { get; private set; } = Maybe<string>.None;
    public Maybe<string> After { get; private set; } = Maybe<string>.None;
    public Maybe<string> WantedBy { get; private set; } = Maybe<string>.None;
    public Maybe<IEnumerable<string>> Environment { get; private set; } = Maybe<IEnumerable<string>>.None;

    public ServiceDefinition WithType(ServiceType type)
    {
        Type = type;
        return this;
    }

    public ServiceDefinition WithRestart(RestartPolicy restart)
    {
        Restart = restart;
        return this;
    }

    public ServiceDefinition WithRestartSec(int restartSec)
    {
        if (restartSec < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(restartSec), "Must be non-negative");
        }

        RestartSec = restartSec;
        return this;
    }

    public ServiceDefinition WithUser(string user)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            throw new ArgumentException("Can't be null or empty", nameof(user));
        }

        User = user;
        return this;
    }

    public ServiceDefinition WithGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException("Can't be null or empty", nameof(group));
        }

        Group = group;
        return this;
    }

    public ServiceDefinition WithAfter(string after)
    {
        if (string.IsNullOrWhiteSpace(after))
        {
            throw new ArgumentException("Can't be null or empty", nameof(after));
        }

        After = after;
        return this;
    }

    public ServiceDefinition WithWantedBy(string wantedBy)
    {
        if (string.IsNullOrWhiteSpace(wantedBy))
        {
            throw new ArgumentException("Can't be null or empty", nameof(wantedBy));
        }

        WantedBy = wantedBy;
        return this;
    }

    public ServiceDefinition WithEnvironment(params string[] variables)
    {
        if (variables == null || variables.Length == 0)
        {
            throw new ArgumentException("At least one variable is required", nameof(variables));
        }

        Environment = Maybe<IEnumerable<string>>.From(variables);
        return this;
    }
}
