using System;
using PhinixClient.Framework;
using UnityEngine;
using Utils.Framework;

namespace Phinix.Example.Basic
{
    // Ordinary plugins own the registration lease; borrowed host services are not disposed.
    internal sealed class ExampleTheme : IDisposable
    {
        private readonly IClientTheme theme;
        private readonly IClientThemeRegistry registry;
        private IClientThemeRegistration registration;
        public ExampleTheme(IClientTheme theme, IClientThemeRegistry registry)
        { this.theme=theme; this.registry=registry; }
        internal void Start(IPhinixExtensionModule module)
        {
            if(registration!=null) throw new InvalidOperationException("Example theme is already registered.");
            var owned=registry.RegisterModule(module, "phinix.example.basic", "Example / 示例");
            try
            {
                owned.RegisterColor("phinix.example.basic.panel",new ClientRgba(.12f,.16f,.2f,.12f),
                    label:"Panel / 面板背景",fallbackKey:"ui.surface",group:"Example / 示例",description:"Background of the example panel / 示例面板背景");
                owned.RegisterColor("phinix.example.basic.counter",new ClientRgba(.35f,.8f,1),
                    label:"Counter / 计数强调",fallbackKey:"ui.accent",group:"Example / 示例",description:"Color of the existing click count / 已有点击计数的颜色");
                owned.Commit(); registration=owned;
            }
            catch { owned.Dispose(); throw; }
        }
        internal ClientThemeSnapshot Capture() => theme.Capture();
        public void Dispose() { var owned=registration; registration=null; owned?.Dispose(); }
    }
}
