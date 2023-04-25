using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TestTask.Models;
using CsvHelper;


namespace TestTask.Controllers
{
    [Route("users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UsersContext _context;
        private readonly ILogger _logger;

        public UsersController(UsersContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            _logger.LogInformation("Get all Users at {DateTime.Now}", DateTime.Now);

            if (!await _context.Users.AnyAsync())
            {
                _logger.LogWarning("Users not found at {DateTime.Now}", DateTime.Now);
                return NotFound();
            }

           var users = await _context.Users.Select(ob => new User
                  {
                      Id = ob.Id,
                      FirstName = ob.FirstName,
                      LastName = ob.LastName,   

                  })
                .ToListAsync();

            _logger.LogInformation("Get method succecful at {DateTime.Now}", DateTime.Now);
            return users;
        }

        
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            _logger.LogInformation("Get one User to id({id}) at {DateTime.Now}", id, DateTime.Now);

            if (!await _context.Users.AnyAsync())
            {
                _logger.LogWarning("Users not found at {DateTime.Now}", DateTime.Now);
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                _logger.LogWarning("User not found at {DateTime.Now}", DateTime.Now);
                return NotFound();
            }

            return user;
        }

        
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User model)
        {
            _logger.LogInformation("Put one User to id({id}) at {DateTime.Now}", id, DateTime.Now);

            if (id != model.Id)
            {
                _logger.LogWarning("BadRequest for a put request with the current id({id}) at {DateTime.Now}", id, DateTime.Now);
                return BadRequest();
            }

            
            var user = await _context.Users.FindAsync(id);
            
            if (!UserExists(id))
            {
                _logger.LogWarning("User not found for a put request with the current id({id}) at {DateTime.Now}", id, DateTime.Now);
                return NotFound();
            }

            try
            {
                user.LastName = model.LastName;
                user.Address = model.Address;
                user.Phone = model.Phone;
                user.Email = model.Email;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {

                _logger.LogWarning(ex, "Failed to update user with id {id} at {DateTime.Now}", id, DateTime.Now);
                return StatusCode(500, "Failed to update");

            }

            return NoContent();
        }

        private bool UserExists(int id)
        {
            return (_context.Users?.Any(e => e.Id == id)).GetValueOrDefault();
        }


        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            _logger.LogInformation("Post one User at {DateTime.Now}", DateTime.Now);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            _logger.LogInformation("Delete one User to id({id}) at {DateTime.Now}", id, DateTime.Now);

            if (!await _context.Users.AnyAsync())
            {
                _logger.LogWarning("Entity set 'UsersContext.Users'  is nullat {DateTime.Now}", DateTime.Now);
                return NotFound();
            }
 
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("NotFound for a delete request with the current id({id}) at {DateTime.Now}", id, DateTime.Now);
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("import/json")]
        public async Task<IActionResult> ImportJson(IFormFile file)
        {
            _logger.LogInformation("ImportJson all Users at {DateTime.Now}", DateTime.Now);

            if (file == null || file.Length == 0)
            {
                _logger.LogError("ImportJson: file is null or empty at {DateTime.Now}", DateTime.Now);
                return BadRequest("File is null or empty");
            }

            try
            {
                using var streamReader = new StreamReader(file.OpenReadStream());
                var json = await streamReader.ReadToEndAsync();

                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogError("Reading the file was not compressed at {DateTime.Now}", DateTime.Now);
                    return BadRequest("Json reading the file was not compressed");
                }

                var users = JsonConvert.DeserializeObject<List<User>>(json);

                if (users == null || users.Count == 0)
                {
                    _logger.LogWarning("No users found in the json file at {DateTime.Now}", DateTime.Now);
                    return BadRequest("No users found in the json file");
                }

                _context.Users.AddRange(users);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully imported json at {DateTime.Now}", DateTime.Now);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errorimporting jsonat {DateTime.Now}", DateTime.Now);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error importing json");
            }
        }

        [HttpGet("export/json")]
        public async Task<IActionResult> ExportJson()
        {
            try
            {
                var users = await _context.Users.ToListAsync();

                if (users == null || !users.Any())
                {
                    _logger.LogWarning("Users not found at {DateTime.Now}", DateTime.Now);
                    return NotFound();
                }

                var json = JsonConvert.SerializeObject(users);

                var fileName = $"{DateTime.Now}.json";
                var bytes = Encoding.UTF8.GetBytes(json);

                _logger.LogInformation("Successfully exported json at {DateTime.Now}", DateTime.Now);
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting json at {DateTime.Now}", DateTime.Now);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }



        [HttpPost("import/exel")]
        public async Task<IActionResult> ImportExel(IFormFile file)
        {
            _logger.LogInformation("ImportExcel all Users at {DateTime.Now}", DateTime.Now);

            if (file == null || file.Length == 0)
            {
                _logger.LogError("File is null or empty at {DateTime.Now}", DateTime.Now);
                return BadRequest("File is null or empty");
            }

            try
            {
                using var streamReader = new StreamReader(file.OpenReadStream());
                using var csvReader = new CsvHelper.CsvReader(streamReader, CultureInfo.InvariantCulture);

                var users = csvReader.GetRecords<User>().ToList();

                if (users == null || users.Count == 0)
                {
                    _logger.LogWarning("ImportExcel: no users found in the excel file at {DateTime.Now}", DateTime.Now);
                    return BadRequest("No users found in the excel file");
                }

                _context.Users.AddRange(users);
                await _context.SaveChangesAsync();


                _logger.LogInformation("Successfully imported excel at {DateTime.Now}", DateTime.Now);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing json at {DateTime.Now}", DateTime.Now);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error importing json file");
            }
        }

        [HttpGet("export/exel")]
        public async Task<IActionResult> ExportExel()
        {
            try
            {
                var users = await _context.Users.ToListAsync();

                if (users == null || !users.Any())
                {
                    _logger.LogWarning("Users not found at {DateTime.Now}", DateTime.Now);
                    return NotFound();
                }

                var builder = new StringBuilder();
                builder.AppendLine("Id,FirstName,LastName,BirthDate,Address,Phone,Email");

                foreach (var user in users)
                {
                    builder.AppendLine($"{user.Id},{user.FirstName},{user.LastName},{user.BirthDate},{user.Address},{user.Phone},{user.Email}");
                }

                var fileName = $"{DateTime.Now}.csv";
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());

                _logger.LogInformation("Successfully exported excel at {DateTime.Now}", DateTime.Now);

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting excel at {DateTime.Now}", DateTime.Now);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

    }

}
