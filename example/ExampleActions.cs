using System;
using System.Collections.Generic;
using PhinixClient.Framework;
using Utils.Framework;
using Verse;

namespace Phinix.Example.Basic
{
    // This context belongs to the plugin, not Host. Other plugins may reference this contract and contribute to the same point.
    public sealed class ExampleActionContext
    {
        public ExampleActionContext(int count) { Count=count; }
        public int Count { get; }
    }
    internal sealed class ExampleActions : IDisposable
    {
        public const string PointId="phinix.example.basic.actions";
        private readonly IClientExtensionPointService service;
        private readonly ExampleState state;
        private IClientExtensionPointScope scope;
        private IClientActionPoint<ExampleActionContext> point;
        private IClientActionSet current;
        private FloatMenu menu;
        internal string Status {get;private set;}="";
        public ExampleActions(IClientExtensionPointService service,ExampleState state) { this.service=service; this.state=state; }
        internal void Start(IPhinixExtensionModule owner)
        {
            Dispose(); scope=service.ForModule(owner);
            try
            {
                point=scope.DefineActions<ExampleActionContext>(PointId);
                scope.ContributeActions<ExampleActionContext>(PointId,context=>new[] {
                    new ClientActionDefinition("add",state.Text("contributionAdd"),state.Click),
                    new ClientActionDefinition("reset",state.Text("contributionReset"),state.ConfirmReset,
                        context.Count==0?state.Text("contributionDisabled"):null)
                });
            }
            catch { Dispose(); throw; }
        }
        internal void Open()
        {
            if(!state.Active || point==null) return;
            CloseMenu();
            long generation=state.Generation; int count=state.Count;
            current=point.Collect(new ExampleActionContext(count),()=>state.Active && generation==state.Generation && count==state.Count);
            var options=new List<FloatMenuOption>();
            foreach(var action in current.Actions)
            {
                var captured=action;
                string label=action.Label+(action.DisabledReason==null?"":" ("+action.DisabledReason+")");
                options.Add(new FloatMenuOption(label,action.DisabledReason!=null?(Action)null:()=>Status=captured.TryExecute().Code));
            }
            Status="points="+service.CapturePoints().Count;
            if(options.Count!=0)
            {
                var ownedSet=current;
                OwnedActionMenu opened=null;
                opened=new OwnedActionMenu(options,()=>
                {
                    ownedSet.Dispose();
                    if(ReferenceEquals(current,ownedSet)) current=null;
                    if(ReferenceEquals(menu,opened)) menu=null;
                });
                menu=opened; Find.WindowStack.Add(menu);
            }
            else { current.Dispose(); current=null; }
        }
        private sealed class OwnedActionMenu : FloatMenu
        {
            private Action release;
            internal OwnedActionMenu(List<FloatMenuOption> options,Action release):base(options) { this.release=release; }
            public override void PostClose()
            {
                try { base.PostClose(); }
                finally { var owned=release; release=null; owned?.Invoke(); }
            }
        }
        private void CloseMenu()
        {
            var closing=menu; menu=null; closing?.Close();
            var owned=current; current=null; owned?.Dispose();
        }
        public void Dispose()
        {
            CloseMenu(); point=null;
            var owned=scope; scope=null; owned?.Dispose();
        }
    }
}
