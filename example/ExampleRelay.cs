using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using PhinixClient.Framework;
using Utils.Framework;

namespace Phinix.Example.Basic
{
    public sealed class ExampleRelayActionContext { }

    internal sealed class ExampleRelay : IClientCommandHandler, IDisposable
    {
        internal const string PluginId = "phinix.example.basic.relay";
        private readonly ExampleState state;
        private readonly IClientMainThreadDispatcher dispatcher;
        private IClientCommandRelay relay;
        private IClientCommandCapabilities capabilities;
        private IClientUserDirectory users;
        private IDisposable requestDeclaration, announcementDeclaration;
        private volatile bool active;
        private volatile int generation, handlerMode;
        private readonly List<ClientPresentationRow> records = new List<ClientPresentationRow>();
        private ClientPresentationRow[] players = Array.Empty<ClientPresentationRow>();
        private long sequence;
        private int requestVersion, announcementVersion;
        internal string CapabilityStatus => "request · " + (RequestEnabled ? requestVersion.ToString(CultureInfo.InvariantCulture) : "off") +
            " · announcement · " + (AnnouncementEnabled ? announcementVersion.ToString(CultureInfo.InvariantCulture) : "off");
        internal long Revision { get; private set; }
        internal string Target { get; private set; } = "";
        internal string Command { get; private set; } = "request";
        internal string Version { get; private set; } = "1";
        internal string Payload { get; private set; } = "{\"caseId\":\"relay-001\"}";
        internal bool RequestEnabled => requestDeclaration != null;
        internal bool AnnouncementEnabled => announcementDeclaration != null;
        internal int Pending { get; private set; }
        internal bool Available => active && relay != null && capabilities != null;
        internal string SyncStatus => relay == null ? state.Text("relayUnavailable") :
            relay.SyncState + (string.IsNullOrEmpty(relay.SyncError) ? "" : " · " + relay.SyncError);
        internal string HandlerStatus => state.Text(handlerMode == 0 ? "relayHandlerNormal" : handlerMode == 1 ? "relayHandlerOff" : "relayHandlerThrow");
        internal IReadOnlyList<ClientPresentationRow> Records => records;
        internal IReadOnlyList<ClientPresentationRow> Players => players;
        internal string OwnUuid => users?.Uuid ?? "";
        public int Priority => 100;
        public ExampleRelay(ExampleState state, IClientMainThreadDispatcher dispatcher)
        { this.state = state; this.dispatcher = dispatcher; }

