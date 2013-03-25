﻿// Copyright 2012 Max Toro Q.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;
using MvcCodeRouting.Web.Http;

namespace MvcCodeRouting {
   
   public static class CodeRoutingHttpExtensions {

      static CodeRoutingHttpExtensions() {
         CodeRoutingProvider.RegisterProvider(new HttpCodeRoutingProvider());
      }

      internal static void Initialize() { }

      public static ICollection<IHttpRoute> MapCodeRoutes(this HttpConfiguration configuration, Type rootController) {
         return MapCodeRoutes(configuration, rootController, null);
      }

      public static ICollection<IHttpRoute> MapCodeRoutes(this HttpConfiguration configuration, Type rootController, CodeRoutingSettings settings) {
         return MapCodeRoutes(configuration, null, rootController, settings);
      }

      public static ICollection<IHttpRoute> MapCodeRoutes(this HttpConfiguration configuration, string baseRoute, Type rootController) {
         return MapCodeRoutes(configuration, baseRoute, rootController, null);
      }

      public static ICollection<IHttpRoute> MapCodeRoutes(this HttpConfiguration configuration, string baseRoute, Type rootController, CodeRoutingSettings settings) {

         if (configuration == null) throw new ArgumentNullException("configuration");
         if (rootController == null) throw new ArgumentNullException("rootController");

         if (settings != null)
            settings = new CodeRoutingSettings(settings);

         var registerSettings = new RegisterSettings(null, rootController) {
            BaseRoute = baseRoute,
            Settings = settings
         };

         registerSettings.Settings.HttpConfiguration(configuration);

         IHttpRoute[] newRoutes = RouteFactory.CreateRoutes<IHttpRoute>(registerSettings);

         foreach (IHttpRoute route in newRoutes) {
            // TODO: in Web API v1 name cannot be null
            configuration.Routes.Add((configuration.Routes.Count + 1).ToString(CultureInfo.InvariantCulture), route);
         }

         EnableCodeRouting(configuration);

         return newRoutes;
      }

      internal static HttpConfiguration HttpConfiguration(this CodeRoutingSettings settings) {

         if (settings == null) throw new ArgumentNullException("settings");

         object httpConfiguration;

         if (settings.Properties.TryGetValue("HttpConfiguration", out httpConfiguration))
            return httpConfiguration as HttpConfiguration;

         return null;
      }

      internal static void HttpConfiguration(this CodeRoutingSettings settings, HttpConfiguration configuration) {

         if (settings == null) throw new ArgumentNullException("settings");

         settings.Properties["HttpConfiguration"] = configuration;
      }

      internal static void EnableCodeRouting(HttpConfiguration configuration) {
         
         if (!(configuration.Services.GetHttpControllerSelector() is CustomHttpControllerSelector))
            configuration.Services.Replace(typeof(IHttpControllerSelector), new CustomHttpControllerSelector(configuration));
      }
   }
}
