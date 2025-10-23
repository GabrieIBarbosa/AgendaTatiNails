// Controllers/LoginController.cs (CORRIGIDO - Sem método duplicado)
using Microsoft.AspNetCore.Mvc;
using AgendaTatiNails.ViewModels; // Assume que seus ViewModels estão aqui
using AgendaTatiNails.Models;
using AgendaTatiNails.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;

namespace AgendaTatiNails.Controllers
{
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
        [HttpGet]
        public IActionResult Index(string returnUrl = null) // Captura a URL de retorno
        {
            ViewData["ReturnUrl"] = returnUrl; // Passa para a View (se necessário)
            return View();
        }

        // POST: /Login/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl; // Mantém a URL de retorno
            if (ModelState.IsValid)
            {
                var cliente = _dataService.Clientes.FirstOrDefault(c =>
                    c.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase) &&
                    c.Senha == model.Senha); // ATENÇÃO: Comparação de senha insegura!

                if (cliente != null)
                {
                    await AutenticarUsuario(cliente.Id.ToString(), cliente.Nome, cliente.Email, "Cliente");
                    return RedirectToLocal(returnUrl); // Redireciona para a URL original ou Home
                }

                var profissional = _dataService.Profissionais.FirstOrDefault(p =>
                    p.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase) &&
                    p.Senha == model.Senha); // ATENÇÃO: Comparação de senha insegura!

                if (profissional != null)
                {
                    await AutenticarUsuario(profissional.Id.ToString(), profissional.Nome, profissional.Email, "Profissional");
                    return RedirectToLocal(returnUrl); // Redireciona para a URL original ou Home
                }

                ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
            }
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

        // POST: /Login/Cadastro (VERSÃO CORRETA - ÚNICA)
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
                    Senha = model.Senha, // ATENÇÃO: Criptografar em produção!
                    Telefone = model.Telefone
                };

                try
                {
                    // Chama o método correto do serviço
                    novoCliente = _dataService.AdicionarCliente(novoCliente);
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"Erro ao adicionar cliente: {ex.Message}");
                     ModelState.AddModelError(string.Empty, "Erro ao criar conta. Tente novamente.");
                     return View(model);
                }

                // Autentica após cadastro
                await AutenticarUsuario(novoCliente.Id.ToString(), novoCliente.Nome, novoCliente.Email, "Cliente");

                // Redireciona para a Home
                return RedirectToAction("Index", "Home");
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
                    // SIMULAÇÃO: Apenas redireciona para confirmação
                    var viewModel = new ConfirmacaoEnvioViewModel { Email = model.Email };
                    // ATENÇÃO: Em produção, aqui você geraria um token e enviaria o email real
                    Console.WriteLine($"[Simulação] Enviando link de recuperação para: {model.Email}");
                    return View("ConfirmacaoEnvio", viewModel);
                }
                else
                {
                    ModelState.AddModelError("Email", "Email não cadastrado.");
                }
            }
            return View(model);
        }

        // =================================================================
        // AÇÃO DE LOGOUT
        // =================================================================

        // GET or POST: /Login/Logout
        [HttpPost] // É boa prática usar POST para logout para evitar CSRF
        [ValidateAntiForgeryToken] // Protege contra CSRF
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
                new Claim(ClaimTypes.Role, role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                // IsPersistent = model.LembrarMe, // Se tiver checkbox "Lembrar-me"
                 IsPersistent = true, // Mantém logado entre sessões por padrão
                 ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) // Cookie expira em 7 dias
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        // Helper para redirecionar de volta após login
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}