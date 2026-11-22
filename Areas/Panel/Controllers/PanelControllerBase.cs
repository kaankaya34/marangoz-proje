using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace web.Areas.Panel.Controllers;

[Area("Panel")]
[Authorize(Policy = "ManagerOrAdmin")]
public abstract class PanelControllerBase : Controller
{
}
