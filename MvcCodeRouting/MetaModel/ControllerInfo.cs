﻿// Copyright 2011 Max Toro Q.
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace MvcCodeRouting {
   
   [DebuggerDisplay("{ControllerUrl}")]
   abstract class ControllerInfo {

      internal static readonly Type BaseType = typeof(Controller);
      static readonly Func<Controller, IActionInvoker> createActionInvoker;
      static readonly Func<ControllerActionInvoker, ControllerContext, ControllerDescriptor> getControllerDescriptor;

      ReadOnlyCollection<string> _CodeRoutingNamespace;
      ReadOnlyCollection<string> _CodeRoutingContext;
      ReadOnlyCollection<string> _NamespaceSegments;
      ReadOnlyCollection<string> _BaseRouteAndNamespaceSegments;
      TokenInfoCollection _RouteProperties;
      Collection<ActionInfo> _Actions;
      string _Name;
      string _ControllerSegment;

      public Type Type { get; private set; }
      public RegisterInfo Register { get; private set; }

      public virtual string Name {
         get {
            if (_Name == null) 
               _Name = Type.Name.Substring(0, Type.Name.Length - "Controller".Length);
            return _Name;
         }
      }

      public string Namespace {
         get {
            return Type.Namespace ?? "";
         }
      }

      public bool IsInRootNamespace {
         get {
            return Namespace == Register.RootNamespace
               || IsInSubNamespace;
         }
      }

      public bool IsInSubNamespace {
         get {
            return Namespace.Length > Register.RootNamespace.Length
               && Namespace.StartsWith(Register.RootNamespace + ".", StringComparison.Ordinal);
         }
      }

      public bool IsRootController {
         get {
            return Type == Register.RootController;
         }
      }

      public string ControllerSegment {
         get {
            if (_ControllerSegment == null) 
               _ControllerSegment = Register.Settings.FormatRouteSegment(new RouteFormatterArgs(Name, RouteSegmentType.Controller, Type), caseOnly: false);
            return _ControllerSegment;
         }
      }

      public ReadOnlyCollection<string> CodeRoutingNamespace {
         get {
            if (_CodeRoutingNamespace == null) {

               List<string> segments = new List<string>();

               if (IsInSubNamespace) {
                  
                  segments.AddRange(Namespace.Remove(0, Register.RootNamespace.Length + 1).Split('.'));

                  if (segments.Count > 0 && NameEquals(segments.Last(), Name))
                     segments.RemoveAt(segments.Count - 1);
               }
               _CodeRoutingNamespace = new ReadOnlyCollection<string>(segments);
            }
            return _CodeRoutingNamespace;
         }
      }

      public ReadOnlyCollection<string> CodeRoutingContext {
         get {
            if (_CodeRoutingContext == null) {

               if (String.IsNullOrEmpty(Register.BaseRoute)) {
                  _CodeRoutingContext = new ReadOnlyCollection<string>(CodeRoutingNamespace);
               } else {
                  var segments = new List<string>();
                  segments.AddRange(Register.BaseRoute.Split('/'));
                  segments.AddRange(CodeRoutingNamespace);

                  _CodeRoutingContext = new ReadOnlyCollection<string>(segments);
               }
            }
            return _CodeRoutingContext;
         }
      }

      public ReadOnlyCollection<string> NamespaceSegments {
         get {
            if (_NamespaceSegments == null) {
               var namespaceSegments = new List<string>();

               namespaceSegments.AddRange(
                  CodeRoutingNamespace.Select(s => Register.Settings.FormatRouteSegment(new RouteFormatterArgs(s, RouteSegmentType.Namespace, Type), caseOnly: false))
               );

               _NamespaceSegments = new ReadOnlyCollection<string>(namespaceSegments);
            }
            return _NamespaceSegments;
         }
      }

      public ReadOnlyCollection<string> BaseRouteAndNamespaceSegments {
         get {
            if (_BaseRouteAndNamespaceSegments == null) {

               if (String.IsNullOrEmpty(Register.BaseRoute)) {
                  _BaseRouteAndNamespaceSegments = new ReadOnlyCollection<string>(NamespaceSegments);
               } else {
                  var segments = new List<string>();
                  segments.AddRange(Register.BaseRoute.Split('/'));
                  segments.AddRange(NamespaceSegments);

                  _BaseRouteAndNamespaceSegments = new ReadOnlyCollection<string>(segments);
               }
            }
            return _BaseRouteAndNamespaceSegments;
         }
      }

      public TokenInfoCollection RouteProperties {
         get {
            if (_RouteProperties == null) {

               var types = new List<Type>();

               for (Type t = this.Type; t != null; t = t.BaseType) 
                  types.Add(t);

               types.Reverse();

               var list = new List<TokenInfo>();

               foreach (var type in types) {
                  list.AddRange(
                     from p in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                     where p.IsDefined(typeof(FromRouteAttribute), inherit: false /* [1] */)
                     let rp = CreateTokenInfo(p)
                     where !list.Any(item => TokenInfo.NameEquals(item.Name, rp.Name))
                     select rp
                  );
               }

               _RouteProperties = new TokenInfoCollection(list);
            }
            return _RouteProperties;

            // [1] Procesing each type of the hierarchy one by one, hence inherit: false.
         }
      }

      public Collection<ActionInfo> Actions {
         get {
            if (_Actions == null) {
               _Actions = new Collection<ActionInfo>(
                  (from a in GetActions()
                   where !a.IsDefined(typeof(NonActionAttribute), inherit: true)
                   select a).ToArray()
               );

               CheckOverloads(_Actions);
               CheckCustomRoutes(_Actions);
            }
            return _Actions;
         }
      }

      public string UrlTemplate {
         get {
            return String.Join("/", BaseRouteAndNamespaceSegments
               .Concat((!IsRootController) ? new[] { "{controller}" } : new string[0])
               .Concat(RouteProperties.Select(p => p.RouteSegment))
            );
         }
      }

      public string ControllerUrl {
         get {
            return String.Join("/", BaseRouteAndNamespaceSegments
               .Concat((!IsRootController) ? new[] { ControllerSegment } : new string[0])
               .Concat(RouteProperties.Select(p => p.RouteSegment))
            );
         }
      }

      [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Not a big deal.")]
      static ControllerInfo() {

         try {
            createActionInvoker =
               (Func<Controller, IActionInvoker>)
                  Delegate.CreateDelegate(typeof(Func<Controller, IActionInvoker>), BaseType.GetMethod("CreateActionInvoker", BindingFlags.NonPublic | BindingFlags.Instance));

            getControllerDescriptor =
               (Func<ControllerActionInvoker, ControllerContext, ControllerDescriptor>)
                  Delegate.CreateDelegate(typeof(Func<ControllerActionInvoker, ControllerContext, ControllerDescriptor>), typeof(ControllerActionInvoker).GetMethod("GetControllerDescriptor", BindingFlags.NonPublic | BindingFlags.Instance));
         
         } catch (MethodAccessException) { }
      }

      public static bool NameEquals(string name1, string name2) {
         return String.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
      }

      static void CheckOverloads(IEnumerable<ActionInfo> actions) {

         var overloadedActions =
            (from a in actions
             where a.RouteParameters.Count > 0
             group a by new { a.Controller, Name = a.ActionSegment } into g
             where g.Count() > 1
             select g).ToList();

         var withoutRequiredAttr =
            (from g in overloadedActions
             let distinctParamCount = g.Select(a => a.RouteParameters.Count).Distinct()
             where distinctParamCount.Count() > 1
             let bad = g.Where(a => !a.HasRequireRouteParametersAttribute)
             where bad.Count() > 0
             select bad).ToList();

         if (withoutRequiredAttr.Count > 0) {
            var first = withoutRequiredAttr.First();

            throw new InvalidOperationException(
               String.Format(CultureInfo.InvariantCulture,
                  "The following action methods must be decorated with {0} for disambiguation: {1}.",
                  typeof(RequireRouteParametersAttribute).FullName,
                  String.Join(", ", first.Select(a => String.Concat(a.DeclaringType.FullName, ".", a.MethodName, "(", String.Join(", ", a.Parameters.Select(p => p.Type.Name)), ")")))
               )
            );
         }

         var overloadsComparer = new ActionSignatureComparer();

         var overloadsWithDifferentParameters =
            (from g in overloadedActions
             let ordered = g.OrderByDescending(a => a.RouteParameters.Count).ToArray()
             let first = ordered.First()
             where !ordered.Skip(1).All(a => overloadsComparer.Equals(first, a))
             select g).ToList();

         if (overloadsWithDifferentParameters.Count > 0) {
            var first = overloadsWithDifferentParameters.First();

            throw new InvalidOperationException(
               String.Format(CultureInfo.InvariantCulture,
                  "Overloaded action methods must have parameters that are equal in name, position and constraint ({0}).",
                  String.Concat(first.Key.Controller.Type.FullName, ".", first.First().MethodName)
               )
            );
         }
      }

      static void CheckCustomRoutes(IEnumerable<ActionInfo> actions) { 

         var sameCustomRouteDifferentNames = 
            (from a in actions
             where a.CustomRoute != null
               && !a.CustomRouteHasActionToken
             group a by a.CustomRoute into grp
             let distinctNameCount = grp.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
             where distinctNameCount > 1
             select grp).ToList();

         if (sameCustomRouteDifferentNames.Count > 0) {
            var first = sameCustomRouteDifferentNames.First();
            
            throw new InvalidOperationException(
               String.Format(CultureInfo.InvariantCulture,
                  "Action methods decorated with {0} must have the same name: {1}.",
                  typeof(CustomRouteAttribute).FullName,
                  String.Join(", ", first.Select(a => String.Concat(a.DeclaringType.FullName, ".", a.MethodName, "(", String.Join(", ", a.Parameters.Select(p => p.Type.Name)), ")")))
               )
            );
         }
      }

      public static ControllerInfo Create(Type controllerType, RegisterInfo registerInfo) {

         ControllerDescriptor controllerDescr = null;

         if (createActionInvoker != null) {

            Controller instance = null;

            try {
               instance = (Controller)FormatterServices.GetUninitializedObject(controllerType);
            } catch (SecurityException) { }

            if (instance != null) {

               ControllerActionInvoker actionInvoker = createActionInvoker(instance) as ControllerActionInvoker;

               if (actionInvoker != null)
                  controllerDescr = getControllerDescriptor(actionInvoker, new ControllerContext { Controller = instance });
            }
         }

         if (controllerDescr != null) 
            return new DescriptedControllerInfo(controllerDescr, controllerType, registerInfo);

         return new ReflectedControllerInfo(controllerType, registerInfo);
      }

      protected ControllerInfo(Type type, RegisterInfo registerInfo) {
         
         this.Type = type;
         this.Register = registerInfo;
      }

      protected internal abstract ActionInfo[] GetActions();

      protected IEnumerable<MethodInfo> GetCanonicalActionMethods() {

         bool controllerIsDisposable = typeof(IDisposable).IsAssignableFrom(this.Type);

         return
             from m in this.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
             where !m.IsSpecialName
                && BaseType.IsAssignableFrom(m.DeclaringType)
                && !m.IsDefined(typeof(NonActionAttribute), inherit: true)
                && !(controllerIsDisposable && m.Name == "Dispose" && m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
             select m;
      }

      TokenInfo CreateTokenInfo(PropertyInfo property) {

         Type propertyType = property.PropertyType;

         var routeAttr = property.GetCustomAttributes(typeof(FromRouteAttribute), inherit: true)
            .Cast<FromRouteAttribute>()
            .Single();

         string name = routeAttr.TokenName ?? property.Name;
         string constraint = this.Register.Settings.GetConstraintForType(propertyType, routeAttr);

         return new TokenInfo(name, constraint);
      }
   }
}