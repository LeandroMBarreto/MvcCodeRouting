﻿// Copyright 2013 Max Toro Q.
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
using System.Globalization;

namespace MvcCodeRouting.ParameterBinding.Binders {

   /// <summary>
   /// Binds <see cref="UInt16"/> route parameters.
   /// </summary>
   public class UInt16ParameterBinder : ParameterBinder {

      /// <summary>
      /// Returns the <see cref="Type"/> for <see cref="UInt16"/>.
      /// </summary>
      public override Type ParameterType {
         get { return typeof(ushort); }
      }

      /// <summary>
      /// Attempts to bind a route parameter.
      /// </summary>
      /// <param name="value">The value of the route parameter.</param>
      /// <param name="provider">The format provider to be used.</param>
      /// <param name="result">The bound value, an instance of <see cref="UInt16"/>.</param>
      /// <returns>true if the parameter is successfully bound; else, false.</returns>
      public override bool TryBind(string value, IFormatProvider provider, out object result) {

         result = null;

         if (String.IsNullOrWhiteSpace(value)) {
            return false;
         }

         const ushort zero = 0;
         ushort parsedResult;

         if (!UInt16.TryParse(value, NumberStyles.None, provider, out parsedResult)) {
            return false;
         }

         // disallow leading sign or leading zero

         if (parsedResult == zero
            && value.Length != 1) {

            return false;
         }

         if (parsedResult > zero
            && (value[0] == '+' || value[0] == '0')) {

            return false;
         }

         result = parsedResult;

         return true;
      }
   }
}
