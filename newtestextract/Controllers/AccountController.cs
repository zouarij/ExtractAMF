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
            return View();
        }

        [HttpPost]
        public IActionResult AddUser(User user)
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                string sql = "INSERT INTO Users (Username, Password) VALUES (@Username, @Password)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Username", user.Username);
                cmd.Parameters.AddWithValue("@Password", HashPassword(user.Password));
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("ListUsers");
        }

        public IActionResult ListUsers()
        {
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
        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            // Validate empty fields
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                string query = "SELECT COUNT(*) FROM Users WHERE Username = @username AND Password = @password";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", HashPassword(password));
                conn.Open();
                int count = (int)cmd.ExecuteScalar();

                if (count > 0)
                {
                    HttpContext.Session.SetString("username", username);
                    return RedirectToAction("Index", "Export");
                }
                else
                {
                    ViewBag.Error = "Invalid username or password.";
                    return View();
                }
            }
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

