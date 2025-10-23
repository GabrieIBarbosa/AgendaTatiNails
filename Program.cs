using Microsoft.AspNetCore.Authentication.Cookies;
using AgendaTatiNails.Services; // Importe seu novo serviço

var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona serviços ao contêiner.
builder.Services.AddControllersWithViews();

// 2. REMOVA qualquer linha que tenha 'AddDbContext' (ex: builder.Services.AddDbContext<...>)

// 3. Adicione o seu serviço em memória como Singleton
// Singleton = "Crie apenas uma instância e use-a sempre"
builder.Services.AddSingleton<InMemoryDataService>();

// 4. Adicione o serviço de autenticação por Cookies
// Isso permite que o login (HttpContext.SignInAsync) funcione
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; // Página de login
        options.LogoutPath = "/Login/Logout"; // Página de logout
        options.AccessDeniedPath = "/Home/AccessDenied";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 5. ATIVE a autenticação e autorização
// (Tem que estar entre UseRouting e MapControllerRoute)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();