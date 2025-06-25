using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace newtestextract.Controllers
{
    public class exportController : Controller
    {
        private readonly IConfiguration _config;

        public exportController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            var user = HttpContext.Session.GetString("username");

            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Login", "Account");

            ViewBag.StatusOptions = new[] { "Active", "Pending", "Closed" };
            return View();
        }
        [HttpGet]
        public JsonResult SearchCodIsin(string term)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var result = new List<string>();

            using (var conn = new SqlConnection(connectionString))
            {
                string query = "SELECT DISTINCT CodIsin FROM TestData WHERE CodIsin LIKE @term + '%'";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@term", term ?? "");

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return Json(result);
        }
        [HttpPost]
        public IActionResult Export(IFormCollection form)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var csv = new StringBuilder();

            using (var conn = new SqlConnection(connectionString))
            {
                var query = new StringBuilder("SELECT * FROM dbo.TestData WHERE 1=1");
                var cmd = new SqlCommand();

                // List of known filterable columns and their types
                var filterFields = new Dictionary<string, string>
                {
                    ["DatTraitementStart"] = "date",
                    ["DatTraitementEnd"] = "date",
                    ["CodSens"] = "text",
                    ["Qte"] = "number",
                    ["Crs"] = "number",
                    ["MntBrutDevNegociation"] = "number",
                    ["NumCompte"] = "text",
                    ["CodSociete"] = "text",
                    ["CodAssiste"] = "text",
                    ["NomAbrege"] = "text",
                    ["TypGestion"] = "text",
                    ["CodIsin"] = "text",
                    ["LibValeur"] = "text",
                    ["CodOperation"] = "text",
                    ["CodMarche"] = "text",
                    ["CodAnnulation"] = "text",
                    ["Nom"] = "text",
                    ["Adr1"] = "text",
                    ["Adr2"] = "text",
                    ["Adr3"] = "text",
                    ["Adr4"] = "text",
                    ["Adr5"] = "text",
                    ["Adr6"] = "text",
                    ["AdrVille"] = "text",
                    ["CodGerant"] = "text",
                    ["CategorisationMIFID"] = "text",
                    ["LieuNegociation"] = "text"
                };

                foreach (var key in filterFields.Keys)
                {
                    var value = form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (key == "DatTraitementStart" && form["DatTraitementEnd"].Count > 0)
                        {
                            query.Append(" AND DatTraitement >= @DatTraitementStart AND DatTraitement <= @DatTraitementEnd");
                            cmd.Parameters.AddWithValue("@DatTraitementStart", DateTime.Parse(form["DatTraitementStart"]));
                            cmd.Parameters.AddWithValue("@DatTraitementEnd", DateTime.Parse(form["DatTraitementEnd"]));
                        }
                        else if (!key.Contains("DatTraitement")) // avoid double-handling date
                        {
                            query.Append($" AND {key} = @{key}");
                            cmd.Parameters.AddWithValue($"@{key}", value.ToString());
                        }
                    }
                }

                cmd.CommandText = query.ToString();
                cmd.Connection = conn;

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        csv.Append(reader.GetName(i));
                        if (i < reader.FieldCount - 1) csv.Append(",");
                    }
                    csv.AppendLine();

                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object rawValue = reader[i];
                            string value = rawValue is DateTime dt
                                ? dt.ToString("yyyy-MM-dd")
                                : rawValue.ToString();

                            value = value.Replace("\"", "\"\"");
                            csv.Append($"\"{value}\"");
                            if (i < reader.FieldCount - 1) csv.Append(",");
                        }
                        csv.AppendLine();
                    }
                }
            }

           

        byte[] fileBytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(fileBytes, "text/csv", "export.csv");
        }


    }
}
