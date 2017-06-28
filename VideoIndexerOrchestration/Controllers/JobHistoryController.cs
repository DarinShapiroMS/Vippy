using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VideoIndexerOrchestration.Models;

namespace VideoIndexerOrchestration.Controllers
{
    public class JobHistoryController : Controller
    {
        // GET: JobHistory
        [ActionName("Index")]
        public async Task<ActionResult> Index()
        {
            var items = await DocDbRepository<VIJob>.GetItemsAsync(q => q.VIId != null);
            return View(items);
        }
    }
}