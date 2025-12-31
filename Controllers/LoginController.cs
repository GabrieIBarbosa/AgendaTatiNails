using Microsoft.AspNetCore.Mvc;
using AgendaTatiNails.ViewModels;
using AgendaTatiNails.Models; 
using AgendaTatiNails.Repositories.Interfaces; // Importa a nova pasta de Interfaces
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using Microsoft.AspNetCore.Authorization;

namespace AgendaTatiNails.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        // Pede apenas o repositório de usuário
        private readonly IUsuarioRepository _usuarioRepository;

        public LoginController(IUsuarioRepository repository)
        {
            _usuarioRepository = repository;
        }

        // =================================================================
        // AÇÕES DE LOGIN
        // =================================================================

        [HttpGet]
        public IActionResult Index(string? returnUrl = null) 
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null) 
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var usuario = _usuarioRepository.ObterUsuarioPorEmail(model.Email);

                // TODO: Implementar HASHING DE SENHA (Prioridade 3)
                if (usuario != null && usuario.UsuarioSenha == model.Senha)
                {
                    var cliente = _usuarioRepository.ObterClientePorId(usuario.UsuarioId);

                    if (cliente != null)
                    {
                        // --- É um Cliente ---
                        await AutenticarUsuario(cliente.ClienteId.ToString(), cliente.Usuario.UsuarioNome, cliente.Usuario.UsuarioEmail, "Cliente");
                        
                        return RedirectToLocal(returnUrl ?? "/", "Cliente");
                    }
                    else
                    {
                        // --- É um Profissional/Admin ---
                        await AutenticarUsuario(usuario.UsuarioId.ToString(), usuario.UsuarioNome, usuario.UsuarioEmail, "Profissional");
                        
                        return RedirectToLocal(returnUrl ?? "/", "Profissional");
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
            }
            return View(model);
        }

        // =================================================================
        // AÇÕES DE CADASTRO
        // =================================================================

        [HttpGet]
        public IActionResult Cadastro()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastro(CadastroViewModel model)
        {
            if (ModelState.IsValid)
            {
                var usuarioExistente = _usuarioRepository.ObterUsuarioPorEmail(model.Email);

                if (usuarioExistente != null)
                {
                    ModelState.AddModelError("Email", "Este email já está cadastrado.");
                    return View(model);
                }

                var novoCliente = new Cliente
                {
                    Usuario = new Usuario
                    {
                        UsuarioNome = model.Nome,
                        UsuarioEmail = model.Email,
                        UsuarioSenha = model.Senha // TODO: Fazer HASH
                    },
                    ClienteTelefone = model.Telefone
                };

                
                Cliente clienteSalvo;
                try
                {
                    clienteSalvo = _usuarioRepository.AdicionarNovoCliente(novoCliente);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao salvar cliente no repositório: {ex.Message}");
                    ModelState.AddModelError(string.Empty, "Ocorreu um erro ao salvar seu cadastro. Tente novamente.");
                    return View(model);
                }

                // Loga o usuário recém-criado
                await AutenticarUsuario(clienteSalvo.ClienteId.ToString(), clienteSalvo.Usuario.UsuarioNome, clienteSalvo.Usuario.UsuarioEmail, "Cliente");
                // Volta para a Home (já logado)
                return RedirectToAction("Index", "Home"); 

                
            }
            return View(model);
        }

        // =================================================================
        // AÇÕES DE ESQUECI A SENHA
        // =================================================================

        [HttpGet]
        public IActionResult EsqueciSenha()
        {
            return View(new EsqueciSenhaViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EsqueciSenha(EsqueciSenhaViewModel model)
        {
            if (ModelState.IsValid)
            {
                var emailExiste = _usuarioRepository.ObterUsuarioPorEmail(model.Email);

                if (emailExiste != null)
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
                new Claim(ClaimTypes.Role, role) 
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
        
        private IActionResult RedirectToLocal(string returnUrl, string role)
        {
            if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1)
            {
                return Redirect(returnUrl);
            }

            if (role == "Profissional")
            {
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}