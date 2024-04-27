using CountOnIt.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TriangleDbRepository;
using static System.Net.WebRequestMethods;

namespace CountOnIt.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class GoogleController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DbRepository _db;

        public GoogleController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public GoogleController(DbRepository db)
        {
            _db = db;
        }

        [HttpGet("GetUserId")]
        public async Task<IActionResult> GetUserId()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user != null)
            {
                return Ok(user.Id);
            }
            return NotFound();
        }     
    }
}
