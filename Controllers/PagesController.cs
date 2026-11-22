using Microsoft.AspNetCore.Mvc;

namespace web.Controllers;

// Kurumsal ve bilgilendirme sayfaları (içerik View'larda statiktir).
public class PagesController : Controller
{
    [Route("hakkimizda")]
    public IActionResult About() => View();

    [Route("iletisim")]
    public IActionResult Contact() => View();

    [Route("sss")]
    public IActionResult Faq() => View();

    [Route("ahsap-rehberi")]
    public IActionResult WoodGuide() => View();

    [Route("kvkk")]
    public IActionResult Kvkk() => View();
}
