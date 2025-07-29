using System.Text;

using Microsoft.AspNetCore.Http;

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

            _logger = logger;

        }



        public IActionResult Index()

        {

            var user = HttpContext.Session.GetString("username");

            if (string.IsNullOrEmpty(user))

                return RedirectToAction("Login", "Account");



            ViewBag.Columns = new List<string>();

            ViewBag.PageData = new List<List<string>>();

            ViewBag.CurrentPage = 1;

            ViewBag.TotalPages = 0;



            return View();

        }
        [HttpPost]
public async Task<IActionResult> ShowData(IFormCollection form)
{
            _currentProgress = 0;
            _currentStatus = "Starting data load...";


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
                    ["AdrVille"] = "text",
                    ["CodGerant"] = "text",
                    ["CategorisationMIFID"] = "text",
                    ["LieuNegociation"] = "text"
                };
            _currentProgress = 10;
            _currentStatus = "Applying filters...";
            var query = new StringBuilder("SELECT TOP 1000 * FROM Testdata WHERE 1=1");
    using var conn = new SqlConnection(connectionString);
    using var cmd = new SqlCommand { Connection = conn };
    // Date filters
    if (form.ContainsKey("DateEffetStart") && DateTime.TryParse(form["DateEffetStart"], out var dateEffetStart))
    {
        query.Append(" AND dateffet >= @DateEffetStart");
        cmd.Parameters.AddWithValue("@DateEffetStart", dateEffetStart);
    }

    if (form.ContainsKey("DateEffetEnd") && DateTime.TryParse(form["DateEffetEnd"], out var dateEffetEnd))
    {
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
        query.Append(" AND DatTraitement <= @DatTraitementEnd");
        cmd.Parameters.AddWithValue("@DatTraitementEnd", datTraitementEnd);
    }

            _currentProgress = 40;
            _currentStatus = "Fetching data from database...";

            // Other filters
            foreach (var key in filterFields.Keys)
    {
        if (key.Contains("Start") || key.Contains("End")) continue;

        if (form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key]))
        {
            query.Append($" AND {key} = @{key}");
            cmd.Parameters.AddWithValue($"@{key}", form[key].ToString());
        }
    }
    var validColumns = new HashSet<string>(filterFields.Keys.Select(k => k.ToLower())) { "id", "dateeffet" };
    var sortColumn = form["sortColumn"].ToString();
    var sortDirection = form["sortDirection"].ToString()?.ToUpper() == "ASC" ? "ASC" : "DESC";
    if (string.IsNullOrEmpty(sortColumn) || !validColumns.Contains(sortColumn.ToLower()))
        sortColumn = "dateffet";
    query.Append($" ORDER BY {sortColumn} {sortDirection}");

    cmd.CommandText = query.ToString();
    await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var columns = new List<string>();
            var pageData = new List<List<string>>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            _currentProgress = 60;
            _currentStatus = "Reading rows...";

            int processed = 0;
            const int maxRows = 1000;

            while (await reader.ReadAsync())
            {
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader[i]?.ToString() ?? string.Empty);
                }
                pageData.Add(row);

                processed++;
                _currentProgress = 60 + (int)((double)processed / maxRows * 30);
                _currentStatus = $"Processing {processed} / {maxRows}";
            }

            _currentProgress = 100;
            _currentStatus = "Done loading data";

            ViewBag.Columns = columns;
            ViewBag.PageData = pageData;
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;

            return View("Index");
        }





        [HttpPost]
            public async Task<IActionResult> Export(IFormCollection form, int page = 1, int pageSize = 1000, string sortColumn = null, string sortDirection = "DESC")
            {
                _currentProgress = 0;
                _currentStatus = "Preparing data...";

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
                    ["AdrVille"] = "text",
                    ["CodGerant"] = "text",
                    ["CategorisationMIFID"] = "text",
                    ["LieuNegociation"] = "text"
                };

                var query = new StringBuilder("SELECT * FROM Testdata WHERE 1=1");

                using var conn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand { Connection = conn };

                _currentProgress = 10;
                _currentStatus = "Querying database...";

                // Date filters
                if (form.ContainsKey("DateEffetStart") && DateTime.TryParse(form["DateEffetStart"], out var dateEffetStart))
                {
                    query.Append(" AND dateffet >= @DateEffetStart");
                    cmd.Parameters.AddWithValue("@DateEffetStart", dateEffetStart);
                }

                if (form.ContainsKey("DateEffetEnd") && DateTime.TryParse(form["DateEffetEnd"], out var dateEffetEnd))
                {
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
                    query.Append(" AND DatTraitement <= @DatTraitementEnd");
                    cmd.Parameters.AddWithValue("@DatTraitementEnd", datTraitementEnd);
                }

                // Other filters
                foreach (var key in filterFields.Keys)
                {
                    if (key.Contains("Start") || key.Contains("End")) continue;

                    if (form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key]))
                    {
                        query.Append($" AND {key} = @{key}");
                        cmd.Parameters.AddWithValue($"@{key}", form[key].ToString());
                    }
                }

                // Sorting
                var validColumns = new HashSet<string>(filterFields.Keys.Select(k => k.ToLower())) { "id", "dateffet" };
                if (string.IsNullOrEmpty(sortColumn) || !validColumns.Contains(sortColumn.ToLower()))
                    sortColumn = "dateffet";
                sortDirection = sortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";
                query.Append($" ORDER BY {sortColumn} {sortDirection}");

                // Logging filters used
                var excludedKeys = new HashSet<string> { "__RequestVerificationToken", "page", "filters" };
                var filtersUsed = string.Join(", ",
                    form.Keys
                        .Where(k => !excludedKeys.Contains(k))
                        .Where(k => !string.IsNullOrWhiteSpace(form[k]) && form[k] != "on")
                        .Select(k => $"{k}={form[k]}")
                );

                var username = HttpContext.Session.GetString("username") ?? "Unknown";

                using (var logConn = new SqlConnection(connectionString))
                {
                    logConn.Open();
                    string logQuery = @"
                INSERT INTO RD2_ExportLog (Username, ExportedAt, FiltersUsed)
                VALUES (@Username, @ExportedAt, @FiltersUsed)";
                    using (var cdmd = new SqlCommand(logQuery, logConn))
                    {
                        cdmd.Parameters.AddWithValue("@Username", username);
                        cdmd.Parameters.AddWithValue("@ExportedAt", DateTime.Now);
                        cdmd.Parameters.AddWithValue("@FiltersUsed", filtersUsed);
                        // Optional: uncomment to log
                        // cdmd.ExecuteNonQuery();
                    }
                }

                // Prepare for export
                _currentProgress = 40;
                _currentStatus = "Processing records...";

                // Prepare COUNT(*) query
                var countQuery = new StringBuilder(query.ToString());
                int orderByIndex = countQuery.ToString().IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
                if (orderByIndex != -1)
                    countQuery.Remove(orderByIndex, countQuery.Length - orderByIndex);
                countQuery.Replace("SELECT *", "SELECT COUNT(*)");

                using var countCmd = new SqlCommand(countQuery.ToString(), conn);
                foreach (SqlParameter param in cmd.Parameters)
                {
                    countCmd.Parameters.AddWithValue(param.ParameterName, param.Value);
                }

                await conn.OpenAsync();
                var totalRows = (int)await countCmd.ExecuteScalarAsync();
                await conn.CloseAsync();

                _currentProgress = 50;
                _currentStatus = "Generating CSV file...";

                cmd.CommandText = query.ToString();
                cmd.CommandTimeout = 600;

                Response.ContentType = "text/csv";
                Response.Headers.Add("Content-Disposition", $"attachment; filename=export_page{page}.csv");

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, bufferSize: 65536, leaveOpen: true);

                // Write headers
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    await writer.WriteAsync(reader.GetName(i));
                    if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
                }
                await writer.WriteLineAsync();

                // Write rows
                int processed = 0;
                while (await reader.ReadAsync())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader[i]?.ToString()?.Replace("\"", "\"\"") ?? "";
                        await writer.WriteAsync($"\"{value}\"");
                        if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
                    }
                    await writer.WriteLineAsync();

                    processed++;
                    _currentProgress = 70 + (int)((double)processed / totalRows * 20);
                    _currentStatus = $"Exporting {processed} / {totalRows}";
                }

                Response.Cookies.Append("exportFinished", "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddMinutes(2),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax
                });

                _currentProgress = 100;
                _currentStatus = "Export complete!";
                await writer.FlushAsync();

                return new EmptyResult();
            }





            private static int _currentProgress = 0;

            private static string _currentStatus = "Starting...";





            [HttpGet]

            public IActionResult GetProgress()

            {

                return Json(new

                {

                    progress = _currentProgress,

                    message = _currentStatus

                });

            }





        }

}

