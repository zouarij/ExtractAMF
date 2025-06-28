var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
//********1
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
//********1
var app = builder.Build();
//********2
app.UseSession();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//********
var configuration = builder.Configuration;

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
//********
app.UseCookiePolicy();
app.UseAuthorization();
builder.Services.AddLogging(logging => logging.AddConsole());
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=account}/{action=login}/{id?}");

app.Run();
