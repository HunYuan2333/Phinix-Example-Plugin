using System;
using System.Linq;
using System.Threading;
using PhinixClient;
using PhinixClient.Framework;
using UnityEngine;
using Utils.Framework;
using Verse;

namespace Phinix.Example.Basic
{
    // An intentionally absent optional contract: its absence is ordinary capability state.
    public interface IExampleOptionalApi { }
    internal sealed class ExampleAuthorServices : IDisposable
    {
        private readonly IClientApiDirectory apis;
        private readonly IClientLifetimeService lifetimes;
        private readonly IClientTheme theme;
        private readonly ExampleState state;
        private IPhinixExtensionModule module;
        private IClientLifetimeScope scope;
        private Action pulse,lateCallback;
        private CancellationToken token;
        private int callbacks,releases;
        private string providerStatus;
        private DemoWindow window;
        public ExampleAuthorServices(IClientApiDirectory apis,IClientLifetimeService lifetimes,IClientTheme theme,ExampleState state)
        {this.apis=apis;this.lifetimes=lifetimes;this.theme=theme;this.state=state;}
        internal void Start(IPhinixExtensionModule module) {this.module=module;}
        private void Bind()
        {
            scope?.Dispose();scope=lifetimes.ForModule(module);token=scope.Stopping;
            scope.Own(new Release(()=>releases++));
            scope.Subscribe(handler=> {lateCallback=handler;pulse+=handler;},handler=>pulse-=handler,()=>callbacks++);
            scope.Subscribe(handler=>theme.Changed+=handler,handler=>theme.Changed-=handler,()=>callbacks++);
        }
        private void Inspect()
        {
            var providers=apis.Capture<IClientTheme>();
            var available=providers.Count(p=>p.TryGet(out var borrowed));
            var own=apis.Capture<IMainTabProvider>().FirstOrDefault(p=>p.OwnerId==module.ExtensionId);
            bool ownAvailable=own!=null && own.TryGet(out var tab);
            providerStatus=state.Text("apiProviders")+": "+available+" · "+state.Text("apiOwn")+": "+ownAvailable+
                " · "+state.Text("apiOptional")+": "+apis.Capture<IExampleOptionalApi>().Count;
        }
        internal void Open()
        {
            if(module==null || window?.IsOpen==true) return;
            Bind();Inspect();window=new DemoWindow(this);Find.WindowStack.Add(window);
        }
        private void CloseScope() {scope?.Dispose();scope=null;}
        public void Dispose()
        {CloseScope();module=null;pulse=null;lateCallback=null;if(window?.IsOpen==true) window.Close();window=null;}
        private sealed class Release:IDisposable
        {
            private Action release;
            internal Release(Action release) {this.release=release;}
            public void Dispose() {var callback=release;release=null;callback?.Invoke();}
        }
        private sealed class DemoWindow:Window
        {
            private readonly ExampleAuthorServices service;
            internal DemoWindow(ExampleAuthorServices service) {this.service=service;doCloseX=true;closeOnAccept=false;absorbInputAroundWindow=true;}
            public override Vector2 InitialSize=>new Vector2(Mathf.Min(680,UiScreenSafeArea.Current.width),Mathf.Min(360,UiScreenSafeArea.Current.height));
            public override void WindowUpdate() {base.WindowUpdate();windowRect=UiScreenSafeArea.ClampWindow(windowRect,new Vector2(320,320));}
            public override void PostClose() {base.PostClose();service.CloseScope();service.lateCallback=null;}
            public override void DoWindowContents(Rect rect)
            {
                using(new ClientGuiScope(true))
                {
                    var palette=service.theme.Capture();Text.Font=GameFont.Small;
                    ClientWidgets.Label(new Rect(0,0,rect.width,80),service.providerStatus,palette,true);
                    ClientWidgets.Label(new Rect(0,84,rect.width,60),service.state.Text("apiLifetime")+": "+service.callbacks+" / "+service.releases+
                        " · "+service.state.Text("apiCanceled")+": "+service.token.IsCancellationRequested,palette,true);
                    float width=(rect.width-6)/2;
                    if(ClientWidgets.Button(new Rect(0,156,width,30),service.state.Text("apiPulse"),palette)) {service.pulse?.Invoke();service.lateCallback?.Invoke();}
                    if(ClientWidgets.Button(new Rect(width+6,156,width,30),service.state.Text("apiClose"),palette)) service.CloseScope();
                    if(ClientWidgets.Button(new Rect(0,194,width,30),service.state.Text("apiRebind"),palette)) service.Bind();
                    if(ClientWidgets.Button(new Rect(width+6,194,width,30),service.state.Text("apiInspect"),palette)) service.Inspect();
                    ClientWidgets.Label(new Rect(0,232,rect.width,Mathf.Max(0,rect.height-232)),service.state.Text("apiTip"),palette,true);
                }
            }
        }
    }
}
