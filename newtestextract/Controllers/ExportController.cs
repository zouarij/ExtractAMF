using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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

            return View();
        }

        [HttpGet]
        public JsonResult SearchCodIsin(string term)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var result = new List<string>();

            using (var conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT TOP 15 DISTINCT CodIsin
                    FROM TestData
                    WHERE CodIsin LIKE @term + '%'
                    ORDER BY CodIsin";

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

                var filterFields = new Dictionary<string, string>
                {
                    ["DateEffetStart"] = "date",
                    ["DateEffetEnd"] = "date",
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
                var hasFilter = filterFields.Keys.Any(key =>
               form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key])
           );
                if (!hasFilter)
                {
                    TempData["Error"] = "Please select at least one filter before exporting.";
                    return RedirectToAction("Index");
                }
                foreach (var key in filterFields.Keys)
                {
                    var value = form[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (key == "DateEffetStart" && form["DateEffetEnd"].Count > 0)
                        {
                            if (DateTime.TryParse(form["DateEffetStart"], out var start) &&
                                DateTime.TryParse(form["DateEffetEnd"], out var end))
                            {
                                end = end.Date.AddDays(1).AddTicks(-1);
                                query.Append(" AND DateEffet >= @DateEffetStart AND DateEffet <= @DateEffetEnd");
                                cmd.Parameters.AddWithValue("@DateEffetStart", start);
                                cmd.Parameters.AddWithValue("@DateEffetEnd", end);
                            }
                        }
                        else if (key == "DatTraitementStart" && form["DatTraitementEnd"].Count > 0)
                        {
                            if (DateTime.TryParse(form["DatTraitementStart"], out var start) &&
                                DateTime.TryParse(form["DatTraitementEnd"], out var end))
                            {
                                end = end.Date.AddDays(1).AddTicks(-1);
                                query.Append(" AND DatTraitement >= @DatTraitementStart AND DatTraitement <= @DatTraitementEnd");
                                cmd.Parameters.AddWithValue("@DatTraitementStart", start);
                                cmd.Parameters.AddWithValue("@DatTraitementEnd", end);
                            }
                        }
                        else if (!key.Contains("DateEffet") && !key.Contains("DatTraitement"))
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
/*CREATE PROCEDURE ExportFilteredData
    @DateEffetStart DATE = NULL,
    @DateEffetEnd DATE = NULL,
    @DatTraitementStart DATE = NULL,
    @DatTraitementEnd DATE = NULL,
    @CodSens NVARCHAR(50) = NULL,
    @Qte FLOAT = NULL,
    @Crs FLOAT = NULL,
    @MntBrutDevNegociation FLOAT = NULL,
    @NumCompte NVARCHAR(50) = NULL,
    @CodSociete NVARCHAR(50) = NULL,
    @CodAssiste NVARCHAR(50) = NULL,
    @NomAbrege NVARCHAR(50) = NULL,
    @TypGestion NVARCHAR(50) = NULL,
    @CodIsin NVARCHAR(50) = NULL,
    @LibValeur NVARCHAR(255) = NULL,
    @CodOperation NVARCHAR(50) = NULL,
    @CodMarche NVARCHAR(50) = NULL,
    @CodAnnulation NVARCHAR(50) = NULL,
    @Nom NVARCHAR(255) = NULL,
    @Adr1 NVARCHAR(255) = NULL,
    @Adr2 NVARCHAR(255) = NULL,
    @Adr3 NVARCHAR(255) = NULL,
    @Adr4 NVARCHAR(255) = NULL,
    @Adr5 NVARCHAR(255) = NULL,
    @Adr6 NVARCHAR(255) = NULL,
    @AdrVille NVARCHAR(255) = NULL,
    @CodGerant NVARCHAR(50) = NULL,
    @CategorisationMIFID NVARCHAR(50) = NULL,
    @LieuNegociation NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

SELECT*
FROM TestData
    WHERE
        (@DateEffetStart IS NULL OR DateEffet >= @DateEffetStart) AND
        (@DateEffetEnd IS NULL OR DateEffet <= @DateEffetEnd) AND
        (@DatTraitementStart IS NULL OR DatTraitement >= @DatTraitementStart) AND
        (@DatTraitementEnd IS NULL OR DatTraitement <= @DatTraitementEnd) AND
        (@CodSens IS NULL OR CodSens = @CodSens) AND
        (@Qte IS NULL OR Qte = @Qte) AND
        (@Crs IS NULL OR Crs = @Crs) AND
        (@MntBrutDevNegociation IS NULL OR MntBrutDevNegociation = @MntBrutDevNegociation) AND
        (@NumCompte IS NULL OR NumCompte = @NumCompte) AND
        (@CodSociete IS NULL OR CodSociete = @CodSociete) AND
        (@CodAssiste IS NULL OR CodAssiste = @CodAssiste) AND
        (@NomAbrege IS NULL OR NomAbrege = @NomAbrege) AND
        (@TypGestion IS NULL OR TypGestion = @TypGestion) AND
        (@CodIsin IS NULL OR CodIsin = @CodIsin) AND
        (@LibValeur IS NULL OR LibValeur = @LibValeur) AND
        (@CodOperation IS NULL OR CodOperation = @CodOperation) AND
        (@CodMarche IS NULL OR CodMarche = @CodMarche) AND
        (@CodAnnulation IS NULL OR CodAnnulation = @CodAnnulation) AND
        (@Nom IS NULL OR Nom = @Nom) AND
        (@Adr1 IS NULL OR Adr1 = @Adr1) AND
        (@Adr2 IS NULL OR Adr2 = @Adr2) AND
        (@Adr3 IS NULL OR Adr3 = @Adr3) AND
        (@Adr4 IS NULL OR Adr4 = @Adr4) AND
        (@Adr5 IS NULL OR Adr5 = @Adr5) AND
        (@Adr6 IS NULL OR Adr6 = @Adr6) AND
        (@AdrVille IS NULL OR AdrVille = @AdrVille) AND
        (@CodGerant IS NULL OR CodGerant = @CodGerant) AND
        (@CategorisationMIFID IS NULL OR CategorisationMIFID = @CategorisationMIFID) AND
        (@LieuNegociation IS NULL OR LieuNegociation = @LieuNegociation);
END;*/