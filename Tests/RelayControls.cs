using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Utils.Framework;

internal static partial class Program
{
    private sealed class RelayDirectory : PhinixClient.Framework.IClientUserDirectory
    {
        public string Uuid => "local-player";
        public UserManagement.ImmutableUser[] GetUsers(bool loggedIn = false) => new[] { new UserManagement.ImmutableUser("target-player", "Target", true) };
        public bool TryGetUser(string uuid, out UserManagement.ImmutableUser user)
        { user = new UserManagement.ImmutableUser(uuid); return uuid == "target-player"; }
    }
    private static PhinixClient.Framework.ClientPresentationSection[] RelaySections(object controller)
    {
        var state = controller.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var panel = typeof(Phinix.Example.Basic.ExampleExtension).Assembly.GetType("Phinix.Example.Basic.ExampleRelayPanel");
        return (PhinixClient.Framework.ClientPresentationSection[])panel.GetMethod("BuildSections", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { state, controller, Array.Empty<PhinixClient.Framework.ClientPresentationAction>() });
    }
    private sealed class RelayService : IClientCommandRelay, IDisposable
    {
        public CapabilitySyncState SyncState => CapabilitySyncState.Synced;
        public string SyncError => null;
        internal string Target, Command, Payload;
        internal int Version;
        internal bool Disposed;
        private TaskCompletionSource<RelayResult> pending;
        public Task<RelayResult> SendRelay(string pluginId, string commandType, string payloadJson, string targetPlayerUuid = null, int version = 1)
        {
            Target = targetPlayerUuid; Command = commandType; Payload = payloadJson; Version = version;
            pending = new TaskCompletionSource<RelayResult>();
            return pending.Task;
        }
        internal void Finish()
        {
            pending?.TrySetResult(new RelayResult { RequestMessageId = "test-message", Status = "Submitted",
                Deliveries = new List<RelayDelivery> { new RelayDelivery { ConnectionId = "test-target", Outcome = "Submitted" } } });
        }
        public void Dispose() { Disposed = true; }
    }
    private static void RelayCall(object controller, string name, params object[] arguments)
    {
        controller.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments);
    }
    private static int RelayRecords(object controller) =>
        ((IEnumerable)controller.GetType().GetProperty("Records", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller)).Cast<object>().Count();
    private static void AssertRelayControls(IClientCommandHandler handler, ClientCommandCapabilities declarations, RelayService relay, Dispatcher dispatcher)
    {
        Check(declarations.GetSnapshot().Count == 0, "Example starts with capabilities explicitly off.");
        Check(RelaySections(handler).Single(section => section.Id == "relay-users").List.SelectedId == null,
            "Panel with no selected target produces valid public presentation.");
        RelayCall(handler, "SetInput", "relay-target", "offline target!");
        var offline = RelaySections(handler);
        Check(offline.Single(section => section.Id == "relay-users").List.SelectedId == null &&
            offline.Single(section => section.Id == "relay-form").Fields.Single(field => field.Id == "relay-target").Value == "offline target!",
            "Manual offline or invalid target stays in field without invalid list selection.");
        RelayCall(handler, "ToggleCapability", true);
        RelayCall(handler, "ToggleCapability", false);
        Check(declarations.GetSnapshot().Count == 2, "Independent request and announcement controls declare both capabilities.");
        RelayCall(handler, "ToggleCapability", true);
        Check(declarations.GetSnapshot().Single().CommandType == "announcement", "Revoking request retains announcement.");
        RelayCall(handler, "SetInput", "relay-version", "2");
        RelayCall(handler, "ToggleCapability", true);
        Check(declarations.GetSnapshot().Single(c => c.CommandType == "request").Version == 2, "Declaration uses chosen version.");
        RelayCall(handler, "SetInput", "relay-target", "target-player");
        Check(RelaySections(handler).Single(section => section.Id == "relay-users").List.SelectedId == "target-player",
            "Online target is highlighted in public list.");
        RelayCall(handler, "SetInput", "relay-payload", "{\"caseId\":\"test-1\"}");
        RelayCall(handler, "Send", false, false);
        Check(relay.Target == "target-player" && relay.Command == "request" && relay.Version == 2 && relay.Payload.Contains("test-1"), "Unicast preserves target, type, version and payload.");
        relay.Finish(); dispatcher.Drain();
        RelayCall(handler, "SetInput", "relay-command", "announcement");
        RelayCall(handler, "Send", true, false);
        Check(relay.Target == null && relay.Command == "announcement", "Broadcast uses explicit Relay default target.");
        relay.Finish(); dispatcher.Drain();
        var packet = new FrameworkPacket { MessageId = "receive-1", SenderUuid = "sender-player", PayloadJson = "{\"caseId\":\"receive\"}",
            CommandRoute = new CommandRoute { Mode = CommandProcessingMode.Relay, PluginId = "phinix.example.basic.relay", CommandType = "request", Version = 2 } };
        Check(handler.CanHandleIncomingCommand(packet), "Normal receiving mode matches plugin commands.");
        RelayCall(handler, "CycleHandler");
        Check(!handler.CanHandleIncomingCommand(packet), "Paused handler keeps declarations independent.");
        Check(declarations.GetSnapshot().Count == 2, "Pausing handler does not revoke capabilities.");
        RelayCall(handler, "CycleHandler");
        bool threw = false;
        try { handler.HandleIncomingCommand(packet, null); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "Fault mode exercises actual framework handler exception boundary.");
        RelayCall(handler, "CycleHandler");
        for (int i = 0; i < 60; i++) handler.HandleIncomingCommand(packet, null);
        dispatcher.Drain();
        Check(RelayRecords(handler) == 50, "Receive history remains bounded to 50 rows.");
        RelayCall(handler, "ClearRecords");
        Check(RelayRecords(handler) == 0, "Clear affects only local history.");
        RelayCall(handler, "Send", true, true);
        Check(relay.Payload.Length > CommandRelayProtocol.MaxPacketBytes, "Oversize action reaches public API with oversized payload.");
        // Keep this last completion pending so the lifecycle test can exercise late publication after stop.
    }
}
