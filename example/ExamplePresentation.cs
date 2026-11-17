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
    // The window is only an entry/lifecycle adapter. Layout, state, actions and confirmation are Host-rendered.
    internal sealed class ExamplePresentation : IDisposable
    {
        public const string PointId = "phinix.example.basic.presentation.actions";
        private readonly IClientPresentationService renderer;
        private readonly IClientExtensionPointService contributions;
        private readonly ExampleState state;
        private IPhinixExtensionModule module;
        private IClientExtensionPointScope scope;
        private IClientActionPoint<ExampleActionContext> point;
        private PresentationWindow window;

        public ExamplePresentation(IClientPresentationService renderer, IClientExtensionPointService contributions, ExampleState state)
        { this.renderer = renderer; this.contributions = contributions; this.state = state; }
        internal void Start(IPhinixExtensionModule module)
        {
            Dispose(); this.module = module;
            scope = contributions.ForModule(module);
            try
            {
                point = scope.DefineActions<ExampleActionContext>(PointId);
                scope.ContributeActions<ClientThemePresentationActionContext>(ClientThemePresentationActionContext.PointId, context =>
                    context.Surface == ClientThemePresentationSurface.Editor ? new[] {
                        new ClientActionDefinition("example-counter", state.Text("click"), state.Click,
                            state.Count == int.MaxValue ? state.Text("counterFull") : null)
                    } : Array.Empty<ClientActionDefinition>());
                scope.ContributeActions<ExampleActionContext>(PointId, context => new[] {
                    new ClientActionDefinition("add", state.Text("click"), state.Click,
                        context.Count == int.MaxValue ? state.Text("counterFull") : null),
                    new ClientActionDefinition("reset", state.Text("reset"), state.ResetCount,
                        context.Count == 0 ? state.Text("contributionDisabled") : null),
                    new ClientActionDefinition("cancel", state.Text("taskCancel"), () => state.DemonstrateTask(true)),
                    new ClientActionDefinition("fail", state.Text("taskFail"), () => state.DemonstrateTask(false)),
                    new ClientActionDefinition("layout", state.Text("toggleLayout"), () => window?.ToggleLayout())
                });
            }
            catch { Dispose(); throw; }
        }
        internal void Open()
        {
            if (module == null || !state.Active || window?.IsOpen == true) return;
            var session = renderer.Open(module, "counter");
            try { window = new PresentationWindow(this, session); Find.WindowStack.Add(window); }
            catch (Exception error) { session.Dispose(); window = null; Log.Warning("[Phinix Example] Presentation open failed: " + error); }
        }
        public void Dispose()
        {
            var previous = window; window = null; if (previous != null) previous.Close();
            var owned = scope; scope = null; point = null; module = null; owned?.Dispose();
        }

        private sealed class PresentationWindow : Window
        {
            private readonly ExamplePresentation owner;
            private readonly IClientPresentationSession session;
            private long revision, activation, pointRevision = -1;
            private int count = -1;
            private string boundedDraft="0";
            private string rangeDraft = "0.5"; private float rangeValue = .5f;
            private bool hints, singleColumn;
            internal void ToggleLayout() { singleColumn = !singleColumn; inputChanged = true; }
            private string locale, task;
            private ClientActionExecutionResult lastAction;
            private string filter = "", selection, composerNote = "", integerDraft = "1";
            private bool inputChanged;

            internal PresentationWindow(ExamplePresentation owner, IClientPresentationSession session)
            {
                this.owner = owner; this.session = session;
                doCloseX = true; closeOnAccept = false; absorbInputAroundWindow = true;
                Refresh();
            }
            public override Vector2 InitialSize => new Vector2(Mathf.Min(620, UiScreenSafeArea.Current.width), Mathf.Min(580, UiScreenSafeArea.Current.height));
            public override void WindowUpdate()
            {
                base.WindowUpdate(); windowRect = UiScreenSafeArea.ClampWindow(windowRect, new Vector2(280, 240));
                if (session.IsClosed || !owner.state.Active || activation != owner.state.Generation) { Close(); return; }
                try
                {
                    string nextFilter = filter, nextSelection = selection, nextNote = composerNote, nextInteger = integerDraft; bool submit = false;
                    while (session.TryDequeueInput(out var input))
                    {
                        if (input.Id == "bounded-demo") {boundedDraft=input.Value;inputChanged=true;}
                        if (input.Id == "integer-demo") nextInteger = input.Value;
                        if (input.Id == "filter") nextFilter = input.Value;
                        if (input.Id == "action-list") nextSelection = input.Value;
                        if (input.Id == "example-feed")
                        {
                            if (input.Kind == ClientPresentationInputKind.FeedReferenceSelected) session.ScrollToRow("example-feed", input.Target);
                            else if (input.Kind == ClientPresentationInputKind.FeedBodySelected || input.Kind == ClientPresentationInputKind.FeedHeadingSelected) nextNote = input.Value == "counter-row" ? owner.state.CountText : owner.state.TaskStatus;
                        }
                        if (input.Id == "range-demo") { rangeDraft = input.Value; if (float.TryParse(rangeDraft, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= 0 && parsed <= 1) rangeValue = parsed; inputChanged = true; }
                        if (input.Id == "composer-note") { nextNote = input.Value; if (input.Kind == ClientPresentationInputKind.SubmitRequested) submit = true; }
                    }
                    inputChanged = inputChanged || nextInteger != integerDraft || submit || nextFilter != filter || nextSelection != selection || nextNote != composerNote;
                    filter = nextFilter; selection = nextSelection; composerNote = nextNote; integerDraft = nextInteger;
                    Refresh();
                    if (submit) { session.InvokeAction("phinix.example.basic.add"); session.RequestComposerFocus(); Refresh(); }
                } // Read projection/collect only on change, outside IMGUI Draw/Layout/Repaint.
                catch (Exception error) { Log.Warning("[Phinix Example] Presentation projection failed: " + error); Close(); }
            }
            private void Refresh()
            {
                var state = owner.state;
                int nextCount = state.Count; bool nextHints = state.ShowHints;
                string nextLocale = state.Locale, nextTask = state.TaskStatus;
                long nextPoints = owner.contributions.Revision;
                var nextAction = session.LastActionResult;
                if (!inputChanged && count == nextCount && hints == nextHints && locale == nextLocale && task == nextTask && pointRevision == nextPoints && ReferenceEquals(lastAction, nextAction)) return;
                long expectedActivation = state.Generation; bool nextSingleColumn = singleColumn;
                Func<bool> current = () => singleColumn == nextSingleColumn && state.Active && expectedActivation == state.Generation && nextCount == state.Count &&
                    nextHints == state.ShowHints && nextLocale == state.Locale && nextTask == state.TaskStatus && nextPoints == owner.contributions.Revision && ReferenceEquals(nextAction, session.LastActionResult);
                var actions = owner.point.Collect(new ExampleActionContext(nextCount), current);
                try
                {
                    var bindings = new List<ClientPresentationAction>();
                    foreach (var action in actions.Actions)
                    {
                        bool reset = action.OwnerId == owner.module.ExtensionId && action.Id == "reset";
                        var confirmation = reset ? new ClientPresentationConfirmation(state.Text("reset"), state.Text("confirmReset") + "\n\n" + state.CountText + "\n" + nextTask, state.Text("reset"), state.Text("cancel")) : null;
                        bindings.Add(new ClientPresentationAction(action.OwnerId + "." + action.Id, action,
                            reset ? ClientButtonKind.Danger : ClientButtonKind.Normal, confirmation).WithSelection(selection == action.OwnerId + "." + action.Id));
                    }
                    var rows = new List<ClientPresentationRow>();
                    foreach (var binding in bindings)
                        if (binding.Action.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            rows.Add(new ClientPresentationRow(binding.Id, binding.Action.Label, binding.Action.DisabledReason).WithBadges(new[] { new ClientPresentationBadge("owner", binding.Action.OwnerId).WithColor("ui.badge.managed") }));
                    if (!rows.Exists(row => row.Id == selection)) selection = null;
                    ClientPresentationAction composerAction = bindings.Find(binding => binding.Action.OwnerId == owner.module.ExtensionId && binding.Action.Id == "add");
                    bindings.Remove(composerAction);
                    var sections = new List<ClientPresentationSection>();
                    sections.Add(new ClientPresentationSection("controls", state.Text("publicControls"), state.Text("selectionOnly"),
                        fields: new[] { new ClientPresentationTextField("filter", state.Text("filter"), filter, state.Text("filter")),
                            ClientPresentationTextField.Integer("bounded-demo", state.Text("boundedQuantity"), boundedDraft, int.TryParse(boundedDraft,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var bounded)&&bounded>=0&&bounded<=100?null:state.Text("boundedQuantityInvalid")),
                            ClientPresentationTextField.Integer("integer-demo", "Int64", integerDraft, long.TryParse(integerDraft, out _) ? null : "Int64: -9223372036854775808 … 9223372036854775807"),
                            new ClientPresentationTextField("range-demo", "0–1", rangeDraft).WithSlider(new ClientPresentationSlider(0, 1, rangeValue)) },
                        list: new ClientPresentationList("action-list", rows, state.Text("noMatches"), selection).WithColors("ui.hover","phinix.example.basic.counter"),
                        region: new ClientPresentationRegion("counter-preview", new CounterPreview(nextCount))).WithPlainLayout().WithFieldColumns());
                    sections.Add(new ClientPresentationSection("counter", state.Text("tab"), state.CountText, bindings,
                        new ClientPresentationStatus(nextTask), "phinix.example.basic.panel", "phinix.example.basic.counter").WithWrappedActions().WithPlainLayout().WithActionMenus(bindings.Count == 0 ? null : new[] { new ClientPresentationActionMenu("counter-tools", state.Text("publicControls") + " · " + state.Text("hostPresentation"), bindings.ConvertAll(b => b.Id)) }));
                    sections.Add(new ClientPresentationSection("feed-preview", new ClientPresentationFeed("example-feed", new[] {
                        new ClientPresentationFeedRow("counter-row", state.Text("tab"), nextCount.ToString(), state.CountText).WithMetadataLayout(minimumWidth: 72, gap: 12).WithMetadataPlacement(ClientPresentationMetadataPlacement.AfterHeading),
                        new ClientPresentationFeedRow("task-row", state.Text("hostPresentation"), nextCount.ToString(), nextTask, state.CountText, "counter-row")
                    }, "", followEnd: false)));
                    sections.Add(new ClientPresentationSection("readonly-preview", state.Text("readOnlyList"), state.Text("readOnlyListHint"),
                        list: new ClientPresentationList("readonly-facts", new[] {
                            new ClientPresentationRow("count-fact", state.CountText).WithImage(ClientTextureImage.FromTexture(Texture2D.whiteTexture)),
                            new ClientPresentationRow("task-fact", nextTask).WithImage(ClientTextureImage.FromSource(() => Texture2D.whiteTexture, () => !session.IsClosed, null, default(ClientPresentationImageSize)))
                        }, "").WithReadOnly()).WithPlainLayout());
                    sections.Add(new ClientPresentationSection("color-preview", "RGBA", "").WithSwatches(new[] { new ClientPresentationSwatch("range", "0–1", new ClientRgba(rangeValue, .5f, .3f)), new ClientPresentationSwatch("alpha", "alpha", new ClientRgba(.3f, .7f, .9f, .5f)) }));
                    sections.Add(new ClientPresentationSection("registered-points", state.Text("registeredPoints"),
                        string.Join("\n", owner.contributions.CapturePoints().Select(p => p.Id + " · " + p.OwnerId + " · " + p.ContextType.Name + " · " + p.ContributorCount))));
                    if (nextHints) sections.Add(new ClientPresentationSection("operation-ownership", "", state.Text("operationOwnership")));
                    if (nextHints) sections.Add(new ClientPresentationSection("intro", "", state.Text("intro")));
                    if (nextHints) sections.Add(new ClientPresentationSection("persistence", "", state.Text("persistence")));
                    sections.Add(new ClientPresentationSection("fixed-footer", state.Text("tab"), state.CountText).WithPlainLayout());
                    var page = new ClientPresentationPage("counter", state.Text("hostPresentation"), ++revision, sections,
                        new ClientPresentationComposer(new ClientPresentationTextField("composer-note", "", composerNote, state.Text("composerNote")), composerAction));
                    page = singleColumn ? page.WithFixedHeaderSections(1) : page.WithSplitView(new ClientPresentationSplitView(1, state.Text("publicControls"), state.Text("hostPresentation")).WithFixedHeaders(0, 0, 1, selection ?? "none").WithFixedFooters(0, 1));
                    session.Publish(page, actions, current);
                }
                catch { actions.Dispose(); throw; }
                activation = expectedActivation; count = nextCount; hints = nextHints; locale = nextLocale; task = nextTask; pointRevision = nextPoints; lastAction = nextAction; inputChanged = false;
            }
            private sealed class CounterPreview : IClientPresentationRegion
            {
                private readonly int count;
                internal CounterPreview(int count) { this.count = count; }
                public float Measure(float width, IClientThemeReader theme) => 12;
                public void Draw(IClientPresentationDrawContext context) { context.Progress(context.Bounds, Math.Min(count, 10) / 10f); }
            }
            public override void DoWindowContents(Rect rect)
            {
                if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, 32), "⋯"))
                    Find.WindowStack.Add(new FloatMenu(ClientPresentationMenus.ToGameOptions(session.CreateActionMenuItems())));
                session.Draw(new ClientPresentationBounds(rect.x, rect.y + 40, rect.width, Mathf.Max(0, rect.height - 40)));
            }
            public override void PostClose()
            {
                try { session.Dispose(); }
                finally { if (ReferenceEquals(owner.window, this)) owner.window = null; base.PostClose(); }
            }
        }
    }
}
