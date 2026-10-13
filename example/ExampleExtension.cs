using System;
using System.Threading;
using PhinixClient;
using PhinixClient.Framework;
using UnityEngine;
using Utils;
using Utils.Framework;
using Verse;

namespace Phinix.Example.Basic
{
    [PhinixExtension("phinix.example.basic")]
    public sealed class ExampleExtension : ClientExtensionModule, IActivatablePhinixExtensionModule
    {
        private IClientCompositionScope scope;
        private ExampleState state;
        public override string ExtensionId => "phinix.example.basic";
        public override void Compose(IExtensionBuilder builder)
        {
            if(scope!=null) throw new InvalidOperationException("Example is already composed.");
            var host=builder.HostContext;
            scope=host.GetRequiredService<IClientCompositionFactory>().CreateScope(local=>
            {
                local.Borrow(host.GetRequiredService<IClientSettingsContext>());
                local.Borrow(host.GetRequiredService<IClientTheme>());
                local.Borrow(host.GetRequiredService<IClientThemeRegistry>());
                local.Register<ExampleTheme,ExampleTheme>();
                local.Borrow(host.GetRequiredService<IClientApiDirectory>());
                local.Borrow(host.GetRequiredService<IClientLifetimeService>());
                local.Register<ExampleAuthorServices,ExampleAuthorServices>();
                local.Borrow(host.GetRequiredService<IClientExtensionPointService>());
                local.Register<ExampleActions,ExampleActions>();
                local.Borrow(host.GetRequiredService<IClientPresentationService>());
                local.Register<ExamplePresentation,ExamplePresentation>();
                local.Borrow<Action<string,LogLevel>>(host.Log??((message,level)=>{}));
                local.Register<ExampleState,ExampleState>();
                local.Register<IMainTabProvider,ExampleTab>();
                local.Register<IClientSettingsPanelProvider,ExampleSettingsPanel>();
            });
            try
            {
                state=scope.Resolve<ExampleState>();
                var tab=(ExampleTab)scope.Resolve<IMainTabProvider>();
                builder.RegisterApi<IMainTabProvider>(new ClientPresentationMainTab(tab,tab.ViewOpened,tab.ViewUpdate,tab.ViewClosed,tab.NavigationId));
                builder.RegisterApi(scope.Resolve<IClientSettingsPanelProvider>());
            }
            catch { Shutdown(host); throw; }
        }
        public void Activate(ExtensionHostContext hostContext)
        {
            if(state.Active) return;
            scope.Resolve<ExampleTheme>().Start(this);
            var localizer=hostContext.GetRequiredService<IClientLocalizationService>().ForModule(this);
            IClientTaskScope tasks=null;
            try
            {
                tasks=hostContext.GetRequiredService<IClientTaskService>().ForModule(this);
                state.Start(localizer,tasks);
                scope.Resolve<ExampleActions>().Start(this);
                scope.Resolve<ExamplePresentation>().Start(this);
                scope.Resolve<ExampleAuthorServices>().Start(this);
            }
            catch { tasks?.Dispose(); localizer.Dispose(); throw; }
        }
        public void Shutdown(ExtensionHostContext hostContext)
        { var owned=scope; scope=null; state=null; owned?.Dispose(); }
    }

