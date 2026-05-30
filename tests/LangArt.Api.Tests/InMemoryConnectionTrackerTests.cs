using LangArt.Api.Features.Realtime;

namespace LangArt.Api.Tests;

public class InMemoryConnectionTrackerTests
{
    [Fact]
    public void Adding_the_same_connection_twice_does_not_duplicate()
    {
        var tracker = new InMemoryConnectionTracker();
        var userId = Guid.NewGuid();

        tracker.AddConnection(userId, "conn-1");
        tracker.AddConnection(userId, "conn-1");

        Assert.Single(tracker.GetConnections(userId));
        Assert.True(tracker.IsOnline(userId));
    }

    [Fact]
    public void Removing_a_non_existent_connection_does_not_throw()
    {
        var tracker = new InMemoryConnectionTracker();

        var ex = Record.Exception(() => tracker.RemoveConnection("never-added"));
        Assert.Null(ex);
        Assert.Empty(tracker.GetOnlineUsers());
    }

    [Fact]
    public void Multiple_connections_per_user_are_all_tracked()
    {
        var tracker = new InMemoryConnectionTracker();
        var userId = Guid.NewGuid();

        tracker.AddConnection(userId, "conn-a");
        tracker.AddConnection(userId, "conn-b");
        tracker.AddConnection(userId, "conn-c");

        var conns = tracker.GetConnections(userId);
        Assert.Equal(3, conns.Count);
        Assert.Contains("conn-a", conns);
        Assert.Contains("conn-b", conns);
        Assert.Contains("conn-c", conns);
    }

    [Fact]
    public void IsOnline_is_correct_across_connect_and_disconnect_cycles()
    {
        var tracker = new InMemoryConnectionTracker();
        var userId = Guid.NewGuid();

        Assert.False(tracker.IsOnline(userId));

        tracker.AddConnection(userId, "tab-1");
        Assert.True(tracker.IsOnline(userId));

        tracker.AddConnection(userId, "tab-2");
        Assert.True(tracker.IsOnline(userId));
        Assert.Equal(2, tracker.GetConnections(userId).Count);

        // One tab closes — user is still online via the other tab.
        tracker.RemoveConnection("tab-1");
        Assert.True(tracker.IsOnline(userId));
        Assert.Single(tracker.GetConnections(userId));

        // Last tab closes — user goes offline.
        tracker.RemoveConnection("tab-2");
        Assert.False(tracker.IsOnline(userId));
        Assert.Empty(tracker.GetConnections(userId));
        Assert.DoesNotContain(userId, tracker.GetOnlineUsers());
    }

    [Fact]
    public void GetOnlineUsers_returns_distinct_users_with_at_least_one_connection()
    {
        var tracker = new InMemoryConnectionTracker();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();

        tracker.AddConnection(alice, "c1");
        tracker.AddConnection(alice, "c2");      // alice on two tabs
        tracker.AddConnection(bob, "c3");
        tracker.AddConnection(carol, "c4");
        tracker.RemoveConnection("c4");          // carol goes offline

        var online = tracker.GetOnlineUsers().ToHashSet();
        Assert.Contains(alice, online);
        Assert.Contains(bob, online);
        Assert.DoesNotContain(carol, online);
        Assert.Equal(2, online.Count);
    }

    [Fact]
    public async Task Concurrent_adds_and_removes_do_not_corrupt_state()
    {
        // Sanity check that the locking strategy holds up under contention. Not a
        // formal stress test — we just want a deterministic "no exception, no leaks" run.
        var tracker = new InMemoryConnectionTracker();
        var userId = Guid.NewGuid();
        const int N = 200;

        var tasks = new List<Task>();
        for (int i = 0; i < N; i++)
        {
            int local = i;
            tasks.Add(Task.Run(() =>
            {
                tracker.AddConnection(userId, $"c{local}");
                tracker.RemoveConnection($"c{local}");
            }));
        }
        await Task.WhenAll(tasks);

        Assert.False(tracker.IsOnline(userId));
        Assert.Empty(tracker.GetConnections(userId));
    }
}
