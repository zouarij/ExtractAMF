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

        public exportController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index(int page = 1)
        {
            var user = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Login", "Account");

            const int pageSize = 1000;
            var allRows = new List<List<string>>();
            var columnNames = new List<string>();

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                var cmd = new SqlCommand("SELECT * FROM TestData", conn);
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columnNames.Add(reader.GetName(i));
                    }

                    while (reader.Read())
                    {
                        var row = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader[i].ToString());
                        }
                        allRows.Add(row);
                    }
                }
            }

            // Pagination logic
            int totalRows = allRows.Count;
            int totalPages = (int)Math.Ceiling((double)totalRows / pageSize);
            var pagedRows = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Columns = columnNames;
            ViewBag.PageData = pagedRows;
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
        public async Task<IActionResult> Export(IFormCollection form, int page = 1, int pageSize = 1000, string sortColumn = null, string sortDirection = "DESC")
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

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

            var query = new StringBuilder("SELECT * FROM dbo.TestData WHERE 1=1");
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand { Connection = conn };

            // ✅ Handle special date filters safely
            if (form.ContainsKey("DateEffetStart") && DateTime.TryParse(form["DateEffetStart"], out var dateEffetStart))
            {
                query.Append(" AND dateffet >= @DateEffetStart");
                cmd.Parameters.AddWithValue("@DateEffetStart", dateEffetStart);
            }
            if (form.ContainsKey("DateEffetEnd") && DateTime.TryParse(form["DateEffetEnd"], out var dateEffetEnd))
            {
                dateEffetEnd = dateEffetEnd.Date.AddDays(1).AddTicks(-1);
                query.Append(" AND dateffet <= @DateEffetEnd");
                cmd.Parameters.AddWithValue("@DateEffetEnd", dateEffetEnd);
            }
            if (form.ContainsKey("DatTraitementStart") && DateTime.TryParse(form["DatTraitementStart"], out var datTraitementStart))
            {
                query.Append(" AND DatTraitement >= @DatTraitementStart");
                cmd.Parameters.AddWithValue("@DatTraitementStart", datTraitementStart);
            }
            if (form.ContainsKey("DatTraitementEnd") && DateTime.TryParse(form["DatTraitementEnd"], out var datTraitementEnd))
            {
                datTraitementEnd = datTraitementEnd.Date.AddDays(1).AddTicks(-1);
                query.Append(" AND DatTraitement <= @DatTraitementEnd");
                cmd.Parameters.AddWithValue("@DatTraitementEnd", datTraitementEnd);
            }

            // ✅ Handle all other filters only if present & not empty
            foreach (var key in filterFields.Keys)
            {
                if (key.Contains("Start") || key.Contains("End")) continue;

                if (form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key]))
                {
                    query.Append($" AND {key} = @{key}");
                    cmd.Parameters.AddWithValue($"@{key}", form[key].ToString());
                }
            }

            // ✅ Sorting
            var validColumns = new HashSet<string>(filterFields.Keys.Select(k => k.ToLower())) { "id", "dateffet" };
            if (string.IsNullOrEmpty(sortColumn) || !validColumns.Contains(sortColumn.ToLower()))
                sortColumn = "dateffet";

            sortDirection = sortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";
            query.Append($" ORDER BY {sortColumn} {sortDirection}");

           

            cmd.CommandText = query.ToString();
            cmd.CommandTimeout = 600;

            // ✅ Prepare HTTP response
            Response.ContentType = "text/csv";
            Response.Headers.Add("Content-Disposition", $"attachment; filename=export_page{page}.csv");

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, bufferSize: 65536, leaveOpen: true);

            // ✅ Write header
            for (int i = 0; i < reader.FieldCount; i++)
            {
                await writer.WriteAsync(reader.GetName(i));
                if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
            }
            await writer.WriteLineAsync();

            // ✅ Stream rows
            while (await reader.ReadAsync())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader[i]?.ToString()?.Replace("\"", "\"\"") ?? "";
                    await writer.WriteAsync($"\"{value}\"");
                    if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
                }
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync();
            return new EmptyResult();
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