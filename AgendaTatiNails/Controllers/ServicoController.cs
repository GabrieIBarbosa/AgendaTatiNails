//Gerencia os serviços disponíveis no sistema. Exibe listas e detalhes de cada serviço para o agendamento.
using AgendaTatiNails.Models;
using AgendaTatiNails.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Cliente")] // Só permite acesso a usuários autenticados com o papel "Cliente"
    public class ServicoController : Controller
    {
        private readonly InMemoryDataService _dataService;

        public IActionResult Index()
        {
            return View("~/Views/Servico/Index.cshtml");
        }
        public ServicoController(InMemoryDataService dataService)
        {
            _dataService = dataService;
        }

        // GET: /Servico/ListaServico
        public IActionResult ListaServico()
        {
            // 1. Obter o ID do usuário logado
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int clienteId))
            {
                // Se não conseguir converter o ID, retorna não autorizado
                // (Embora o [Authorize] deva prevenir isso)
                return Unauthorized();
            }

            // 2. Usar o método do serviço para buscar os agendamentos do cliente
            //    Este método já faz o filtro E inclui os dados do serviço.
            var agendamentosDoCliente = _dataService.ObterAgendamentosPorCliente(clienteId);

            // 3. Enviar a lista (que pode estar vazia) para a View
            //    Se o método retornar null (não deveria), envia uma lista vazia.
            return View(agendamentosDoCliente ?? new List<Models.Agendamento>());
        }
        // Funções CRUD (a serem implementadas)

        // GET: Servico/Detalhes/5
        public IActionResult Detalhes(int id)
        {
            // Lógica para buscar e exibir detalhes do agendamento 'id'
            return Content($"Funcionalidade DETALHES para o agendamento {id} a ser implementada.");
        }
        
        // GET: Servico/Editar/5
        public IActionResult Editar(int id)
        {
            // Lógica para buscar o agendamento 'id' e mostrar um formulário de edição
            return Content($"Funcionalidade EDITAR para o agendamento {id} a ser implementada.");
        }

        // GET: Servico/Excluir/5
        public IActionResult Excluir(int id)
        {
            // Lógica para buscar o agendamento 'id' e mostrar uma tela de confirmação de exclusão
            return Content($"Funcionalidade EXCLUIR para o agendamento {id} a ser implementada.");
        }
    }
}