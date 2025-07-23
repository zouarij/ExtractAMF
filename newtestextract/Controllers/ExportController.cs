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

        public async Task<IActionResult> Export(IFormCollection form, int page = 1, int pageSize = 1000, string sortColumn = null, string sortDirection = "DESC")

        {
            _currentProgress = 0;
            _currentStatus = "Preparing data...";

            string connectionString = _config.GetConnectionString("DefaultConnection");
            string progressKey = HttpContext.Session.GetString("username") ?? Guid.NewGuid().ToString();
            ProgressStore[progressKey] = 0;
             _currentProgress = 0;
    _currentStatus = "Preparing data...";

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




            var query = new StringBuilder("SELECT * FROM TestData WHERE 1=1");

            using var conn = new SqlConnection(connectionString);

            using var cmd = new SqlCommand { Connection = conn };
            _currentProgress = 10;
            _currentStatus = "Querying database...";


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

            _currentProgress = 20;
            _currentStatus = "Querying database...";

            foreach (var key in filterFields.Keys)

            {

                if (key.Contains("Start") || key.Contains("End")) continue;



                if (form.ContainsKey(key) && !string.IsNullOrWhiteSpace(form[key]))

                {

                    query.Append($" AND {key} = @{key}");

                    cmd.Parameters.AddWithValue($"@{key}", form[key].ToString());

                }

            }



            var validColumns = new HashSet<string>(filterFields.Keys.Select(k => k.ToLower())) { "id", "dateffet" };

            if (string.IsNullOrEmpty(sortColumn) || !validColumns.Contains(sortColumn.ToLower()))

                sortColumn = "dateffet";



            sortDirection = sortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";

            query.Append($" ORDER BY {sortColumn} {sortDirection}");



            var excludedKeys = new HashSet<string>

{

    "__RequestVerificationToken", "page", "filters"

};



            var filtersUsed = string.Join(", ",

                form.Keys

                .Where(k => !excludedKeys.Contains(k))

                .Where(k => !string.IsNullOrWhiteSpace(form[k]) && form[k] != "on")

                .Select(k => $"{k}={form[k]}"));



            var username = HttpContext.Session.GetString("username") ?? "Unknown";
            _currentProgress = 25;
            _currentStatus = "Querying database...";


            using (var logConn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))

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

                    // remove this one if you have the write permission****************

                    // cdmd.ExecuteNonQuery();

                }

            }

            _currentProgress = 30;
            _currentStatus = "Processing records...";
            _currentProgress = 34;
            _currentStatus = "Processing records...";
           

            cmd.CommandText = query.ToString();

            cmd.CommandTimeout = 600;

 _currentProgress = 37;
            _currentStatus = "Processing records...";
            _currentProgress = 40;
            _currentStatus = "Processing records...";

            Response.ContentType = "text/csv";

            Response.Headers.Add("Content-Disposition", $"attachment; filename=export_page{page}.csv");


            _currentProgress = 50;
            _currentStatus = "Generating CSV file...";
            _currentProgress = 60;
            _currentStatus = "Generating CSV file...";
            _currentProgress = 67;
            _currentStatus = "Generating CSV file...";



            await conn.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, bufferSize: 65536, leaveOpen: true);



            for (int i = 0; i < reader.FieldCount; i++)

            {

                await writer.WriteAsync(reader.GetName(i));

                if (i < reader.FieldCount - 1) await writer.WriteAsync(",");

            }

            await writer.WriteLineAsync();

            _currentProgress = 70;
            _currentStatus = "Finalizing export...";
            _currentProgress = 80;
            _currentStatus = "Finalizing export...";

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

            _currentProgress = 80;
            _currentStatus = "Finalizing export...";

            Response.Cookies.Append("exportFinished", "true", new CookieOptions

            {

                Expires = DateTimeOffset.UtcNow.AddMinutes(2),

                HttpOnly = false,

                SameSite = SameSiteMode.Lax

            });
            _currentProgress = 90;
            _currentStatus = "Finalizing export...";

            await writer.FlushAsync();
          
            _currentProgress = 100;
            _currentStatus = "Export complete!";
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


    } }