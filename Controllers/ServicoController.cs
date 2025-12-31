//Gerencia os serviços disponíveis no sistema. Exibe listas e detalhes de cada serviço para o agendamento.
using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories.Interfaces; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic; 

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Cliente")] // Só permite acesso a usuários autenticados com o papel "Cliente"
    public class ServicoController : Controller
    {
       
        private readonly IAtendimentoRepository _atendimentoRepository;

        public ServicoController(IAtendimentoRepository repository) 
        {
            _atendimentoRepository = repository;
        }

        public IActionResult Index()
        {
            // Esta ação não usa o repositório, está OK.
            return View("~/Views/Servico/Index.cshtml");
        }


        // GET: /Servico/ListaServico
        // (Esta é a página "Meus Agendamentos" do cliente)
        public IActionResult ListaServico()
        {
            // 1. Obter o ID do usuário logado
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int clienteId))
            {
                return Unauthorized();
            }

            var atendimentosDoCliente = _atendimentoRepository.ObterAtendimentosPorCliente(clienteId);

            return View(atendimentosDoCliente ?? new List<Models.Atendimento>());
        }
        
        // Funções CRUD (Redirecionamentos)
        // Estes métodos estão corretos e não precisam de alteração,
        // pois eles apenas redirecionam para o AgendamentoController.

        // GET: Servico/Detalhes/5
        public IActionResult Detalhes(int id)
        {
            return RedirectToAction("Detalhes", "Agendamento", new { id = id });
        }
        
        // GET: Servico/Editar/5
        public IActionResult Editar(int id)
        {
            return RedirectToAction("Editar", "Agendamento", new { id = id });
        }

        // GET: Servico/Excluir/5
        public IActionResult Excluir(int id)
        {
            return RedirectToAction("Excluir", "Agendamento", new { id = id });
        }
    }
}