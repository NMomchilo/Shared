using Common.Persistence;

namespace ServerSupport.Tests;

public sealed class CurrentActorTests
{
    [Fact]
    public void Require_AcceptsValidatedActorAndRejectsMissingOrOversizedValues()
    {
        Assert.Equal("service-client", CurrentActor.Require(new ActorProvider("service-client")));
        Assert.Throws<InvalidOperationException>(() => CurrentActor.Require(null));
        Assert.Throws<InvalidOperationException>(() => CurrentActor.Require(new ActorProvider(" ")));
        Assert.Throws<InvalidOperationException>(() => CurrentActor.Require(new ActorProvider(new string('x', 201))));
    }

    private sealed class ActorProvider(string? actorId) : ICurrentActorProvider
    {
        public string? ActorId => actorId;
    }
}
