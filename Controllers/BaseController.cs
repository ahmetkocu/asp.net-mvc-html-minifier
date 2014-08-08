using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcMinifyHtml.Controllers
{
    public class BaseController : Controller
    {
        protected override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            var response = filterContext.HttpContext.Response;
            ResponseFilterStream filter = new ResponseFilterStream(response.Filter);
            filter.TransformString += filter_TransformString;

            response.Filter = filter;

            filter.Dispose();
            filter.Flush();
            filter.Close();

            base.OnResultExecuted(filterContext);
        }

        string filter_TransformString(string output)
        {
            MinifyEngine min = new MinifyEngine();
            return min.Minify(output, true, true);
        }
    }
}