﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Web;
using Kiss.Security;
using Kiss.Utils;
using Kiss.Web.Mvc;
using Kiss.Web.UrlMapping;

namespace Kiss.Web
{
    /// <summary>
    /// 当前HTTP请求的上下文
    /// </summary>
    public sealed class JContext
    {
        #region Private Containers

        //Generally expect 10 or less items
        private HybridDictionary _items = new HybridDictionary();
        private NameValueCollection _queryString = null;
        private NameValueCollection _form = null;

        private HttpContext _httpContext = null;
        private DateTime requestStartTime = DateTime.Now;
        public DateTime RequestStartTime { get { return requestStartTime; } }
        private string _rawUrl;
        public string RawUrl { get { return _rawUrl; } }

        #endregion

        #region Initialize  and cnstr.'s

        /// <summary>
        /// Create/Instatiate items that will vary based on where this object 
        /// is first created
        /// 
        /// We could wire up Path, encoding, etc as necessary
        /// </summary>
        private void Initialize(NameValueCollection qs, string rawUrl)
        {
            _queryString = qs;
            _rawUrl = rawUrl;
        }

        /// <summary>
        /// cnst called when HttpContext is avaiable
        /// </summary>
        private JContext(HttpContext context, bool includeQS)
        {
            this._httpContext = context;

            this._form = new NameValueCollection(context.Request.Form);

            if (includeQS)
                Initialize(new NameValueCollection(context.Request.QueryString), context.Request.RawUrl);
            else
                Initialize(null, context.Request.RawUrl);
        }

        #endregion

        #region Create

        /// <summary>
        /// Creates a Context instance based on HttpContext. Generally, this
        /// method should be called via Begin_Request in an HttpModule
        /// </summary>
        public static JContext Create(HttpContext context)
        {
            JContext jcontext = new JContext(context, true);
            SaveContextToStore(jcontext);

            return jcontext;
        }

        #endregion

        #region Core Properties
        /// <summary>
        /// Simulates Context.Items and provides a per request/instance storage bag
        /// </summary>
        public IDictionary Items
        {
            get { return _items; }
        }

        /// <summary>
        /// Provides direct access to the .Items property
        /// </summary>
        public object this[string key]
        {
            get
            {
                return this.Items[key];
            }
            set
            {
                this.Items[key] = value;
            }
        }

        /// <summary>
        /// Allows access to QueryString values
        /// </summary>
        public NameValueCollection QueryString
        {
            get { return _queryString; }
        }


        public NameValueCollection Form
        {
            get
            {
                return _form;
            }
        }

        public NameValueCollection Params
        {
            get
            {
                NameValueCollection p = new NameValueCollection(Form);
                foreach (string key in QueryString.Keys)
                {
                    if (string.IsNullOrEmpty(p[key]))
                        p[key] = QueryString[key];
                }
                return p;
            }
        }

        public HttpContext Context
        {
            get
            {
                return _httpContext;
            }
        }

        #endregion

        #region Helpers

        // *********************************************************************
        //  GetGuidFromQueryString
        //
        /// <summary>
        /// Retrieves a value from the query string and returns it as an int.
        /// </summary>
        // ***********************************************************************/
        public Guid GetGuidFromQueryString(string key)
        {
            Guid returnValue = Guid.Empty;
            string queryStringValue;

            // Attempt to get the value from the query string
            //
            queryStringValue = QueryString[key];

            // If we didn't find anything, just return
            //
            if (queryStringValue == null)
                return returnValue;

            // Found a value, attempt to conver to integer
            //
            try
            {

                // Special case if we find a # in the value
                //
                if (queryStringValue.IndexOf("#") > 0)
                    queryStringValue = queryStringValue.Substring(0, queryStringValue.IndexOf("#"));

                returnValue = new Guid(queryStringValue);
            }
            catch { }

            return returnValue;

        }

        // *********************************************************************
        //  GetIntFromQueryString
        //
        /// <summary>
        /// Retrieves a value from the query string and returns it as an int.
        /// </summary>
        // ***********************************************************************/
        public int GetIntFromQueryString(string key, int defaultValue)
        {
            string queryStringValue;


            // Attempt to get the value from the query string
            //
            queryStringValue = this.QueryString[key];

            // If we didn't find anything, just return
            //
            if (queryStringValue == null)
                return defaultValue;

            // Found a value, attempt to conver to integer
            //
            try
            {

                // Special case if we find a # in the value
                //
                if (queryStringValue.IndexOf("#") > 0)
                    queryStringValue = queryStringValue.Substring(0, queryStringValue.IndexOf("#"));

                defaultValue = Convert.ToInt32(queryStringValue);
            }
            catch { }

            return defaultValue;

        }

