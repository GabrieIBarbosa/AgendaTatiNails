using Microsoft.AspNetCore.Mvc;
using AgendaTatiNails.ViewModels; // Garanta que este namespace está correto
using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using Microsoft.AspNetCore.Authorization; // Necessário para [AllowAnonymous]

namespace AgendaTatiNails.Controllers
{
    [AllowAnonymous] // Permite que usuários não logados acessem as páginas de login/cadastro
    public class LoginController : Controller
    {
        private readonly InMemoryDataService _dataService;

        public LoginController(InMemoryDataService dataService)
        {
            _dataService = dataService;
        }

        // =================================================================
        // AÇÕES DE LOGIN
        // =================================================================

        // GET: /Login/Index
        // Mostra a View de Login e captura a URL de retorno (para onde o usuário tentava ir)
        [HttpGet]
        public IActionResult Index(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Login/Index
        // Processa o formulário de login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl; // Garante que a returnUrl persista se o login falhar

            if (ModelState.IsValid)
            {
                // 1. Tenta logar como Cliente
                var cliente = _dataService.Clientes.FirstOrDefault(c =>
                    c.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase) &&
                    c.Senha == model.Senha); // ATENÇÃO: Senha em texto plano (OK para demo)

                if (cliente != null)
                {
                    await AutenticarUsuario(cliente.Id.ToString(), cliente.Nome, cliente.Email, "Cliente");
                    
                    // *** MUDANÇA PRINCIPAL (Etapa 1) ***
                    // Passa a Role "Cliente" para o método de redirecionamento
                    return RedirectToLocal(returnUrl, "Cliente");
                }

                // 2. Se não for cliente, tenta logar como Profissional (Admin)
                var profissional = _dataService.Profissionais.FirstOrDefault(p =>
                    p.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase) &&
                    p.Senha == model.Senha);

                if (profissional != null)
                {
                    await AutenticarUsuario(profissional.Id.ToString(), profissional.Nome, profissional.Email, "Profissional");
                    
                    // *** MUDANÇA PRINCIPAL (Etapa 1) ***
                    // Passa a Role "Profissional" para o método de redirecionamento
                    return RedirectToLocal(returnUrl, "Profissional");
                }

                // 3. Se não encontrou ninguém
                ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
            }

            // Se o modelo não for válido ou login falhar, retorna à View de Login
            return View(model);
        }

        // =================================================================
        // AÇÕES DE CADASTRO
        // =================================================================

        // GET: /Login/Cadastro
        [HttpGet]
        public IActionResult Cadastro()
        {
            return View();
        }

        // POST: /Login/Cadastro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastro(CadastroViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool emailJaExiste = _dataService.Clientes.Any(c => c.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase)) ||
                                     _dataService.Profissionais.Any(p => p.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));

                if (emailJaExiste)
                {
                    ModelState.AddModelError("Email", "Este email já está cadastrado.");
                    return View(model);
                }

                var novoCliente = new Cliente
                {
                    Nome = model.Nome,
                    Email = model.Email,
                    Senha = model.Senha,
                    Telefone = model.Telefone
                };

                try
                {
                    novoCliente = _dataService.AdicionarCliente(novoCliente);
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"Erro ao adicionar cliente: {ex.Message}");
                     ModelState.AddModelError(string.Empty, "Ocorreu um erro ao criar a conta.");
                     return View(model);
                }

                await AutenticarUsuario(novoCliente.Id.ToString(), novoCliente.Nome, novoCliente.Email, "Cliente");
                return RedirectToAction("Index", "Home"); // Novos cadastros são sempre Clientes
            }
            return View(model);
        }

        // =================================================================
        // AÇÕES DE ESQUECI A SENHA
        // =================================================================

        // GET: /Login/EsqueciSenha
        [HttpGet]
        public IActionResult EsqueciSenha()
        {
            return View(new EsqueciSenhaViewModel());
        }

        // POST: /Login/EsqueciSenha
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EsqueciSenha(EsqueciSenhaViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool emailExiste = _dataService.Clientes.Any(c => c.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase)) ||
                                   _dataService.Profissionais.Any(p => p.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));

                if (emailExiste)
                {
                    var viewModel = new ConfirmacaoEnvioViewModel { Email = model.Email };
                    Console.WriteLine($"[Simulação] Enviando link de recuperação para: {model.Email}");
                    return View("ConfirmacaoEnvio", viewModel);
                }
                else
                {
                    ModelState.AddModelError("Email", "Este email não está cadastrado.");
                }
            }
            return View(model);
        }

        // =================================================================
        // AÇÃO DE LOGOUT
        // =================================================================

        // POST: /Login/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // =================================================================
        // MÉTODOS AUXILIARES
        // =================================================================

        private async Task AutenticarUsuario(string id, string nome, string email, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, nome),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role) // Define a Role
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        // *** MÉTODO ATUALIZADO (Etapa 1) ***
        // Redireciona com base na Role se não houver URL de retorno
        private IActionResult RedirectToLocal(string returnUrl, string role)
        {
            // Se o usuário estava tentando acessar uma página (ex: /Agendamento)
            // e o login foi válido, manda ele de volta para lá.
            if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1)
            {
                return Redirect(returnUrl);
            }
            
            // Se NÃO havia URL de retorno (veio direto para /Login):
            if (role == "Profissional")
            {
                // *** PRONTO PARA ETAPA 2 ***
                // Redireciona o Admin para o Dashboard dele
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                // Redireciona o Cliente para a Home
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}