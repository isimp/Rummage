using System.Linq;
using Rummage.Core;
using Xunit;

namespace Rummage.Tests;

/// <summary>One grab at a time: a grab whose takes go through other players' games waits for their answers.</summary>
public class WaitingTests
{
    [Fact]
    public void WithNothingOutstandingAGrabMayStart()
    {
        var waiting = new Waiting(timeoutSeconds: 10);

        Assert.False(waiting.Busy);
    }

    [Fact]
    public void AGrabThatOnlyTookFromChestsThisGameHoldsNeverWaits()
    {
        var waiting = new Waiting(10);
        waiting.Start([], now: 0);

        Assert.False(waiting.Busy);
    }

    [Fact]
    public void TheNextGrabWaitsWhileAnyAnswerIsOutstanding()
    {
        var waiting = new Waiting(10);
        waiting.Start([(0, 1), (2, 1)], now: 0);

        waiting.Check(slot => slot == (2, 1), now: 1);

        Assert.True(waiting.Busy);
    }

    [Fact]
    public void OnceEveryAnswerHasArrivedTheNextGrabMayStart()
    {
        var waiting = new Waiting(10);
        waiting.Start([(0, 1), (2, 1)], now: 0);

        int answered = waiting.Check(slot => false, now: 1);

        Assert.False(waiting.Busy);
        Assert.Equal(2, answered);
        Assert.Empty(waiting.Unanswered);
    }

    [Fact]
    public void AnswersThatNeverComeStopHoldingUpGrabsAfterTheTimeoutAndAreReported()
    {
        var waiting = new Waiting(10);
        waiting.Start([(0, 1), (2, 1)], now: 0);

        waiting.Check(slot => slot == (0, 1), now: 5);
        Assert.True(waiting.Busy);

        waiting.Check(slot => slot == (0, 1), now: 10.5f);
        Assert.False(waiting.Busy);
        Assert.Equal([(0, 1)], waiting.Unanswered.ToArray());
    }

    [Fact]
    public void ANewGrabForgetsTheLastOnesUnansweredTakes()
    {
        var waiting = new Waiting(10);
        waiting.Start([(0, 1)], now: 0);
        waiting.Check(slot => true, now: 11);

        waiting.Start([(3, 3)], now: 12);

        Assert.Empty(waiting.Unanswered);
        Assert.True(waiting.Busy);
    }
}