        #endregion

        #region Common QueryString Properties

        string returnUrl = null;

        public string ReturnUrl
        {
            get
            {
                if (returnUrl == null)
                    returnUrl = QueryString["returnUrl"];

                return returnUrl;
            }
            set { returnUrl = value; }
        }

        int pageIndex = -2;
        public int PageIndex
        {
            get
            {
                if (pageIndex == -2)
                {
                    pageIndex = this.GetIntFromQueryString("page", GetIntFromQueryString("p", -1));
                    if (pageIndex != -1)
                        pageIndex = pageIndex - 1;
                    else if (pageIndex < 0)
                        pageIndex = 0;

                }
                return pageIndex;
            }
            set { pageIndex = value; }
        }

        #endregion

        #region State

        private static readonly string dataKey = "JContextStore";

        /// <summary>
        /// Returns the current instance of the CSContext from the ThreadData Slot. If one is not found and a valid HttpContext can be found,
        /// it will be used. Otherwise, an exception will be thrown. 
        /// </summary>
        public static JContext Current
        {
            get
            {
                HttpContext httpContext = HttpContext.Current;
                JContext context = null;
                if (httpContext != null)
                {
                    context = httpContext.Items[dataKey] as JContext;
                }
                else
                {
                    context = Thread.GetData(GetSlot()) as JContext;
                }

                if (context == null)
                {

                    if (httpContext == null)
                        throw new Exception("No JContext exists in the Current Application. AutoCreate fails since HttpContext.Current is not accessible.");

                    context = new JContext(httpContext, true);
                    SaveContextToStore(context);
                }
                return context;
            }
        }

        private static LocalDataStoreSlot GetSlot()
        {
            return Thread.GetNamedDataSlot(dataKey);
        }

        private static void SaveContextToStore(JContext context)
        {
            if (context.Context != null)
            {
                context.Context.Items[dataKey] = context;
            }
            else
            {
                Thread.SetData(GetSlot(), context);
            }
        }

        public static void Unload()
        {
            Thread.FreeNamedDataSlot(dataKey);
        }

        #endregion

        #region Navigation

        private NavigationInfo _navigation = new NavigationInfo();
        /// <summary>
        /// get navigation info
        /// </summary>
        public NavigationInfo Navigation { get { return _navigation; } internal set { _navigation = value; } }

        #endregion

        #region ContextData

        /// <summary>
        /// get data in context
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetContextData<T>(string key)
        {
            if (StringUtil.IsNullOrEmpty(key))
                return default(T);

            if (ContextData.Datas.ContainsKey(key))
                return (T)ContextData.Datas[key];

            return default(T);
        }

        #endregion

        #region User

        private Principal _user;
        /// <summary>
        /// current user
        /// </summary>
        public Principal User
        {
            get
            {
                if (_user == null)
                {
                    _user = Context.User as Principal;
                }

                return _user;
            }
        }

        /// <summary>
        /// 当前用户是否通过认证
        /// </summary>
        public bool IsAuth
        {
            get
            {
                return Context.User.Identity.IsAuthenticated;
            }
        }

        /// <summary>
        /// 当前用户的用户名
        /// </summary>
        public string UserName
        {
            get
            {
                return Context.User.Identity.Name;
            }
        }

        #endregion

