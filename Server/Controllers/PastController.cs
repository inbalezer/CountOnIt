using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TriangleDbRepository;

namespace CountOnIt.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PastController : ControllerBase
    {
        private readonly DbRepository _db;

        public PastController(DbRepository db)
        {
            _db = db;
        }

        [HttpGet()]
        public async Task<IActionResult> GetFullUser()
        {

            object param = new { };

            string GetUser = "SELECT firstName FROM users WHERE id=1";
            var userRecord = await _db.GetRecordsAsync<string>(GetUser, param);
            string idToGet = userRecord.FirstOrDefault();

            if (idToGet != null)
            {
                return Ok(idToGet);
            }
            return BadRequest("Pulling match Failed");

        }
    }
}
