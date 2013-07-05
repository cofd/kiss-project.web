﻿using Kiss.Utils;
using System;
using System.Collections.Generic;
using System.Web.UI;

namespace Kiss.Web.Controls
{
    public class ControlPanel : Control
    {
        private bool isSiteAdmin = false;

        private List<IControlPanelItemRenderer> renderers = new List<IControlPanelItemRenderer>();

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            JContext jc = JContext.Current;
            if (jc.User == null) return;

            isSiteAdmin = jc.User.HasPermission(string.Format("site.control_panel@{0}",
                jc.Area["support_multi_site"].ToBoolean() ? jc.SiteId : string.Empty));

            if (!isSiteAdmin) return;

            foreach (var cpItem in Plugin.Plugins.GetPlugins<ControlPanelItemAttribute>(JContext.Current.User))
            {
                Object obj = Activator.CreateInstance(cpItem.Decorates);
                if (obj is Control)
                    Controls.Add(obj as Control);

                renderers.Add(obj as IControlPanelItemRenderer);
            }

            if (renderers.Count == 0) return;

            Include include = new Include();
            include.Js = "cookie";
            Controls.Add(include);

            ClientScriptProxy.Current.RegisterCssResource(GetType(),
                                "Kiss.Web.jQuery.cp.c.css");
        }

        protected override void Render(HtmlTextWriter writer)
        {
            if (!isSiteAdmin || renderers.Count == 0) { base.Render(writer); return; }

            writer.Write("<div id='_g_sc' class='sc opened' path='{0}'><div class='scContent'><div class='controlPanel'><div class='plugins'>", JContext.Current.url("~"));

            foreach (var renderer in renderers)
            {
                renderer.RenderItem(writer);
            }

            writer.Write("</div></div><span class='close' title='关闭控制面板'>«</span><span class='open' title='打开控制面板'>»</span></div></div>");

            ClientScriptProxy.Current.RegisterJsResource(writer, GetType(), "Kiss.Web.jQuery.cp.j.js");

            base.Render(writer);
        }
    }
}
