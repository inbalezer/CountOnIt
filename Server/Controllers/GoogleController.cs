using CountOnIt.Shared.Models.present.toAdd;
using CountOnIt.Shared.Models.present.toEdit;
using CountOnIt.Shared.Models.present.toShow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TriangleDbRepository;


namespace CountOnIt.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoogleController : ControllerBase
    {
        private readonly DbRepository _db;
        public GoogleController(DbRepository db)
        {
            _db = db;
        }

        [HttpPost("AddUser/{userGoogleID}")] // יצירת משתמש חזש
        public async Task<IActionResult> Adduser(string userGoogleID, UserToAdd userToAdd)
        {
            object userToAddParam = new
            {
                googleID = userGoogleID,
                firstName = userToAdd.firstName,
                lastName = userToAdd.lastName

            };

            string insertUserQuery = "INSERT INTO users (googleID,firstName,lastName) values (@googleID ,@firstName ,@lastName)";

            int newUserId = await _db.InsertReturnId(insertUserQuery, userToAddParam);

            if (newUserId != 0)
            {
                return Ok();
            }

            return BadRequest("user not created");
        }
    }
}