        internal void Start(ExtensionHostContext host)
        {
            Dispose();
            host.TryGetService(out relay);
            host.TryGetService(out capabilities);
            host.TryGetService(out users);
            active = true;
            RefreshPlayers();
        }
        internal void RefreshPlayers()
        {
            players = users == null ? Array.Empty<ClientPresentationRow>() : users.GetUsers(true).Take(128)
                .OrderBy(user => user.DisplayName, StringComparer.Ordinal)
                .Select(user => new ClientPresentationRow(user.Uuid, user.DisplayName, user.Uuid)).ToArray();
            Revision++;
        }
        internal void SetInput(string id, string value)
        {
            switch (id)
            {
                case "relay-target": case "relay-players": Target = value ?? ""; break;
                case "relay-command": Command = value ?? ""; break;
                case "relay-version": Version = value ?? ""; break;
                case "relay-payload": Payload = value ?? ""; break;
                default: return;
            }
            Revision++;
        }
        private int ReadVersion()
        {
            if (!int.TryParse(Version, NumberStyles.None, CultureInfo.InvariantCulture, out int version) || version < 1)
                throw new ArgumentException(state.Text("relayInvalidVersion"));
            return version;
        }
        internal void ToggleCapability(bool request)
        {
            if (!Available) return;
            try
            {
                var previous = request ? requestDeclaration : announcementDeclaration;
                if (previous != null)
                {
                    previous.Dispose();
                    if (request) requestDeclaration = null; else announcementDeclaration = null;
                }
                else
                {
                    var declaration = capabilities.Declare("phinix.example.basic", PluginId, request ? "request" : "announcement", ReadVersion());
                    if (request) { requestDeclaration = declaration; requestVersion = ReadVersion(); }
                    else { announcementDeclaration = declaration; announcementVersion = ReadVersion(); }
                }
                AddRecord("capability", (request ? "request" : "announcement") + " · " + (previous == null ? Version : "off"));
            }
            catch (Exception error) { AddRecord("local", error.Message); }
        }
        internal void CycleHandler()
        {
            handlerMode = (handlerMode + 1) % 3;
            AddRecord("handler", HandlerStatus);
        }
        internal void NewCase()
        {
            Payload = "{\"caseId\":\"" + Guid.NewGuid().ToString("N") + "\"}";
            Revision++;
        }
        internal void ClearRecords() { records.Clear(); Revision++; }
        internal void Send(bool broadcast, bool oversized = false)
        {
            if (!Available || Pending >= 64) return;
            if (!broadcast && string.IsNullOrWhiteSpace(Target)) { AddRecord("local", state.Text("relayTargetRequired")); return; }
            try
            {
                int version = ReadVersion();
                string body = oversized ? new string('x', CommandRelayProtocol.MaxPacketBytes + 1) : Payload;
                var operation = relay.SendRelay(PluginId, Command, body, broadcast ? null : Target, version);
                Pending++;
                AddRecord("send", Command + "/" + version + " → " + (broadcast ? state.Text("relayBroadcast") : Target) + " · " + body.Substring(0, Math.Min(body.Length, 256)));
                Observe(operation, generation);
            }
            catch (Exception error) { AddRecord("local", error.Message); }
        }
        internal void SendBurst()
        {
            for (int i = 0; i < 25 && Pending < 64; i++) Send(true);
        }
        private async void Observe(Task<RelayResult> operation, int ticket)
        {
            RelayResult result = null;
            string failure = null;
            try { result = await operation.ConfigureAwait(false); }
            catch (Exception error) { failure = error.GetType().Name; }
            try
            {
                dispatcher.Enqueue(() =>
                {
                    if (!active || ticket != generation) return;
                    Pending--;
                    if (result == null) { AddRecord("result", failure ?? "Unknown"); return; }
                    string detail = result.RequestMessageId + " · " + result.Status + " · " + (result.Error ?? "OK");
                    foreach (var delivery in result.Deliveries ?? new List<RelayDelivery>())
                        detail += "\n" + delivery.ConnectionId + " · " + delivery.Outcome;
                    AddRecord("result", detail);
                });
            }
            catch { /* The host may already have stopped its borrowed dispatcher. */ }
        }
        public bool CanHandleIncomingCommand(FrameworkPacket packet)
        {
            var route = packet?.CommandRoute;
            return active && handlerMode != 1 && route?.Mode == CommandProcessingMode.Relay &&
                route.PluginId == PluginId && (route.CommandType == "request" || route.CommandType == "announcement");
        }
        public ClientIncomingCommandResult HandleIncomingCommand(FrameworkPacket packet, ClientFrameworkContext context)
        {
            if (handlerMode == 2) throw new InvalidOperationException("Example Relay handler fault injection.");
            int ticket = generation;
            string detail = packet.MessageId + " · " + packet.SenderUuid + " · " + packet.CommandRoute.CommandType +
                "/" + packet.CommandRoute.Version + "\n" + packet.PayloadJson;
            dispatcher.Enqueue(() => { if (active && ticket == generation) AddRecord("receive", detail); });
            return new ClientIncomingCommandResult { Action = MessageHandlingResultAction.Handled };
        }
        private void AddRecord(string kind, string detail)
        {
            detail = detail ?? "";
            if (detail.Length > 4096) detail = detail.Substring(0, 4096);
            records.Insert(0, new ClientPresentationRow("relay-record-" + (++sequence),
                DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC · " + kind, detail));
            if (records.Count > 50) records.RemoveAt(records.Count - 1);
            Revision++;
        }
        public void Dispose()
        {
            active = false;
            generation++;
            requestDeclaration?.Dispose(); announcementDeclaration?.Dispose();
            requestDeclaration = announcementDeclaration = null;
            relay = null; capabilities = null; users = null;
            players = Array.Empty<ClientPresentationRow>();
            records.Clear(); Pending = 0; handlerMode = 0;
            Revision++;
        }
    }
}
