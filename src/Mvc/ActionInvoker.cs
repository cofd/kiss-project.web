﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;
using Kiss.Security;
using Kiss.Utils;
using Kiss.Web.Utils;

namespace Kiss.Web.Mvc
{
    /// <summary>
    /// mvc controller action invoker
    /// </summary>
    public class ActionInvoker : IActionInvoker
    {
        private readonly SingleEntryGate _executeWasCalledGate = new SingleEntryGate();
        private Dictionary<Type, Dictionary<string, MethodInfo>> _mis = new Dictionary<Type, Dictionary<string, MethodInfo>>();

        public bool IsAsync(JContext jc)
        {
            MethodInfo mi = getActionMethod(jc);
            if (mi == null)
                return false;

            return mi.GetCustomAttributes(typeof(AsyncAttribute), true).Length > 0;
        }

        public bool InvokeAction(JContext jc)
        {
            MethodInfo mi = getActionMethod(jc);

            if (mi == null)
                return false;

            object ret = null;

            try
            {
                if (jc.User != null)
                {
                    object[] attrs = mi.GetCustomAttributes(typeof(PermissionAttribute), true);
                    if (attrs.Length > 0)
                    {
                        PermissionAttribute attr = attrs[0] as PermissionAttribute;
                        if (!string.IsNullOrEmpty(attr.Permission))
                        {
                            if (jc.User.HasPermission(attr.Permission))
                                goto execute;
                            else
                                jc.User.OnPermissionDenied(new PermissionDeniedEventArgs(attr.Permission));
                        }
                    }
                }
                else
                {
                    goto execute;
                }

            execute:

                // before execute action
                Controller.BeforeActionExecuteEventArgs e = new Controller.BeforeActionExecuteEventArgs() { JContext = jc };
                jc.Controller.OnBeforeActionExecute(e);
                if (e.PreventDefault)
                {
                    ret = e.ReturnValue;
                }

                if (jc.IsPost)
                {
                    jc.RenderContent = false;

                    if (!e.PreventDefault)
                    {
                        ret = execute(jc.Controller, mi, jc.Params);
                    }

                    if (ret != null)
                    {
                        if (ret is ActionResult)
                        {
                            ActionResult actionResult = ret as ActionResult;
                            actionResult.ExecuteResult(jc);
                        }
                        else if (!jc.RenderContent)
                        {
                            ResponseUtil.OutputJson(jc.Context.Response, ret);
                        }
                    }                    
                }
                else
                {
                    if (!e.PreventDefault)
                    {
                        ret = execute(jc.Controller, mi, jc.Params);
                    }

                    if (ret != null)
                    {
                        if (ret is ActionResult)
                        {
                            ActionResult actionResult = ret as ActionResult;
                            actionResult.ExecuteResult(jc);
                        }
                        else
                        {
                            jc.RenderContent = false;

                            int cacheMinutes = 0;
                            object[] attrs = mi.GetCustomAttributes(typeof(HttpGetAttribute), false);
                            if (attrs.Length == 1)
                            {
                                cacheMinutes = (attrs[0] as HttpGetAttribute).CacheMinutes;
                            }
                            ResponseUtil.OutputJson(jc.Context.Response, ret, cacheMinutes);
                        }
                    }
                }

                // after execute action
                jc.Controller.OnAfterActionExecute(ret);
            }
            catch (ThreadAbortException) { }// ignore this exception
            catch (Exception ex)
            {
                if (ex is TargetInvocationException)
                    ex = ex.InnerException;

                jc.Controller.logger.Error(ExceptionUtil.WriteException(ex));

                jc.Controller.OnException(ex);
            }

            return true;
        }

        private static object execute(object obj, MethodInfo mi, NameValueCollection nv)
        {
            object ret;

            ParameterInfo[] paras = mi.GetParameters();

            if (paras.Length == 1 && paras[0].ParameterType == typeof(NameValueCollection))
                ret = mi.Invoke(obj, new object[] { nv });
            else if (paras.Length == 0)
                ret = mi.Invoke(obj, null);
            else
            {
                List<object> p = new List<object>();

                foreach (var item in paras)
                {
                    if (item.ParameterType.IsSubclassOf(typeof(Array)))
                    {
                        string v = nv[item.Name] ?? nv[item.Name + "[]"];

                        string[] strs = StringUtil.CommaDelimitedListToStringArray(v);
                        Array array = Array.CreateInstance(item.ParameterType.GetElementType(), strs.Length);
                        for (int i = 0; i < strs.Length; i++)
                        {
                            array.SetValue(TypeConvertUtil.ConvertTo(strs[i], item.ParameterType.GetElementType()), i);
                        }

                        p.Add(array);
                    }
                    else
                    {
                        string v = nv[item.Name];

                        p.Add(TypeConvertUtil.ConvertTo(v, item.ParameterType));
                    }
                }

                ret = mi.Invoke(obj, p.ToArray());
            }
            return ret;
        }

        private MethodInfo getActionMethod(JContext jc)
        {
            Type t = jc.Controller.GetType();

            Dictionary<string, MethodInfo> mis;

            if (_mis.ContainsKey(t))
            {
                mis = _mis[t];
            }
            else
            {
                mis = new Dictionary<string, MethodInfo>();
                _mis[t] = mis;
            }

            MethodInfo mi = null;

            string action = jc.Navigation.Action + ":" + jc.IsPost;

            if (mis.ContainsKey(action))
                mi = mis[action];
            else
            {
                List<MethodInfo> methods = new List<MethodInfo>(t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                methods.Sort((x, y) =>
                {
                    bool hasPostAttr = x.GetCustomAttributes(typeof(HttpPostAttribute), false).Length == 1;
                    bool hasPostAttr_2 = y.GetCustomAttributes(typeof(HttpPostAttribute), false).Length == 1;

                    return hasPostAttr_2.CompareTo(hasPostAttr);
                });

                foreach (MethodInfo m in methods)
                {
                    bool hasPostAttr = m.GetCustomAttributes(typeof(HttpPostAttribute), false).Length == 1;
                    bool hasGetAttr = m.GetCustomAttributes(typeof(HttpGetAttribute), false).Length == 1;
                    bool hasAjaxAttr = m.GetCustomAttributes(typeof(Ajax.AjaxMethodAttribute), true).Length > 0;

                    if (!m.ContainsGenericParameters &&
                        m.Name.Equals(jc.Navigation.Action, StringComparison.InvariantCultureIgnoreCase) &&
                         !hasAjaxAttr &&
                        ((jc.IsPost && hasPostAttr) || (!jc.IsPost && hasGetAttr) || (!hasPostAttr && !hasGetAttr)))
                    {
                        mi = m;
                        mis[action] = mi;
                        break;
                    }
                    mis[action] = null;
                }
            }
            return mi;
        }
    }
}
