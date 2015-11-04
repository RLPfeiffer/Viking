﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc; 
using System.Web.Routing;
using System.ComponentModel;
using System.Net;

namespace DataExport
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801


    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Network_Dot", "Network/Dot",
                            new { controller = "Network", action = "GetDot"},
                            new { controller = "Network"});

            routes.MapRoute("Network_TLP", "Network/TLP",
                            new { controller = "Network", action = "GetTLP" },
                            new { controller = "Network" });

            routes.MapRoute("Network_GML", "Network/GraphML",
                            new { controller = "Network", action = "GetGML" },
                            new { controller = "Network" });

            routes.MapRoute("Network_JSON", "Network/JSON",
                            new { controller = "Network", action = "GetJSON" },
                            new { controller = "Network" });

            routes.MapRoute("Motifs_Dot", "Motifs/Dot",
                            new { controller = "Motif", action = "GetDot"},
                            new { controller = "Motif"});

            routes.MapRoute("Motifs_TLP", "Motifs/TLP",
                            new { controller = "Motif", action = "GetTLP" },
                            new { controller = "Motif" });

            routes.MapRoute("Motifs_JSON", "Motifs/JSON",
                            new { controller = "Motif", action = "GetJSON" },
                            new { controller = "Motif" });

            routes.MapRoute("Morphology_TLP", "Morphology/TLP",
                            new { controller = "Morphology", action = "GetTLP" },
                            new { controller = "Morphology" });

            routes.MapRoute("Morphology_JSON", "Morphology/JSON",
                            new { controller = "Morphology", action = "GetJSON" },
                            new { controller = "Morphology" });

            //Ignore the root level so we can load our static index.html file
            routes.IgnoreRoute("");
        }

        protected void Application_Start()
        {
            
            RegisterRoutes(RouteTable.Routes);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }
    }
}