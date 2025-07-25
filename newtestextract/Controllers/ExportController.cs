﻿using System.Text;

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

        public IActionResult ShowData(IFormCollection form, int page = 1)

        {

            const int pageSize = 1000;

            var rows = new List<List<string>>();

            var columns = new List<string>();

            var filters = new List<SqlParameter>();

            var whereClause = new StringBuilder("WHERE 1=1");



            ViewBag.FormData = form;



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



                var countCmd = new SqlCommand($"SELECT COUNT(*) testdata {whereClause}", conn);

                countCmd.Parameters.AddRange(filters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());

                countCmd.CommandTimeout = 600;



                totalRows = (int)countCmd.ExecuteScalar();

                totalPages = (int)Math.Ceiling(totalRows / (double)pageSize);



                var query = $@"

            SELECT TOP 1000 * FROM  RD2_V_ExtractionAMF

            {whereClause}

            ORDER BY Dateffet DESC";



                var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddRange(filters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());



                cmd.CommandTimeout = 600;



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



            ViewBag.Columns = columns;

            ViewBag.PageData = rows;

            ViewBag.CurrentPage = page;

            ViewBag.TotalPages = totalPages;

            ViewBag.TotalRows = totalRows;



            return View("Index");

        }









        [HttpPost]



        public async Task<IActionResult> Export(IFormCollection form, int page = 1, int pageSize = 1000, string sortColumn = null, string sortDirection = "DESC")



        {

            _currentProgress = 0;

            _currentStatus = "Preparing data...";



            string connectionString = _config.GetConnectionString("DefaultConnection");

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









            var query = new StringBuilder("SELECT * FROM Testdata WHERE 1=1");



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
            // Clone and clean query for COUNT(*)
            var countQuery = new StringBuilder(query.ToString());
            int orderByIndex = countQuery.ToString().IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
            if (orderByIndex != -1)
            {
                countQuery.Remove(orderByIndex, countQuery.Length - orderByIndex); // remove ORDER BY
            }
            countQuery.Replace("SELECT *", "SELECT COUNT(*)");

            // Prepare count command
            using var countCmd = new SqlCommand(countQuery.ToString(), conn);
            foreach (SqlParameter param in cmd.Parameters)
            {
                countCmd.Parameters.AddWithValue(param.ParameterName, param.Value);
            }
            await conn.OpenAsync();
            var totalRows = (int)await countCmd.ExecuteScalarAsync();
            await conn.CloseAsync();



            int processed = 0;

            await conn.OpenAsync();



            await using var reader = await cmd.ExecuteReaderAsync();



            await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, bufferSize: 65536, leaveOpen: true);

            _currentProgress = 70;

            _currentStatus = "Finalizing export...";

            _currentProgress = 75;
 _currentStatus = "Finalizing export...";
            for (int i = 0; i < reader.FieldCount; i++)
            {
                await writer.WriteAsync(reader.GetName(i));
                if (i < reader.FieldCount - 1) await writer.WriteAsync(",");
            }
            await writer.WriteLineAsync();


            _currentProgress = 85;

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

                processed++;

                // Dynamically update progress (90% max for export loop)
                _currentProgress = 70 + (int)((double)processed / totalRows * 20);
                _currentStatus = $"Exporting {processed} / {totalRows}";
            }

            Response.Cookies.Append("exportFinished", "true", new CookieOptions



            {



                Expires = DateTimeOffset.UtcNow.AddMinutes(2),



                HttpOnly = false,



                SameSite = SameSiteMode.Lax



            }); 

            _currentProgress = 86;

            _currentStatus = "Finalizing export...";



           

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





    }

}