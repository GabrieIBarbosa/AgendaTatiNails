//Gerencia os serviços disponíveis no sistema. Exibe listas e detalhes de cada serviço para o agendamento.
using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories;
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
        private readonly IAgendaRepository _repository;

        public ServicoController(IAgendaRepository repository)
        {
            _repository = repository;
        }

        public IActionResult Index()
        {
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

            // Trocamos _dataService.ObterAgendamentosPorCliente por...
            // ... _repository.ObterAtendimentosPorCliente
            var atendimentosDoCliente = _repository.ObterAtendimentosPorCliente(clienteId);

            // 3. Enviar a lista para a View

            // TODO: A View "ListaServico.cshtml" precisa ser atualizada
            // para receber um @model List<Atendimento>
            return View(atendimentosDoCliente ?? new List<Models.Atendimento>());
            // --- Fim da MUDANÇA 2 ---
        }
        
        // Funções CRUD 

        // GET: Servico/Detalhes/5
        public IActionResult Detalhes(int id)
        {
            // Redireciona para a ação correta no AgendamentoController
            return RedirectToAction("Detalhes", "Agendamento", new { id = id });
        }
        
        // GET: Servico/Editar/5
        public IActionResult Editar(int id)
        {
            // Redireciona para a ação correta no AgendamentoController
            return RedirectToAction("Editar", "Agendamento", new { id = id });
        }

        // GET: Servico/Excluir/5
        public IActionResult Excluir(int id)
        {
            // Redireciona para a ação correta no AgendamentoController
            return RedirectToAction("Excluir", "Agendamento", new { id = id });
        }
    }
}