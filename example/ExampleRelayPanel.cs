using System;
using System.Collections.Generic;
using System.Linq;
using PhinixClient;
using PhinixClient.Framework;
using UnityEngine;
using Utils.Framework;
using Verse;

namespace Phinix.Example.Basic
{
    internal sealed class ExampleRelayPanel : IDisposable
    {
        private readonly ExampleState state;
        private readonly ExampleRelay relay;
        private readonly IClientPresentationService renderer;
        private readonly IClientExtensionPointService contributions;
        private IPhinixExtensionModule module;
        private IClientExtensionPointScope scope;
        private IClientActionPoint<ExampleRelayActionContext> point;
        private RelayWindow window;
        public ExampleRelayPanel(ExampleState state, ExampleRelay relay, IClientPresentationService renderer,
            IClientExtensionPointService contributions)
        { this.state = state; this.relay = relay; this.renderer = renderer; this.contributions = contributions; }
        internal void Start(IPhinixExtensionModule owner)
        {
            Dispose(); module = owner;
            scope = contributions.ForModule(owner);
            try
            {
                const string pointId = "phinix.example.basic.relay-actions";
                point = scope.DefineActions<ExampleRelayActionContext>(pointId);
                scope.ContributeActions<ExampleRelayActionContext>(pointId, _ => new[]
                {
                    new ClientActionDefinition("unicast", state.Text("relayUnicast"), () => relay.Send(false), relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("broadcast", state.Text("relayBroadcast"), () => relay.Send(true), relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("request", state.Text(relay.RequestEnabled ? "relayRevokeRequest" : "relayDeclareRequest"), () => relay.ToggleCapability(true), relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("announcement", state.Text(relay.AnnouncementEnabled ? "relayRevokeAnnouncement" : "relayDeclareAnnouncement"), () => relay.ToggleCapability(false), relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("handler", state.Text("relayChangeHandler"), relay.CycleHandler),
                    new ClientActionDefinition("case", state.Text("relayNewCase"), relay.NewCase),
                    new ClientActionDefinition("players", state.Text("relayRefreshPlayers"), relay.RefreshPlayers),
                    new ClientActionDefinition("oversized", state.Text("relayOversized"), () => relay.Send(true, true), relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("burst", state.Text("relayBurst"), relay.SendBurst, relay.Available ? null : state.Text("relayUnavailable")),
                    new ClientActionDefinition("clear", state.Text("relayClear"), relay.ClearRecords)
                });
            }
            catch { Dispose(); throw; }
        }
        internal void Open()
        {
            if (module == null || !state.Active || window?.IsOpen == true) return;
            relay.RefreshPlayers();
            var session = renderer.Open(module, "relay-tests");
            try { window = new RelayWindow(this, session); Find.WindowStack.Add(window); }
            catch { session.Dispose(); window = null; throw; }
        }
        public void Dispose()
        {
            var closing = window; window = null; closing?.Close();
            point = null; module = null;
            var owned = scope; scope = null; owned?.Dispose();
        }
        internal static ClientPresentationSection[] BuildSections(ExampleState state, ExampleRelay relay,
            IEnumerable<ClientPresentationAction> bindings)
        {
            string sync = relay.SyncStatus;
            return new[]
            {
                new ClientPresentationSection("relay-status", state.Text("relayTitle"),
                    state.Text("relaySync") + " · " + sync + "\n" + state.Text("relayOwnId") + " · " + relay.OwnUuid +
                    "\n" + relay.CapabilityStatus + "\n" + relay.HandlerStatus + " · " + state.Text("relayPending") + " " + relay.Pending),
                new ClientPresentationSection("relay-form", state.Text("relaySendTitle"), state.Text("relayHint"), bindings,
                    fields: new[]
                    {
                        new ClientPresentationTextField("relay-target", state.Text("relayTarget"), relay.Target, maxLength: 256),
                        new ClientPresentationTextField("relay-command", state.Text("relayCommand"), relay.Command, maxLength: 128),
                        new ClientPresentationTextField("relay-version", state.Text("relayVersion"), relay.Version, maxLength: 10),
                        new ClientPresentationTextField("relay-payload", state.Text("relayPayload"), relay.Payload, maxLength: 4096)
                    }).WithWrappedActions(),
                new ClientPresentationSection("relay-users", state.Text("relayPlayers"), state.Text("relayPlayersHint"),
                    list: new ClientPresentationList("relay-players", relay.Players, state.Text("relayNoPlayers"), relay.Players.FirstOrDefault(row => row.Id == relay.Target)?.Id)),
                new ClientPresentationSection("relay-records", state.Text("relayRecords"), state.Text("relayResultHint"),
                    list: new ClientPresentationList("relay-record-list", relay.Records, state.Text("relayNoRecords")).WithReadOnly())
            };
        }

        private sealed class RelayWindow : Window
        {
            private readonly ExampleRelayPanel owner;
            private readonly IClientPresentationSession session;
            private readonly long activation;
            private long publishedRevision = -1, pageRevision;
            private string publishedSync, publishedLocale;
            internal RelayWindow(ExampleRelayPanel owner, IClientPresentationSession session)
            {
                this.owner = owner; this.session = session; activation = owner.state.Generation;
                doCloseX = true; closeOnAccept = false; absorbInputAroundWindow = true;
                Refresh();
            }
            public override Vector2 InitialSize => new Vector2(Mathf.Min(860, UiScreenSafeArea.Current.width), Mathf.Min(700, UiScreenSafeArea.Current.height));
            public override void WindowUpdate()
            {
                base.WindowUpdate();
                windowRect = UiScreenSafeArea.ClampWindow(windowRect, new Vector2(320, 280));
                if (session.IsClosed || !owner.state.Active || activation != owner.state.Generation) { Close(); return; }
                while (session.TryDequeueInput(out var input)) owner.relay.SetInput(input.Id, input.Value);
                Refresh();
            }
            private void Refresh()
            {
                var relay = owner.relay;
                var state = owner.state;
                string sync = relay.SyncStatus;
                if (publishedRevision == relay.Revision && publishedSync == sync && publishedLocale == state.Locale) return;
                long revision = relay.Revision;
                string locale = state.Locale;
                Func<bool> current = () => !session.IsClosed && state.Active && activation == state.Generation &&
                    revision == relay.Revision && sync == relay.SyncStatus && locale == state.Locale;
                var actions = owner.point.Collect(new ExampleRelayActionContext(), current);
                try
                {
                    var bindings = new List<ClientPresentationAction>();
                    foreach (var action in actions.Actions)
                        bindings.Add(new ClientPresentationAction(action.OwnerId + "." + action.Id, action));
                    var sections = BuildSections(state, relay, bindings);
                    session.Publish(new ClientPresentationPage("relay-tests", state.Text("relayTitle"), ++pageRevision, sections), actions, current);
                }
                catch { actions.Dispose(); throw; }
                publishedRevision = revision; publishedSync = sync; publishedLocale = locale;
            }
            public override void DoWindowContents(Rect rect)
            { session.Draw(new ClientPresentationBounds(rect.x, rect.y, rect.width, rect.height)); }
            public override void PostClose()
            {
                try { session.Dispose(); }
                finally { if (ReferenceEquals(owner.window, this)) owner.window = null; base.PostClose(); }
            }
        }
    }
}
