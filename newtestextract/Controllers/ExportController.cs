using System.Collections.Concurrent;
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
        public static ConcurrentDictionary<string, int> ProgressStore = new();
        public exportController(IConfiguration config)
        {
            _config = config;

        }

        public IActionResult Index()
        {
            var user = HttpContext.Session.GetString("username");
            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Login", "Account");

            // Prevent NullReference in View
            ViewBag.Columns = new List<string>();
            ViewBag.PageData = new List<List<string>>();
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 0;

            return View();
        }
        [HttpPost]
        public IActionResult ShowData(IFormCollection form, int page = 1)
        {

            const int pageSize = 1;
            var rows = new List<List<string>>();
            var columns = new List<string>();
            var filters = new List<SqlParameter>();
            var whereClause = new StringBuilder("WHERE 1=1");

            // Keep this in ViewBag for pagination and input persistence
            ViewBag.FormData = form;

            // Filtering logic
            void AddFilter(string key, string columnName)
            {
                if (form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key]))
                {
                    whereClause.Append($" AND {columnName} = @{key}");
                    filters.Add(new SqlParameter($"@{key}", form[key].ToString()));
                }
            }

            AddFilter("CodIsin", "CodIsin");
            AddFilter("CodSociete", "CodSociete");
            AddFilter("TypGestion", "TypGestion");
            // Add more as needed...

            // Date range handling (DateEffet, DatTraitement)
            if (DateTime.TryParse(form["DateEffetStart"], out var dateEffetStart))
            {
                whereClause.Append(" AND Dateffet >= @DateEffetStart");
                filters.Add(new SqlParameter("@DateEffetStart", dateEffetStart));
            }
            if (DateTime.TryParse(form["DateEffetEnd"], out var dateEffetEnd))
            {
                dateEffetEnd = dateEffetEnd.Date.AddDays(1).AddTicks(-1);
                whereClause.Append(" AND Dateffet <= @DateEffetEnd");
                filters.Add(new SqlParameter("@DateEffetEnd", dateEffetEnd));
            }

            int totalRows = 0;
            int totalPages = 0;
            int offset = (page - 1) * pageSize;

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                // Total rows
                var countCmd = new SqlCommand($"SELECT COUNT(*) FROM TestData {whereClause}", conn);
                countCmd.Parameters.AddRange(filters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());
                totalRows = (int)countCmd.ExecuteScalar();
                totalPages = (int)Math.Ceiling(totalRows / (double)pageSize);

                // Get page data
                var query = $@"
            SELECT * FROM TestData
            {whereClause}
            ORDER BY Dateffet DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddRange(filters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                        columns.Add(reader.GetName(i));

                    while (reader.Read())
                    {
                        var row = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                            row.Add(reader[i]?.ToString() ?? "");
                        rows.Add(row);
                    }
                }
            }

            // Pass data to view
            ViewBag.Columns = columns;
            ViewBag.PageData = rows;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRows = totalRows;

            return View("Index");
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
        public async Task<IActionResult> Export(
    IFormCollection form,
    [FromServices] ExportProgressTracker progressTracker,
    int page = 1,
    int pageSize = 1000,
    string sortColumn = null,
    string sortDirection = "DESC")
        {
            // Generate unique operation ID
            var operationId = Guid.NewGuid().ToString();
            const int totalSteps = 8; // Total logical steps in your export process
            progressTracker.Initialize(operationId, totalSteps);

            try
            {
                // Step 1: Preparation
                progressTracker.UpdateProgress(operationId, "Preparing data...");
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

                // Step 2: Build query
                progressTracker.UpdateProgress(operationId, "Building query...");
                var query = new StringBuilder("SELECT * FROM TestData WHERE 1=1");
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

                // Other filters
                progressTracker.UpdateProgress(operationId, "Applying filters...");
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

                // Step 3: Log the export
                progressTracker.UpdateProgress(operationId, "Logging export...");
                var excludedKeys = new HashSet<string> { "__RequestVerificationToken", "page", "filters" };
                var filtersUsed = string.Join(", ",
                    form.Keys
                        .Where(k => !excludedKeys.Contains(k))
                        .Where(k => !string.IsNullOrWhiteSpace(form[k]) && form[k] != "on")
                        .Select(k => $"{k}={form[k]}"));

                var username = HttpContext.Session.GetString("username") ?? "Unknown";
                using (var logConn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await logConn.OpenAsync();
                    string logQuery = @"
                INSERT INTO RD2_ExportLog (Username, ExportedAt, FiltersUsed)
                VALUES (@Username, @ExportedAt, @FiltersUsed)";
                    using (var logCmd = new SqlCommand(logQuery, logConn))
                    {
                        logCmd.Parameters.AddWithValue("@Username", username);
                        logCmd.Parameters.AddWithValue("@ExportedAt", DateTime.Now);
                        logCmd.Parameters.AddWithValue("@FiltersUsed", filtersUsed);
                        //await logCmd.ExecuteNonQueryAsync();
                    }
                }

                // Step 4: Execute query
                progressTracker.UpdateProgress(operationId, "Executing database query...");
                cmd.CommandText = query.ToString();
                cmd.CommandTimeout = 600;

                // Step 5: Prepare CSV response
                progressTracker.UpdateProgress(operationId, "Preparing CSV file...");
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

                // Step 6: Stream data
                progressTracker.UpdateProgress(operationId, "Streaming data...");
                long recordsProcessed = 0;
                while (await reader.ReadAsync())
                {
                    recordsProcessed++;

                    // Update progress every 100 records
                    if (recordsProcessed % 100 == 0)
                    {
                        progressTracker.UpdateProgress(operationId, $"Processing record {recordsProcessed}...");
                    }

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader[i]?.ToString()?.Replace("\"", "\"\"") ?? "";
                        await writer.WriteAsync($"\"{value}\"");
                        if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
                    }
                    await writer.WriteLineAsync();
                }
 Response.Cookies.Append("exportFinished", "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddMinutes(2),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax
                });
                // Step 7: Finalize
                progressTracker.UpdateProgress(operationId, "Finalizing export...");
                await writer.FlushAsync();

               

                // Step 8: Complete
                progressTracker.UpdateProgress(operationId, "Export complete!");
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                progressTracker.UpdateProgress(operationId, $"Error: {ex.Message}");
                throw;
            }
            finally
            {
                progressTracker.Complete(operationId);
            }
        
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