        private bool? _isAjaxRequest;
        /// <summary>
        /// 当前请求是否是ajax
        /// </summary>
        public bool IsAjaxRequest { get { if (_isAjaxRequest == null) _isAjaxRequest = Context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"; return _isAjaxRequest.Value; } internal set { _isAjaxRequest = value; } }

        public bool IsAsync { get; internal set; }

        private bool? _isEmbed;

        /// <summary>
        /// 当前请求是否是嵌入式（只返回html片段）
        /// </summary>
        public bool IsEmbed { get { if (_isEmbed == null) _isEmbed = Context.Request.Headers["embed"] == "1"; return _isEmbed.Value; } }

        private bool _renderContent = true;
        /// <summary>
        /// 是否渲染皮肤
        /// </summary>
        public bool RenderContent { get { return _renderContent; } set { _renderContent = value; } }

        /// <summary>
        /// 是否是提交表单
        /// </summary>
        public bool IsPost
        {
            get
            {
                return Context.Request.HttpMethod.Equals("POST", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// 站点Id，用于站点群
        /// </summary>
        public int SiteId { get; set; }

        /// <summary>
        /// 站点群配置
        /// </summary>
        public Site SiteConfig { get; set; }

        #region Design

        private bool? _isdesignMode = null;
        /// <summary>
        /// 是否处于设计模式
        /// </summary>
        public bool IsDesignMode
        {
            get
            {
                if (_isdesignMode == null || !_isdesignMode.HasValue)
                    _isdesignMode = QueryString["edit"] != null && (User == null || User.HasPermission("menu:widget_home"));
                return _isdesignMode.Value;
            }
        }

        #endregion

        #region mvc

        #region ViewData

        private Dictionary<string, object> _viewData = new Dictionary<string, object>();
        /// <summary>
        /// datas used in mvc style binding
        /// </summary>
        public Dictionary<string, object> ViewData { get { return _viewData; } }

        /// <summary>
        /// get datas in mvc style binding
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object GetViewData(string key)
        {
            if (StringUtil.IsNullOrEmpty(key))
                return null;

            if (_viewData.ContainsKey(key))
                return _viewData[key];

            return null;
        }

        /// <summary>
        /// get datas in mvc style binding
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetViewData<T>(string key)
        {
            object obj = GetViewData(key);
            if (obj == null)
                return default(T);

            return (T)obj;
        }

        #endregion

        /// <summary>
        /// current mvc controller
        /// </summary>
        public Controller Controller { get; set; }

        #endregion

        private string referrer;
        public string Referrer
        {
            get
            {
                if (referrer == null)
                {
                    Uri refer = Context.Request.UrlReferrer;
                    if (refer != null)
                        referrer = refer.AbsoluteUri;
                    else
                        referrer = string.Empty;
                }
                return referrer;
            }
        }

        private NameValueCollection _referrerQueryString;
        public NameValueCollection ReferrerQueryString
        {
            get
            {
                if (_referrerQueryString == null)
                {
                    Uri refer = Context.Request.UrlReferrer;
                    if (refer != null)
                        _referrerQueryString = StringUtil.DelimitedEquation2NVCollection("&", refer.Query.Trim('?'));
                    else
                        _referrerQueryString = new NameValueCollection();
                }

                return _referrerQueryString;
            }
        }

        private IUrlMappingProvider _urlmapping;
        public IUrlMappingProvider UrlMapping
        {
            get
            {
                if (_urlmapping == null)
                    _urlmapping = UrlMappingModule.Instance.Provider;
                return _urlmapping;
            }
        }

        public string GetUrlBySite(string siteKey, string name)
        {
            ISite site = null;

            foreach (var item in Host.AllSites)
            {
                if (!item.SiteKey.Equals(siteKey, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                site = item;
            }

            if (site == null)
                return string.Empty;

            Dictionary<string, string> urls = UrlMapping.GetUrlsBySite(site);

            if (urls.Count == 0)
                return string.Empty;

            if (urls.ContainsKey(name))
                return StringUtil.CombinUrl(site.VirtualPath, urls[name]);

            return string.Empty;
        }

        public string GetUrlBySite(string siteKey, string name, string replace)
        {
            NameValueCollection nv = StringUtil.CommaDelimitedEquation2NVCollection(replace);

            string url = GetUrlBySite(siteKey, name);

            foreach (string key in nv.Keys)
            {
                url = url.Replace("[" + key + "]", nv[key]);
            }

            return url;
        }

        /// <summary>
        /// get url by UrlMapping name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetUrl(string name)
        {
            return GetUrlBySite(Host.CurrentSite.SiteKey, name);
        }

        /// <summary>
        /// get url by UrlMapping name, and replace
        /// </summary>
        /// <param name="name"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        public string GetUrl(string name, string replace)
        {
            return GetUrlBySite(Host.CurrentSite.SiteKey, name, replace);
        }

        /// <summary>
        /// get url by UrlMapping name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="withDomain"></param>
        /// <returns></returns>
        public string GetUrl(string name, bool withDomain)
        {
            if (UrlMapping.Urls.ContainsKey(name))
                return withDomain ? Utility.FormatUrlWithDomain(Site, UrlMapping.Urls[name]) : UrlMapping.Urls[name];
            return string.Empty;
        }

        /// <summary>
        /// combin baseurl with current site's virtual path
        /// </summary>
        /// <param name="baseurl"></param>
        /// <returns></returns>
        public string CombinUrl(string baseurl, bool embable)
        {
            string url;

            if (StringUtil.IsNullOrEmpty(baseurl))
                url = Site.VirtualPath;
            else if (baseurl.StartsWith("~"))
                url = ServerUtil.ResolveUrl(baseurl);
            else if (baseurl.StartsWith("."))
                url = StringUtil.CombinUrl(Site.VirtualPath, baseurl.Substring(1)).Substring(HttpRuntime.AppDomainAppVirtualPath.Length);
            else
                url = StringUtil.CombinUrl(Site.VirtualPath, baseurl);

            url = HttpUtility.UrlPathEncode(url);

            if (!embable || !IsEmbed) return url;

            if (baseurl.IndexOf("_res.aspx", StringComparison.InvariantCultureIgnoreCase) != -1
                || baseurl.IndexOf("_resc.aspx", StringComparison.InvariantCultureIgnoreCase) != -1
                || baseurl.IndexOf(".css", StringComparison.InvariantCultureIgnoreCase) != -1
                || baseurl.IndexOf(".js", StringComparison.InvariantCultureIgnoreCase) != -1
                || baseurl.IndexOf("/themes/", StringComparison.InvariantCultureIgnoreCase) != -1
                )
                return url;

            string embedUrl = Context.Request.Headers["embedUrl"];

            if (string.IsNullOrEmpty(embedUrl)) return url;

            if (!embedUrl.StartsWith("~"))
                embedUrl = "~" + embedUrl;

            embedUrl = ServerUtil.ResolveUrl(embedUrl);

            return url.StartsWith(embedUrl, StringComparison.InvariantCultureIgnoreCase) ? '#' + url : url;
        }

        /// <summary>
        /// CombinUrl的简写方法
        /// </summary>
        public string url(string baseUrl)
        {
            return CombinUrl(baseUrl, true);
        }

        public string url(string baseUrl, bool embable)
        {
            return CombinUrl(baseUrl, embable);
        }

        #region Engine

        private ISite _site;
        /// <summary>
        /// get current site
        /// </summary>
        public ISite Site
        {
            get
            {
                if (_site == null)
                {
                    _site = Host.CurrentSite;
                }

                return _site;
            }
        }

        public IHost Host { get { return ServiceLocator.Instance.Resolve<IHost>(); } }

        public ISite DefaultSite { get { return Kiss.Web.SiteConfig.Instance; } }

        #endregion

        #region lazy include
        public void include(string res)
        {
            include(res, string.Empty);
        }

        public void include(string res, string callback)
        {
            Dictionary<string, List<string>> includes;
            if (!ViewData.ContainsKey("_lazy_include_"))
            {
                includes = new Dictionary<string, List<string>>();
                ViewData["_lazy_include_"] = includes;
            }
            else
            {
                includes = ViewData["_lazy_include_"] as Dictionary<string, List<string>>;
            }

            if (res.Contains(";"))
            {
                List<string> items = new List<string>();
                foreach (var item in StringUtil.Split(res, ";", true, true))
                {
                    if (item.Contains("/") || item.Contains(","))
                        items.Add(item);
                    else
                    {
                        items.Add(string.Format("Kiss.Web.jQuery.{0},Kiss.Web", item));
                    }
                }

                res = items.Join(";");
            }
            else
            {
                if (!res.Contains("/") && !res.Contains(","))
                    res = string.Format("Kiss.Web.jQuery.{0},Kiss.Web", res);
            }

            if (includes.ContainsKey(res))
                includes[res].Add(callback);
            else
                includes[res] = new List<string>() { callback };
        }

        public string render_lazy_include()
        {
            if (!ViewData.ContainsKey("_lazy_include_"))
                return string.Empty;

            ClientScriptProxy csp = ClientScriptProxy.Current;

            Dictionary<string, List<string>> includes = JContext.Current.ViewData["_lazy_include_"] as Dictionary<string, List<string>>;

            List<string> cssfiles = new List<string>();
            Dictionary<string, string> jsfiles = new Dictionary<string, string>();
            List<string> scripts = new List<string>();

            bool is_css = false;

            foreach (var item in includes.Keys)
            {
                string url = item;

                if (item.Contains(";"))
                {
                    is_css = item.Contains(".css");

                    List<string> hrefs = new List<string>();

                    string[] strs = StringUtil.Split(item, ";", true, true);
                    for (int i = 0; i < strs.Length; i++)
                    {
                        string href = strs[i];
                        if (!href.Contains("/"))
                            href = Resources.Utility.GetResourceUrl(href);

                        if (csp.IsScriptRended(href))
                            continue;

                        csp.SetScriptRended(href);

                        if (is_css && !Site.CombineCss)
                            cssfiles.Add(href);
                        else
                            hrefs.Add(href);
                    }

                    for (int i = strs.Length - 1; i >= 0; i--)
                    {
                        string href = strs[i];
                        if (!href.Contains("/"))
                            href = Resources.Utility.GetResourceUrl(href);

                        if (jsfiles.ContainsKey(href))
                        {
                            jsfiles[href] = includes[item].Join(";");
                        }
                    }

                    // comine url
                    if (is_css && Site.CombineCss)
                        url = Utility.FormatCssUrl(Site, string.Format("_resc.aspx?f={0}&t=text/css&v={1}",
                                                                ServerUtil.UrlEncode(StringUtil.CollectionToCommaDelimitedString(hrefs)),
                                                                Site.CssVersion));
                    else if (!is_css)
                        url = Utility.FormatJsUrl(Kiss.Web.SiteConfig.Instance, string.Format("_resc.aspx?f={0}&t=text/javascript&v={1}",
                                                            ServerUtil.UrlEncode(StringUtil.CollectionToCommaDelimitedString(hrefs)),
                                                            Site.JsVersion));
                    else
                        continue;
                }
                else if (!item.Contains("/"))
                {
                    url = Resources.Utility.GetResourceUrl(item);

                    is_css = item.Contains(".css");
                }
                else
                {
                    is_css = url.Contains(".css");
                }

                if (csp.IsScriptRended(url))
                {
                    scripts.AddRange(includes[item]);
                    continue;
                }

                csp.SetScriptRended(url);

                if (is_css)
                    cssfiles.Add(url);
                else
                    jsfiles.Add(url, includes[item].Join(";"));
            }

            StringBuilder sb = new StringBuilder();

            if (!IsAjaxRequest && (cssfiles.Count > 0 || jsfiles.Count > 0))
                sb.AppendFormat("<script src='{0}' type='text/javascript'></script>", Resources.Utility.GetResourceUrl("Kiss.Web.jQuery.lazyinclude.js,Kiss.Web", true));

            sb.Append("<script type='text/javascript'>");

            sb.Append("$(function(){");

            if (cssfiles.Count > 0 || jsfiles.Count > 0)
            {
                sb.Append("lazy_include({");
                sb.AppendFormat("cssFiles:[{0}],", StringUtil.CollectionToDelimitedString(cssfiles, StringUtil.Comma, "'"));
                sb.Append("jsFiles:[");

                var i = 0;
                foreach (var item in jsfiles.Keys)
                {
                    if (i != 0)
                        sb.Append(",");

                    i++;

                    sb.Append("{url:'" + item + "', cb: function(){" + jsfiles[item] + "} }");
                }

                sb.Append("]");

                sb.Append("});");
            }

            sb.Append(scripts.Join(";"));

            sb.Append("});");

            sb.Append("</script>");

            return sb.ToString();
        }

        #endregion

        #region utils

        public string UrlEncode(string str)
        {
            return HttpUtility.UrlEncode(str);
        }

        public string HtmlEncode(string str)
        {
            return HttpUtility.HtmlEncode(str);
        }

        /// <summary>
        /// 指向当前站点的主题目录
        /// </summary>
        public string ThemePath
        {
            get
            {
                return url(string.Format("/themes/{0}", Site.Theme));
            }
        }

        /// <summary>
        /// 指向主站点的主题目录
        /// </summary>
        public string DefaultThemePath
        {
            get
            {
                ISite site = Kiss.Web.SiteConfig.Instance;

                string baseurl = string.Format("/themes/{0}", site.Theme);

                return StringUtil.CombinUrl(site.VirtualPath, baseurl);
            }
        }

        #endregion
    }
}
