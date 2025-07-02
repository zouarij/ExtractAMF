using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

public class ExportLogController : Controller
{
    private readonly string _connectionString;

    public ExportLogController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }
    public IActionResult Index()
    {
        var role = HttpContext.Session.GetString("role");
        if (role != "admin")
        {
            return RedirectToAction("AccessDenied", "Account");
        }
        DataTable exportLogs = new DataTable();

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            string query = @"SELECT TOP 1000 [Id], [Username], [ExportedAt], [FiltersUsed]
                             FROM [TestExportDB].[dbo].[ExportLog]
                             ORDER BY ExportedAt DESC";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(exportLogs);
            }
        }

        return View(exportLogs);
    }
}
