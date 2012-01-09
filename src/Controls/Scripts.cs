﻿using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using Kiss.Utils;
using System;

namespace Kiss.Web.Controls
{
    /// <summary>
    /// 该控件将输出合并的js
    /// </summary>
    [PersistChildren(true), ParseChildren(false), NonVisualControl(true)]
    class Scripts : Control, IContextAwaredControl
    {
        const string ScriptKey = "__scripts__";

        private ISite _site;
        public ISite CurrentSite { get { return _site ?? JContext.Current.Site; } set { _site = value; } }

        /// <summary>
        /// 重载Control的Render方法
        /// </summary>
        /// <param name="writer"></param>
        protected override void Render(HtmlTextWriter writer)
        {
            RenderScripts(writer);

            writer.Write("$!jc.render_lazy_include()");

            base.Render(writer);
        }

        /// <summary>
        /// 输出脚本
        /// </summary>
        /// <param name="writer"></param>
        protected virtual void RenderScripts(HtmlTextWriter writer)
        {
            List<string> urls = new List<string>();
            List<string> blocks = new List<string>();

            Queue queue = Context.Items[ScriptKey] as Queue;

            if (queue != null && queue.Count > 0)
            {
                IEnumerator ie = queue.GetEnumerator();
                while (ie.MoveNext())
                {
                    ScriptQueueItem si = (ScriptQueueItem)ie.Current;

                    if (si.IsScriptBlock)
                        blocks.Add(si.Script);
                    else
                        urls.Add(si.Script);
                }
            }

            if (urls.Count > 0)
            {
                int ps = (int)Math.Ceiling(urls.Count * 1.0 / 6);

                for (int i = 0; i < ps; i++)
                {
                    List<string> list = new List<string>();

                    for (int j = 0; j < 6; j++)
                    {
                        list.Add(urls[i * 6 + j]);
                    }

                    writer.Write(string.Format("<script src='{0}' type='text/javascript'></script>",
                        Utility.FormatJsUrl(SiteConfig.Instance, string.Format("_resc.aspx?f={0}&t=text/javascript&v={1}",
                                                            ServerUtil.UrlEncode(StringUtil.CollectionToCommaDelimitedString(list)),
                                                            CurrentSite.JsVersion))));
                }

            }

            if (blocks.Count > 0)
            {
                writer.Write("<script type='text/javascript'>{0}</script>", StringUtil.CollectionToDelimitedString(blocks, " ", string.Empty));
            }
        }

        /// <summary>
        /// 添加脚本链接
        /// </summary>
        /// <param name="url"></param>
        public static void AddRes(string url)
        {
            AddScript(url, false, HttpContext.Current);
        }

        /// <summary>
        /// 添加脚本块
        /// </summary>
        /// <param name="script"></param>
        public static void AddBlock(string script)
        {
            AddScript(script, true, HttpContext.Current);
        }

        private static void AddScript(string script, bool isblock, HttpContext context)
        {
            Queue scriptQueue = context.Items[ScriptKey] as Queue;
            if (scriptQueue == null)
            {
                scriptQueue = new Queue();
                context.Items[ScriptKey] = scriptQueue;
            }

            scriptQueue.Enqueue(new ScriptQueueItem(script, isblock));
        }

        internal class ScriptQueueItem
        {
            public bool IsScriptBlock { get; set; }
            public string Script { get; set; }

            public ScriptQueueItem(string script, bool isblock)
            {
                Script = script;
                IsScriptBlock = isblock;
            }
        }
    }
}
