using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Configuration;
using System.Web.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace Polidea.Websites
{
    public class UrlUtility
    {
        public static string Urlize(string txt)
        {
            string s = "";
            for (int i = 0; i < txt.Length; i++)
                if (Char.IsWhiteSpace(txt[i]) && s.Length > 0 && s[s.Length - 1] != '_')
                    s += '_';
                else
                    s += txt[i];
            return HttpUtility.UrlEncode(s);
        }
    }

    public class Host
    {
        [XmlAttribute("name")]
        public string name;
    }

    public class Rewrite
    {
        [XmlAttribute("targetUrl")]
        public string targetUrl;

        [XmlAttribute("destinationUrl")]
        public string destinationUrl;

        [XmlAttribute("permanentRedir")]
        public bool permanentRedir = false;

        [XmlAttribute("isAbsoluteURI")]
        public bool isAbsoluteURI = false;

        [XmlAttribute("ignoreCase")]
        public bool ignoreCase = false;

        [XmlIgnore]
        public Regex Regex;
    }

    [XmlRoot("rewrites")]
    public class Rewrites
    {
        [XmlElement("add")]
        public Rewrite[] rewrites;

        [XmlElement("host")]
        public Host[] hosts;
    }

    /// <summary>
    /// The purpose of this module is to redirect requests 
    /// based on web.config data in the &lt;redirections&gt; section.
    /// </summary>

    public class Rewriter : IHttpModule
    {
        public static string RedirectPageContent = "<HTML><HEAD><meta http-equiv=\"content-type\" content=\"text/html;charset=utf-8\"><TITLE>301 Moved</TITLE></HEAD><BODY><H1>301 Moved</H1>The document has moved<A HREF=\"{0}\">here</A>.</BODY></HTML>";
        private List<Rewrite> _Rewrites;
        private string _Host;
        bool isLoaded;

        public Rewriter()
        {
            isLoaded = false;
            _Rewrites = new List<Rewrite>();
        }

        #region Private members

        private void OnBeginRequest(object sender, EventArgs e)
        {
            HttpApplication app = sender as HttpApplication;

            if (!isLoaded)
                Reload(app);

            if (app.Request.RawUrl.EndsWith("ResetCache__"))
            {
                Reload(app);
                app.Context.Response.Redirect("/");
            }

            if (app.Request.Url.Host != "localhost" && _Host != null && app.Request.Url.Host != _Host)
            {
                String destinationUrl = "http://"+ _Host + app.Request.Url.PathAndQuery;

                app.Response.Clear();
                app.Response.Write(String.Format(RedirectPageContent, destinationUrl));

                app.Response.StatusCode = 301; // make a permanent redirect
                app.Response.AddHeader("Location", destinationUrl);
                app.Response.End();
            }   

            foreach (Rewrite rewrite in _Rewrites)
            {
                string requestUrl = rewrite.isAbsoluteURI ? app.Request.Url.AbsoluteUri : app.Request.Url.PathAndQuery;

                if (rewrite.Regex.IsMatch(requestUrl))
                {
                    string destinationUrl = rewrite.Regex.Replace(requestUrl, rewrite.destinationUrl, 1);

                    if (rewrite.permanentRedir)
                    {
                        app.Response.Clear();
                        app.Response.Write(String.Format(RedirectPageContent, destinationUrl));

                        app.Response.StatusCode = 301; // make a permanent redirect
                        app.Response.AddHeader("Location", destinationUrl);
                        app.Response.End();
                    }
                    else
                    {
                        //crossPostBackForm will use it for the form's action attribute
                        HttpContext.Current.Items["PostUrl"] = requestUrl;

                        app.Context.RewritePath(destinationUrl);
                    }
                    break;
                }
            }
        }

        #endregion

        public void Reload(System.Web.HttpApplication context)
        {
            lock (_Rewrites)
            {
                _Rewrites.Clear();

                XmlSerializer xs = new XmlSerializer(typeof(Rewrites));
                FileStream fs = File.OpenRead( context.Request.PhysicalApplicationPath+@"App_Data\rewrite.xml");
                Rewrites xml = (Rewrites)xs.Deserialize(fs);
                fs.Close();

                if(xml.rewrites != null)
                    foreach (Rewrite rewrite in xml.rewrites)
                    {
                        rewrite.Regex = new Regex(rewrite.targetUrl, rewrite.ignoreCase ? RegexOptions.IgnoreCase | RegexOptions.Compiled : RegexOptions.Compiled);

                        _Rewrites.Add(rewrite);
                    }

                if (xml.hosts != null && xml.hosts.Length>0)
                    _Host = xml.hosts[0].name;

                isLoaded = true;
            }
        }

        #region IHttpModule members

        public void Init(System.Web.HttpApplication context)
        {
            context.BeginRequest += new EventHandler(OnBeginRequest);
        }

        public void Dispose()
        {
        }

        #endregion
    }

}

