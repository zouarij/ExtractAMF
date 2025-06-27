using System.Data;
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
        private readonly ILogger<exportController> _logger;
        public exportController(IConfiguration config, ILogger<exportController> logger)
        {
            _config = config;
            _logger = logger; // ⬅️ Cette ligne manquait
        }

        public IActionResult Index(IFormCollection form = null, int page = 1, int pageSize = 1000)
        {
            var user = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Login", "Account");

            var rows = new List<List<string>>();
            var columns = new List<string>();
            var totalRows = 0;

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                var cmd = new SqlCommand();
                cmd.Connection = conn;

                var query = new StringBuilder("SELECT * FROM TestData WHERE 1=1");
                var countQuery = new StringBuilder("SELECT COUNT(*) FROM TestData WHERE 1=1");

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

                var hasFilter = filterFields.Keys.Any(k => form != null && form.ContainsKey(k) && !string.IsNullOrWhiteSpace(form[k]));

                if (!hasFilter)
                {
                    ViewBag.ShowTable = false;
                    ViewBag.Columns = new List<string>();
                    ViewBag.PageData = new List<List<string>>();
                    return View();
                }

                foreach (var key in filterFields.Keys)
                {
                    var value = form[key];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (key == "DateEffetStart" && form["DateEffetEnd"].Count > 0)
                        {
                            if (DateTime.TryParse(form["DateEffetStart"], out var start) &&
                                DateTime.TryParse(form["DateEffetEnd"], out var end))
                            {
                                end = end.Date.AddDays(1).AddTicks(-1);
                                query.Append(" AND dateeffet >= @DateEffetStart AND dateeffet <= @DateEffetEnd");
                                countQuery.Append(" AND dateeffet >= @DateEffetStart AND dateeffet <= @DateEffetEnd");
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
                                countQuery.Append(" AND DatTraitement >= @DatTraitementStart AND DatTraitement <= @DatTraitementEnd");
                                cmd.Parameters.AddWithValue("@DatTraitementStart", start);
                                cmd.Parameters.AddWithValue("@DatTraitementEnd", end);
                            }
                        }
                        else if (!key.Contains("DateEffet") && !key.Contains("DatTraitement"))
                        {
                            query.Append($" AND {key} = @{key}");
                            countQuery.Append($" AND {key} = @{key}");
                            cmd.Parameters.AddWithValue($"@{key}", value.ToString());
                        }
                    }
                }

                int offset = (page - 1) * pageSize;
                query.Append(" ORDER BY DateEffet DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                // First get column names and data
                cmd.CommandText = query.ToString();
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    while (reader.Read())
                    {
                        var row = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader[i].ToString());
                        }
                        rows.Add(row);
                    }
                }

                // Now get count
                cmd.Parameters.Remove(cmd.Parameters["@Offset"]);
                cmd.Parameters.Remove(cmd.Parameters["@PageSize"]);
                cmd.CommandText = countQuery.ToString();
                totalRows = (int)cmd.ExecuteScalar();
            }

            int totalPages = (int)Math.Ceiling((double)totalRows / pageSize);

            ViewBag.ShowTable = true;
            ViewBag.Columns = columns;
            ViewBag.PageData = rows;
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

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
                    SELECT DISTINCT TOP 15  CodIsin
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
        [HttpPost]
        public IActionResult Export(IFormCollection form, int page = 1, int pageSize = 1000, string sortColumn = null, string sortDirection = "DESC")
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");
            var csv = new StringBuilder();

            using (var conn = new SqlConnection(connectionString))
            {
                var query = new StringBuilder("SELECT * FROM dbo.TestData WHERE 1=1");
                var cmd = new SqlCommand();
                cmd.Connection = conn;
                cmd.CommandTimeout = 180; // 3 minutes timeout

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
                                query.Append(" AND dateffet >= @DateEffetStart AND dateffet <= @DateEffetEnd");
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
                    _logger.LogInformation("Final Query: {Query}", query.ToString());

                    foreach (SqlParameter param in cmd.Parameters)
                    {
                        _logger.LogInformation("Parameter: {Name} = {Value}", param.ParameterName, param.Value);
                    }
                }

                // Allowed columns for sorting, include 'dateffet' exactly as in DB
                var validColumns = new HashSet<string>(filterFields.Keys.Select(k => k.ToLower()))
        {
            "id", "dateffet" // add other columns you want to allow sorting by
        };

                // Normalize and validate sort column
                if (string.IsNullOrEmpty(sortColumn) || !validColumns.Contains(sortColumn.ToLower()))
                {
                    sortColumn = "dateffet"; // default sort column
                }

                sortDirection = sortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";

                query.Append($" ORDER BY {sortColumn} {sortDirection}");

                int offset = (page - 1) * pageSize;
                query.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                cmd.CommandText = query.ToString();

                conn.Open();
                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    // Write CSV header
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        csv.Append(reader.GetName(i));
                        if (i < reader.FieldCount - 1) csv.Append(",");
                    }
                    csv.AppendLine();

                    // Write CSV rows
                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object rawValue = reader[i];
                            string value = rawValue is DateTime dt
                                ? dt.ToString("yyyy-MM-dd")
                                : rawValue?.ToString() ?? "";

                            value = value.Replace("\"", "\"\"");
                            csv.Append($"\"{value}\"");
                            if (i < reader.FieldCount - 1) csv.Append(",");
                        }
                        csv.AppendLine();
                    }
                }
            }

            byte[] fileBytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(fileBytes, "text/csv", $"export_page{page}.csv");
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