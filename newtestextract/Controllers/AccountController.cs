using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using newtestextract.Models;
namespace newtestextract.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connStr;

        public AccountController(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection");
        }


        [HttpGet]
        public IActionResult AddUser()
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "admin")
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View();
        }

        [HttpPost]
        public IActionResult AddUser(User newUser)
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "admin")
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            if (string.IsNullOrWhiteSpace(newUser.Username) ||
                string.IsNullOrWhiteSpace(newUser.Password) ||
                string.IsNullOrWhiteSpace(newUser.Role))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();

                // Check if the username already exists
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @Username", conn);
                checkCmd.Parameters.AddWithValue("@Username", newUser.Username);
                int userCount = (int)checkCmd.ExecuteScalar();

                if (userCount > 0)
                {
                    ViewBag.Error = "This username already exists. Please choose another.";
                    return View();
                }

                // Insert the new user
                var insertCmd = new SqlCommand("INSERT INTO Users (Username, Password, Role) VALUES (@Username, @Password, @Role)", conn);
                insertCmd.Parameters.AddWithValue("@Username", newUser.Username);
                insertCmd.Parameters.AddWithValue("@Password", newUser.Password); // Or hash it
                insertCmd.Parameters.AddWithValue("@Role", newUser.Role);

                insertCmd.ExecuteNonQuery();
            }

            return RedirectToAction("ListUsers");
        }

        public IActionResult ListUsers()
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "admin")
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                string sql = "SELECT Id, Username, Password FROM Users";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    users.Add(new User
                    {
                        Id = (int)reader["Id"],
                        Username = reader["Username"].ToString(),
                        Password = reader["Password"].ToString()
                    });
                }
            }

            return View(users);
        }


       

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                string query = "SELECT Id, Username, Password, Role FROM Users WHERE Username = @username AND Password = @password";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", HashPassword(password));
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var user = new User
                        {
                            Id = (int)reader["Id"],
                            Username = reader["Username"].ToString(),
                            Password = reader["Password"].ToString(),
                            Role = reader["Role"].ToString()
                        };

                        HttpContext.Session.SetString("username", user.Username);
                        HttpContext.Session.SetString("role", user.Role); 

                        return RedirectToAction("Index", "Export");
                    }
                    else
                    {
                        ViewBag.Error = "Invalid username or password.";
                        return View();
                    }
                }
            }
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
        public ActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
        private string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hashBytes = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }

}

