using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using Phinix.Example.Basic;
using PhinixClient;
using PhinixClient.Framework;
using Utils.Framework;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition,string message)
    { if(!condition) throw new Exception(message); assertions++; }
    private static int Main()
    {
        foreach(string file in new[]{"02-Protobuf.dll","03-Utils.dll","10-ClientExtensionAbstractions.dll"})
            Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,file));
        return Run();
    }
    private static int Run()
    {
        try
        {
            var settings=new Settings(); var dispatcher=new Dispatcher(); var localization=new Localization();
            var logs=new List<string>();
            using(var factory=new ClientCompositionFactory(()=>true,e=>{throw e;}))
            {
                for(int cycle=0;cycle<5;cycle++)
                {
                    var host=new ExtensionHostContext {Log=(message,level)=>logs.Add(message)};
                    host.AddService<IClientCompositionFactory>(factory);
                    var borrowed=new BorrowedServices();
                    host.AddService<IClientApiDirectory>(borrowed);
                    host.AddService<IClientLifetimeService>(borrowed);
                    host.AddService<IClientPresentationService>(borrowed);
                    host.AddService<IClientSettingsContext>(settings);
                    var theme=new Theme();
                    host.AddService<IClientTheme>(theme);
                    host.AddService<IClientThemeRegistry>(theme);
                    host.AddService<IClientMainThreadDispatcher>(dispatcher);
                    host.AddService<IClientTaskService>(dispatcher);
                    host.AddService<IClientExtensionPointService>(new Points());
                    host.AddService<IClientLocalizationService>(localization);
                    host.AddService<IExtensionDiscoveryPolicy>(new Discovery());
                    var found=PhinixExtensionRegistry.DiscoverExtensions(host);
                    Check(found.Modules.Count==1 && found.Warnings.Count==0,"Actual Example composes through ordinary discovery: modules="+found.Modules.Count+" warnings="+string.Join("; ",found.Warnings)+" logs="+string.Join("; ",logs));
                    PhinixExtensionRegistry.ActivateExtensions(found,host);
                    Check(found.ApiRegistry.ResolveAll<IMainTabProvider>().Count==1 && found.ApiRegistry.ResolveAll<IClientSettingsPanelProvider>().Count==1,"DI registers one tab and one settings provider");
                    var module=found.Modules.Single();
                    object state=typeof(ExampleExtension).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(module);
                    var click=state.GetType().GetMethod("Click",BindingFlags.Instance|BindingFlags.NonPublic);
                    Check(localization.Current.Subscribers==1,"Activation has one language subscription");
                    localization.Current.Change();
                    Check(logs.Last().Contains("language changed"),"Language event reaches actual Example");
                    click.Invoke(state,null);
                    Check(settings.Get("phinix.example.basic.clicks",0)==cycle+1,"Settings persist across lifecycle cycles");
                    dispatcher.Drain(); Check(logs.Last().Contains("main-thread callback"),"Callback runs through borrowed dispatcher");
                    click.Invoke(state,null);
                    int callbacks=logs.Count(l=>l.Contains("main-thread callback"));
                    PhinixExtensionRegistry.ShutdownExtensions(found,host);
                    PhinixExtensionRegistry.ShutdownExtensions(found,host);
                    dispatcher.Drain();
                    Check(logs.Count(l=>l.Contains("main-thread callback"))==callbacks,"Shutdown rejects queued callbacks from the old activation");
                    Check(localization.Current.Subscribers==0 && localization.Current.Disposals==1,"Localizer unsubscribes and releases exactly once");
                    Check(!settings.Disposed && !dispatcher.Disposed && !localization.Disposed,"Borrowed host services are never disposed");
                    settings.Set("phinix.example.basic.clicks",cycle+1);
                }
            }
            Console.WriteLine("Example lifecycle passed: "+assertions+" assertions, five activation/shutdown cycles."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    // Source alignment for the example's new borrowed theme services; existing scenarios only.
    private sealed class Theme : IClientTheme,IClientThemeRegistry
    {
        public long Version=>1;
        public UnityEngine.Color PrimaryText=>default; public UnityEngine.Color SecondaryText=>default;
        public UnityEngine.Color Background=>default; public UnityEngine.Color Surface=>default;
        public UnityEngine.Color Separator=>default; public UnityEngine.Color HoverHighlight=>default;
        public UnityEngine.Color Pending=>default; public UnityEngine.Color Error=>default;
        public UnityEngine.Color Success=>default; public UnityEngine.Color Warning=>default;
        public event Action Changed { add {} remove {} }
        public UnityEngine.Color GetColor(string key)=>default;
        public bool TryGetColor(string key,out UnityEngine.Color value) { value=default;return true; }
        public float GetFloat(string key,float fallback=0)=>fallback;
        public ClientThemeSnapshot Capture()=>new ClientThemeSnapshot(Version,new Dictionary<string,UnityEngine.Color>(),new Dictionary<string,float>());
        public IClientThemeRegistration RegisterModule(IPhinixExtensionModule module,string keyNamespace=null,string ownerLabel=null)=>new Registration();
        private sealed class Registration : IClientThemeRegistration
        {
            public void RegisterColor(string key,UnityEngine.Color color,string label=null,string fallbackKey=null,string group=null,string description=null){}
            public void RegisterFloat(string key,float value,float minimum=0,float maximum=1,string label=null,string group=null,string description=null){}
            public void Commit(){} public void Dispose(){}
        }
    }
    private sealed class Discovery : IExtensionDiscoveryPolicy
    {
        public bool ShouldScanAssembly(Assembly assembly)=>assembly==typeof(ExampleExtension).Assembly;
        public bool ShouldDiscoverType(Type type)=>type==typeof(ExampleExtension);
    }
    private sealed class Settings : IClientSettingsContext,IDisposable
    {
        private readonly Dictionary<string,object> values=new Dictionary<string,object>();
        public bool Disposed;
        public T Get<T>(string key,T fallback=default(T))=>values.TryGetValue(key,out var value)?(T)value:fallback;
        public void Set<T>(string key,T value) { values[key]=value; }
        public IEnumerable<string> BlockedUsers=>Array.Empty<string>();
        public bool CollapseBlockedUsers {get;set;}
        public void BlockUser(string uuid) {} public void UnBlockUser(string uuid) {}
        public event Action<string,object> OnSettingChanged {add {} remove {}}
        public void Dispose() {Disposed=true;}
    }
    // Contract alignment only; this fixture does not validate contribution collection/execution semantics.
    private sealed class Points : IClientExtensionPointService
    {
        public long Revision=>0;
        public IReadOnlyList<ClientExtensionPointInfo> CapturePoints()=>new ClientExtensionPointInfo[0];
        public IClientExtensionPointScope ForModule(IPhinixExtensionModule module)=>new Scope();
        private sealed class Scope : IClientExtensionPointScope
        {
            public IClientActionPoint<T> DefineActions<T>(string pointId) where T:class=>new Point<T>();
            public void ContributeActions<T>(string pointId,Func<T,IEnumerable<ClientActionDefinition>> factory,float order=0) where T:class { }
            public void Dispose() { }
        }
        private sealed class Point<T> : IClientActionPoint<T> where T:class
        {
            public IClientActionSet Collect(T context,Func<bool> isContextCurrent=null)=>new Set();
        }
        private sealed class Set : IClientActionSet
        {
            public IReadOnlyList<IClientContributedAction> Actions=>new IClientContributedAction[0];
            public void Dispose() { }
        }
    }
    private sealed class Dispatcher : IClientMainThreadDispatcher,IClientTaskService,IDisposable
    {
        private readonly Queue<Action> actions=new Queue<Action>(); public bool Disposed;
        public void Enqueue(Action action)=>actions.Enqueue(action);
        public IClientTaskScope ForModule(IPhinixExtensionModule module)=>new TaskScope(this);
        private sealed class TaskScope : IClientTaskScope
        {
            private readonly Dispatcher dispatcher; private bool closed;
            internal TaskScope(Dispatcher dispatcher) { this.dispatcher=dispatcher; }
            public IClientTaskHandle Submit(Action action,System.Threading.CancellationToken cancellation=default)
            {
                var result=new TaskHandle();
                if(closed || cancellation.IsCancellationRequested) { result.State=ClientTaskState.Canceled; return result; }
                dispatcher.Enqueue(()=> { if(closed || cancellation.IsCancellationRequested) { result.State=ClientTaskState.Canceled; return; } action(); result.State=ClientTaskState.Succeeded; });
                return result;
            }
            public void Dispose() { closed=true; }
        }
        private sealed class TaskHandle : IClientTaskHandle
        {
            internal ClientTaskState State=ClientTaskState.Queued;
            public ClientTaskSnapshot Capture()=>new ClientTaskSnapshot(1,State,State.ToString());
        }
        public void Drain() {while(actions.Count!=0) actions.Dequeue()();}
        public void Dispose() {Disposed=true;}
    }
    private sealed class Localization : IClientLocalizationService,IDisposable
    {
        public Localizer Current; public bool Disposed;
        public IClientLocalizer ForModule(IPhinixExtensionModule module)=>Current=new Localizer();
        public void Dispose() {Disposed=true;}
    }
    private sealed class Localizer : IClientLocalizer
    {
        private Action changed; public int Subscribers,Disposals;
        public string Locale=>"en-US";
        public event Action LanguageChanged {add {changed+=value; Subscribers++;} remove {changed-=value; Subscribers--;}}
        public string Text(string key,string fallback=null)=>fallback??key;
        public string Format(string key,params object[] arguments)=>key;
        public void Change()=>changed?.Invoke();
        public void Dispose() {Disposals++;}
    }
    private sealed class BorrowedServices : IClientApiDirectory,IClientLifetimeService,IClientPresentationService
    {
        public IReadOnlyList<IClientApiProvider<T>> Capture<T>() where T:class => Array.Empty<IClientApiProvider<T>>();
        public IClientLifetimeScope ForModule(IPhinixExtensionModule module)=>new Scope(module.ExtensionId);
        public IClientPresentationSession Open(IPhinixExtensionModule module,string id)=>throw new InvalidOperationException("Lifecycle test does not open GUI sessions");
        private sealed class Scope : IClientLifetimeScope
        {
            public Scope(string owner){OwnerId=owner;}
            public string OwnerId {get;}public bool IsClosed {get;private set;}
            private readonly System.Threading.CancellationTokenSource stop=new System.Threading.CancellationTokenSource();
            private readonly List<IDisposable> owned=new List<IDisposable>();
            public System.Threading.CancellationToken Stopping=>stop.Token;
            public IDisposable Own(IDisposable resource){owned.Add(resource);return resource;}
            public IDisposable Subscribe(Action<Action> subscribe,Action<Action> unsubscribe,Action callback)
            {Action guarded=()=>{if(!IsClosed)callback();};subscribe(guarded);return Own(new Cleanup(()=>unsubscribe(guarded)));}
            public void Dispose(){if(IsClosed)return;IsClosed=true;stop.Cancel();foreach(var resource in owned)resource.Dispose();owned.Clear();stop.Dispose();}
        }
        private sealed class Cleanup : IDisposable {private Action action;public Cleanup(Action action){this.action=action;}public void Dispose(){var old=action;action=null;old?.Invoke();}}
    }

}
