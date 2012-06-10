﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MvcCodeRouting;

namespace Samples.Controllers.Admin {
   
   [Authorize]
   public class UserController : Controller {

      public ActionResult Index() {
         return View();
      }

      [CustomRoute("{id}")]
      public ActionResult Edit([FromRoute]int id) {

         this.ViewBag.UserId = id;

         return View();
      }
   }
}