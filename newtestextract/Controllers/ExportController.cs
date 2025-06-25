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
            ViewBag.StatusOptions = new[] { "Active", "Pending", "Closed" };
            return View();
        }

        [HttpPost]
        public IActionResult Export(string status, DateTime startDate, DateTime endDate)
        {
            string connectionString = _config.GetConnectionString("DefaultConnection");

            var csv = new StringBuilder();

            using (var conn = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT * FROM dbo.TestData
            WHERE Status = @status
              AND StartDate <= @endDate AND EndDate >= @startDate";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@startDate", startDate);
                    cmd.Parameters.AddWithValue("@endDate", endDate);

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        // Write header
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            csv.Append(reader.GetName(i));
                            if (i < reader.FieldCount - 1) csv.Append(",");
                        }
                        csv.AppendLine();

                        // Write data
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                object rawValue = reader[i];
                                string value;

                                if (rawValue is DateTime dt)
                                    value = dt.ToString("yyyy-MM-dd");
                                else
                                    value = rawValue.ToString();

                                value = value.Replace("\"", "\"\"");
                                csv.Append($"\"{value}\"");

                                if (i < reader.FieldCount - 1) csv.Append(",");
                            }
                            csv.AppendLine();
                        }
                    }
                }
            }

            byte[] fileBytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(fileBytes, "text/csv", "export.csv");
        }
    }
}
