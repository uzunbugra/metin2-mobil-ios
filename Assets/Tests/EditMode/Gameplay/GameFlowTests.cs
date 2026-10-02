using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Metin2.Gameplay.Demo;
using Metin2.Gameplay.Flow;
using Metin2.Network.Session;
using Metin2.Protocol.Constants;
using Metin2.Protocol.Packets;

namespace Metin2.Tests.EditMode.Gameplay
{
    /// <summary>
    /// End-to-end integration: the real session clients driven by
    /// <see cref="GameFlow"/> against the in-process <see cref="DemoServer"/>
    /// (genuine DH2 + cipher, faithful frame order with GC_PHASE pushes and
    /// keepalive pings). This is the offline proxy for the live-server smoke
    /// test and the feed the Unity front-end will bind to.
    /// </summary>
    [TestFixture]
    public class GameFlowTests
    {
        private const int TestTimeoutMs = 15000;

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = TestTimeoutMs)
        {
            var deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            return condition();
        }

        private sealed class EventSink
        {
            public readonly ConcurrentQueue<PhaseType> Phases = new ConcurrentQueue<PhaseType>();
            public readonly ConcurrentQueue<GameFlowState> States = new ConcurrentQueue<GameFlowState>();
            public readonly ConcurrentQueue<PacketGCCharacterAdd> Spawns = new ConcurrentQueue<PacketGCCharacterAdd>();
            public readonly ConcurrentQueue<uint> Despawns = new ConcurrentQueue<uint>();
            public readonly ConcurrentQueue<ItemEvent> Items = new ConcurrentQueue<ItemEvent>();
            public readonly ConcurrentQueue<CombatEvent> Combats = new ConcurrentQueue<CombatEvent>();
            public readonly ConcurrentQueue<PacketGCMove> Moves = new ConcurrentQueue<PacketGCMove>();
            public readonly ConcurrentQueue<PacketGCSyncPosition> Syncs = new ConcurrentQueue<PacketGCSyncPosition>();

            public void Attach(GameFlow flow)
            {
                flow.ServerPhaseChanged += Phases.Enqueue;
                flow.StateChanged += States.Enqueue;
                flow.EntitySpawned += Spawns.Enqueue;
                flow.EntityDespawned += Despawns.Enqueue;
                flow.ItemChanged += Items.Enqueue;
                flow.CombatEventReceived += Combats.Enqueue;
                flow.EntityMoved += Moves.Enqueue;
                flow.PositionsSynced += Syncs.Enqueue;
            }

            public IReadOnlyList<T> Drain<T>(ConcurrentQueue<T> queue)
            {
                var result = new List<T>();
                while (queue.TryDequeue(out T item))
                {
                    result.Add(item);
                }

                return result;
            }
        }

