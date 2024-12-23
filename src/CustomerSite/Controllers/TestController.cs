using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.SaaS.Accelerator.CustomerSite.Controllers;
public class TestController : Controller
{
    // GET: TestController
    public ActionResult Index()
    {
        return View();
    }
}