    // One state shared by the tab and settings provider, with package-prefixed settings keys.
    // The host calls lifecycle/UI methods on the main thread. No game/map/item API is used.
    internal sealed class ExampleState : IDisposable
    {
        private readonly IClientSettingsContext settings;
        private IClientTaskScope tasks;
        private IClientTaskHandle lastTask;
        private IClientLocalizer localizer;
        private readonly Action<string, LogLevel> log;
        public ExampleState(IClientSettingsContext settings,Action<string,LogLevel> log)
        { this.settings=settings; this.log=log; }
        private long generation;
        internal bool Active => localizer != null && tasks != null;
        internal long Generation => generation;
        internal string Locale => localizer?.Locale ?? "";
        internal int Count => Math.Max(0, settings?.Get("phinix.example.basic.clicks", 0) ?? 0);
        internal bool ShowHints => settings?.Get("phinix.example.basic.showHints", true) ?? true;
        internal string Text(string key) { return localizer?.Text(key) ?? key; }
        internal string CountText => localizer?.Format("count", Count) ?? Count.ToString();
        internal void Start(IClientLocalizer localization,IClientTaskScope taskScope)
        {
            Stop(); localizer = localization; tasks=taskScope;
            localizer.LanguageChanged += LanguageChanged;
            log?.Invoke("Example: activated; tab and settings registered through public extension APIs.", LogLevel.INFO);
        }
        private void LanguageChanged() { log?.Invoke("Example: language changed; locale=" + localizer?.Locale, LogLevel.INFO); }
        internal void Stop()
        {
            generation++;
            var ownedTasks=tasks; tasks=null; ownedTasks?.Dispose(); lastTask=null;
            if (localizer != null) { localizer.LanguageChanged -= LanguageChanged; localizer.Dispose(); localizer = null; }
            log?.Invoke("Example: shutdown; localization subscription and callbacks cleared.", LogLevel.INFO);
        }
        public void Dispose() { Stop(); }
        internal void Click()
        {
            if (!Active) return;
            int count = Count;
            if (count < int.MaxValue) settings.Set("phinix.example.basic.clicks", count + 1);
            long ticket=generation;
            lastTask=tasks.Submit(()=>
            {
                if(!Active || ticket!=generation) return;
                log?.Invoke("Example: main-thread callback; click count=" + Count + ".",LogLevel.INFO);
            });
        }
        internal string TaskStatus
        {
            get
            {
                var result=lastTask?.Capture();
                return result==null?Text("taskNone"):Text("task"+result.State)+" · "+result.Code;
            }
        }
        internal void DemonstrateTask(bool cancel)
        {
            if(!Active) return;
            if(cancel)
            {
                using(var request=new CancellationTokenSource())
                {
                    request.Cancel();
                    lastTask=tasks.Submit(()=>log?.Invoke("Example: canceled callback unexpectedly ran.",LogLevel.ERROR),request.Token);
                }
            }
            else lastTask=tasks.Submit(()=>{ throw new InvalidOperationException("Example callback failure demonstration."); });
        }
        internal void SetHints(bool value)
        {
            if (!Active || value == ShowHints) return;
            settings.Set("phinix.example.basic.showHints", value);
            log?.Invoke("Example: show hints=" + value + ".", LogLevel.INFO);
        }
        internal void ResetCount()
        {
            if (!Active) return;
            settings.Set("phinix.example.basic.clicks", 0);
            log?.Invoke("Example: click counter reset.", LogLevel.INFO);
        }
        internal void ConfirmReset()
        {
            if (!Active) return;
            long ticket = generation;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Text("confirmReset"), () =>
            {
                // A confirmation from a previous activation cannot change retained settings.
                if (!Active || ticket != generation) return;
                ResetCount();
            }));
        }
    }

    internal sealed class ExampleTab : IMainTabProvider, IResponsiveMainTabProvider, IClientNavigationMetadata
    {
        private int viewOpens,viewCloses;private bool viewVisible;
        internal void ViewOpened(){viewVisible=true;viewOpens++;}
        internal void ViewUpdate(){ }
        internal void ViewClosed(){viewVisible=false;viewCloses++;}
        private readonly ExampleState state;
        private readonly ExampleTheme theme;
        private readonly ExampleActions actions;
        private readonly ExampleAuthorServices authorServices;
        private readonly ExamplePresentation presentation;
        private static readonly UiLayoutHints Hints = new UiLayoutHints(new Vector2(320f, 240f), new Vector2(820f, 480f), true);
        public string NavigationId => "example";
        public ExampleTab(ExampleState state, ExampleTheme theme, ExampleActions actions,ExampleAuthorServices authorServices, ExamplePresentation presentation) { this.state = state; this.theme=theme; this.actions=actions;this.authorServices=authorServices; this.presentation=presentation; }
        public string TabLabel => state.Text("tab");
        public float TabOrder => 998f;
        public UiLayoutHints LayoutHints => Hints;
        public void Draw(Rect inRect)
        {
            if (!state.Active || inRect.width <= 0 || inRect.height <= 0) return;
            var palette=theme.Capture();
            using (new ClientGuiScope(true))
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true; GUI.color = palette.PrimaryText;
                ClientWidgets.Fill(inRect,palette.GetColor("phinix.example.basic.panel"));
                float y = inRect.y;
                if(state.ShowHints&&inRect.height>=36){ClientWidgets.LabelEllipses(new Rect(inRect.x,y,inRect.width,30),string.Format(state.Text("viewLifecycle"),viewOpens,viewCloses,viewVisible?state.Text("visible"):state.Text("hidden")),palette);y+=36;}
                if (inRect.yMax - y >= 30)
                {
                    if (ClientWidgets.Button(new Rect(inRect.x, y, inRect.width, 30), state.Text("hostPresentation"), palette)) presentation.Open();
                    y += 36;
                }
                if (state.ShowHints)
                {
                    float height = Math.Min(Text.CalcHeight(state.Text("intro"), inRect.width), Math.Max(0, inRect.height - 192));
                    Widgets.Label(new Rect(inRect.x, y, inRect.width, height), state.Text("intro")); y += height + 8;
                }
                if (inRect.yMax - y >= 30)
                {
                    if (ClientWidgets.Button(new Rect(inRect.x, y, inRect.width, 30), state.Text("click") + " · " + state.CountText,palette,textColor:palette.GetColor("phinix.example.basic.counter"))) state.Click();
                    y += 36;
                }
                if (inRect.yMax - y >= 30)
                {
                    if (ClientWidgets.Button(new Rect(inRect.x, y, inRect.width, 30), state.Text("reset"),palette,ClientButtonKind.Danger)) state.ConfirmReset();
                    y += 36;
                }
                if (inRect.yMax-y>=30)
                {
                    ClientWidgets.LabelEllipses(new Rect(inRect.x,y,inRect.width,30),state.TaskStatus,palette); y+=36;
                }
                if (inRect.yMax-y>=30)
                {
                    float half=Math.Max(0,(inRect.width-8)/2);
                    if(ClientWidgets.Button(new Rect(inRect.x,y,half,30),state.Text("taskCancel"),palette)) state.DemonstrateTask(true);
                    if(ClientWidgets.Button(new Rect(inRect.x+half+8,y,half,30),state.Text("taskFail"),palette)) state.DemonstrateTask(false);
                    y+=36;
                }
                if (inRect.yMax-y>=30)
                {
                    if(ClientWidgets.Button(new Rect(inRect.x,y,inRect.width,30),state.Text("contributionMenu")+" · "+actions.Status,palette)) actions.Open();
                    y+=36;
                }
                if(inRect.yMax-y>=30)
                {if(ClientWidgets.Button(new Rect(inRect.x,y,inRect.width,30),state.Text("apiDemo"),palette)) authorServices.Open();y+=36;}
                if (state.ShowHints && inRect.yMax > y) Widgets.Label(new Rect(inRect.x, y, inRect.width, inRect.yMax - y), state.Text("persistence"));
            }

        }
    }

    internal sealed class ExampleSettingsPanel : IClientSettingsPanelProvider
    {
        private int viewOpens,viewCloses;private bool viewVisible;
        internal void ViewOpened(){viewVisible=true;viewOpens++;}
        internal void ViewUpdate(){ }
        internal void ViewClosed(){viewVisible=false;viewCloses++;}
        private readonly ExampleState state;
        private readonly ExampleTheme theme;
        public ExampleSettingsPanel(ExampleState state, ExampleTheme theme) { this.state = state; this.theme=theme; }
        public string SectionId => "phinix.example.basic.settings";
        public float Order => 190f;
        public bool IsVisible(IClientSettingsContext settings) => state.Active;
        public void DrawSettings(Listing_Standard listing, IClientSettingsContext settings)
        {
            if (!state.Active) return;
            var palette=theme.Capture();
            using (new ClientGuiScope(true))
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true; GUI.color = palette.PrimaryText;
                listing.Label(state.Text("settingsTitle"));
                bool hints = state.ShowHints;
                Rect rect = listing.GetRect(Math.Max(30, Text.CalcHeight(state.Text("showHints"), Math.Max(1, listing.ColumnWidth - 36))));
                ClientWidgets.Checkbox(rect, state.Text("showHints"), ref hints,palette);
                state.SetHints(hints);
                listing.Label(state.CountText);
                if (ClientWidgets.Button(listing.GetRect(30f),state.Text("reset"),palette,ClientButtonKind.Danger)) state.ConfirmReset();
            }

        }
    }
}
