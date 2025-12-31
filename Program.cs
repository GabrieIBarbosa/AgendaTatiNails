using Microsoft.AspNetCore.Authentication.Cookies;
using AgendaTatiNails.Repositories.Interfaces;
using AgendaTatiNails.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Adiciona a interface e a implementação do repositório SQL.
// AddScoped = "Crie uma nova instância para cada requisição web"
// Isso é ESSENCIAL para repositórios de banco de dados.
builder.Services.AddScoped<IUsuarioRepository, SqlUsuarioRepository>();
builder.Services.AddScoped<IServicoRepository, SqlServicoRepository>();
builder.Services.AddScoped<IHorarioRepository, SqlHorarioRepository>();
builder.Services.AddScoped<IAtendimentoRepository, SqlAtendimentoRepository>();



// Adicione o serviço de autenticação por Cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; 
        options.LogoutPath = "/Login/Logout"; 
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

// ATIVA a autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();