        [Test]
        public async Task FullJourney_AgainstDemoServer_EndToEnd()
        {
            using var demo = new DemoServer();
            (int authPort, int gamePort) = demo.Start();
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            var sink = new EventSink();
            using var flow = new GameFlow();
            sink.Attach(flow);

            // --- Auth hop ---
            AuthLoginResult login = await flow.LoginAsync("127.0.0.1", authPort, "demo", "demo", token).ConfigureAwait(false);
            Assert.IsTrue(login.Succeeded, login.ToString());
            Assert.AreEqual(demo.IssuedLoginKey, flow.LoginKey);
            Assert.AreEqual(GameFlowState.LoggedIn, flow.State);

            // --- Channel hop (interleaved GC_PHASE pushes consumed transparently) ---
            ChannelSelectData slots = await flow.ConnectChannelAsync("127.0.0.1", gamePort, "demo", token).ConfigureAwait(false);
            Assert.AreEqual(1, slots.Empire);
            Assert.AreEqual("Democius", slots.Players[0].Name);
            Assert.AreEqual(42, slots.Players[0].Level);
            Assert.AreEqual(0u, slots.Players[1].Id); // empty slot

            (IPAddress addr, ushort port) = ChannelLoginClient.GetSlotEndpoint(slots.Players[0]);
            Assert.AreEqual(IPAddress.Loopback, addr);
            Assert.AreEqual((ushort)gamePort, port);

            // --- Select + loading bundle ---
            PacketGCMainCharacter main = await flow.SelectCharacterAsync(0, 2, token).ConfigureAwait(false);
            Assert.AreEqual(DemoServer.MainCharacterVid, main.Vid);
            Assert.AreEqual("Democius", main.Name);
            Assert.AreEqual(42, flow.Stats.Points[PointTypes.Level]);
            Assert.AreEqual(1000, flow.Stats.Points[PointTypes.Hp]);
            Assert.AreEqual(GameFlowState.CharacterReady, flow.State);

            // The two loading items were raised via ItemChanged.
            Assert.IsTrue(await WaitUntilAsync(() => sink.Items.Count >= 2).ConfigureAwait(false),
                "Loading items did not arrive.");
            var loadingItems = sink.Drain(sink.Items);
            Assert.AreEqual(2, loadingItems.Count);
            Assert.AreEqual(19u, loadingItems[0].Set.Vnum);
            Assert.AreEqual(11243u, loadingItems[1].Set.Vnum);

            // --- Enter world ---
            WorldEntryData entry = await flow.EnterWorldAsync(token).ConfigureAwait(false);
            Assert.AreEqual(DemoServer.MainCharacterVid, entry.Vid);
            Assert.AreEqual(1, entry.Channel);
            Assert.Greater(entry.ServerTime, 0u);
            Assert.AreEqual(GameFlowState.InWorld, flow.State);

            // --- Event pump + scripted world rules ---
            Task pump = Task.Run(() => flow.RunEventPumpAsync(token), token);

            // Two spawns pushed right after entry.
            Assert.IsTrue(await WaitUntilAsync(() => sink.Spawns.Count >= 2).ConfigureAwait(false),
                "Spawns did not arrive.");
            var spawns = sink.Drain(sink.Spawns);
            Assert.AreEqual(DemoServer.SpawnedMobVid, spawns[0].Vid);
            Assert.AreEqual(DemoServer.SpawnedPlayerVid, spawns[1].Vid);

            // Attack #1 on the mob: damage number + HP delta + motion.
            await flow.Combat.SendAttackAsync(DemoServer.SpawnedMobVid, 0, token).ConfigureAwait(false);
            Assert.IsTrue(await WaitUntilAsync(() => sink.Combats.Count >= 3).ConfigureAwait(false),
                "Attack #1 results did not arrive.");
            var firstHits = sink.Drain(sink.Combats);
            Assert.AreEqual(CombatEvent.Kind.DamageInfo, firstHits[0].EventKind);
            Assert.AreEqual(DemoServer.SpawnedMobVid, firstHits[0].Damage.Vid);
            Assert.AreEqual(CombatEvent.Kind.PointChanged, firstHits[1].EventKind);
            Assert.AreEqual(PointTypes.Hp, firstHits[1].PointChange.Type);
            Assert.AreEqual(CombatEvent.Kind.Motion, firstHits[2].EventKind);

            // Attack #2 kills the mob: dead event.
            await flow.Combat.SendAttackAsync(DemoServer.SpawnedMobVid, 0, token).ConfigureAwait(false);
            Assert.IsTrue(await WaitUntilAsync(() => sink.Combats.Count >= 4).ConfigureAwait(false),
                "Death event did not arrive.");
            var secondHits = sink.Drain(sink.Combats);
            Assert.AreEqual(CombatEvent.Kind.Dead, secondHits[3].EventKind);
            Assert.AreEqual(DemoServer.SpawnedMobVid, secondHits[3].Dead.Vid);
            Assert.AreEqual(2, demo.AttacksReceived);

            // Move intent → server-authoritative rebroadcast.
            await flow.Movement.SendMoveAsync(0, 0, 90f, 960000, 269600, 12345, token).ConfigureAwait(false);
            Assert.IsTrue(await WaitUntilAsync(() => sink.Moves.Count >= 1).ConfigureAwait(false),
                "Move rebroadcast did not arrive.");
            var moves = sink.Drain(sink.Moves);
            Assert.AreEqual(DemoServer.MainCharacterVid, moves[0].Vid);
            Assert.AreEqual(960000, moves[0].X);
            Assert.AreEqual(1, demo.MovesReceived);

            // Item move: server answers with del + set at the destination cell.
            await flow.Inventory.SendMoveItemAsync(1, 0, 1, 2, 0, token).ConfigureAwait(false);
            Assert.IsTrue(await WaitUntilAsync(() => sink.Items.Count >= 2).ConfigureAwait(false),
                "Item move results did not arrive.");
            var itemMoves = sink.Drain(sink.Items);
            Assert.AreEqual(ItemEvent.Kind.Cleared, itemMoves[0].EventKind);
            Assert.AreEqual(0, itemMoves[0].Cell);
            Assert.AreEqual(ItemEvent.Kind.Set, itemMoves[1].EventKind);
            Assert.AreEqual(2, itemMoves[1].Cell);
            Assert.AreEqual(19u, itemMoves[1].Set.Vnum);
            Assert.AreEqual(1, demo.ItemMovesReceived.Count);

            cts.Cancel();
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Pump torn down by the token — expected.
            }

            // --- Phase and state sequences (server pushes GC_PHASE on every SetPhase) ---
            CollectionAssert.AreEqual(
                new[]
                {
                    PhaseType.Auth, PhaseType.Login, PhaseType.Select,
                    PhaseType.Loading, PhaseType.Game
                },
                sink.Drain(sink.Phases));
            CollectionAssert.AreEqual(
                new[]
                {
                    GameFlowState.LoggedIn, GameFlowState.ChannelReady,
                    GameFlowState.CharacterReady, GameFlowState.InWorld
                },
                sink.Drain(sink.States));
        }

        [Test]
        public async Task WrongCredentials_AuthFailsWithStatus()
        {
            using var demo = new DemoServer();
            (int authPort, _) = demo.Start();
            using var cts = new CancellationTokenSource(TestTimeoutMs);
            CancellationToken token = cts.Token;

            using var flow = new GameFlow();
            AuthLoginResult result = await flow.LoginAsync("127.0.0.1", authPort, "demo", "wrong", token).ConfigureAwait(false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("WRONGPWD", result.Status);
            Assert.AreEqual(GameFlowState.Idle, flow.State);
            Assert.AreEqual(0u, flow.LoginKey);
        }

        [Test]
        public void ConnectChannel_BeforeLogin_Throws()
        {
            using var flow = new GameFlow();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await flow.ConnectChannelAsync("127.0.0.1", 1, "demo").ConfigureAwait(false));
        }
    }
}
