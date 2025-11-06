using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AgendaTatiNails.Models;
using AgendaTatiNails.Models.ViewModels; 
using AgendaTatiNails.Repositories;

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Cliente")] // Apenas Clientes podem agendar
    public class AgendamentoController : Controller
    {
        // Trocamos InMemoryDataService por IAgendaRepository
        private readonly IAgendaRepository _repository;

        public AgendamentoController(IAgendaRepository repository)
        {
            _repository = repository;
        }


        // =====================================================================
        // CRIAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento?serviceId=X (ou /Agendamento/Index)
        [HttpGet]
        public IActionResult Index(string serviceId)
        {
            // TODO: A View "Index.cshtml" precisa ser totalmente refeita
            // para funcionar com a nova lógica (múltiplos serviços e slots de 'Horario')
            
            // Passamos a lista de serviços para a View (para os checkboxes)
            var todosServicos = _repository.ObterTodosServicos();
            
            // Passa os serviços para a View
            return View("~/Views/Agendamento/Index.cshtml", todosServicos);
        }

        // GET: /Agendamento/GetHorariosDisponiveis?data=YYYY-MM-DD&duracaoTotal=XX
        [HttpGet]
        public IActionResult GetHorariosDisponiveis(string data, int duracaoTotal) // <-- MUDANÇA AQUI
        {
            if (!DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dataSelecionada))
                return BadRequest(new { message = "Formato de data inválido." });

            // Adicionamos uma validação para a duração
            if (duracaoTotal <= 0)
                return BadRequest(new { message = "Duração do serviço inválida." });

            if (dataSelecionada.Date < DateTime.Now.Date)
                return Ok(new List<Horario>()); 

            try
            {
                // Agora passamos a 'duracaoTotal' para o repositório
                var horarios = _repository.ObterHorariosDisponiveis(dataSelecionada, duracaoTotal);
                
                var horariosFormatados = horarios.Select(h => new 
                {
                    horarioId = h.HorarioId,
                    horario = h.HorarioPeriodo.ToString(@"hh\:mm")
                });

                return Ok(horariosFormatados); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar horários: {ex.Message}");
                return StatusCode(500, new { message = "Erro ao buscar horários."});
            }
        }

        // POST: /Agendamento/CriarAgendamento
        [HttpPost]
        public IActionResult CriarAgendamento([FromBody] CriarAtendimentoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dados inválidos." });
            }

            try
            {
                // Obtem o ID do Cliente logado
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                {
                    return Unauthorized(new { message = "Sua sessão expirou." });
                }

                //Chama o novo método do repositório, o repositório cuida de toda a lógica de transação
                Atendimento novoAtendimento = _repository.AdicionarAtendimento(clienteId, model.ServicoId, model.HorarioId);

                
                // (O JSON que o JS espera em caso de sucesso)
                return Ok(new { message = "Agendamento confirmado!" });
            }
            catch (Exception ex)
            {
                // Procura erros (ex: Conflito de horário)
                Console.WriteLine($"Erro ao CriarAgendamento: {ex.Message}");
                
                // Se o erro foi um conflito (pego pela lógica no repo)
                if (ex.Message.StartsWith("Conflito"))
                {
                    return Conflict(new { message = "Desculpe, este horário foi ocupado. Por favor, escolha outro." });
                }
                
                // Outro erro interno
                return StatusCode(500, new { message = "Erro interno no servidor ao tentar salvar." });
            }
            
        }

        // =====================================================================
        // DETALHES DO AGENDAMENTO (ATENDIMENTO)
        // =====================================================================

        // GET: /Agendamento/Detalhes/{id}
        [HttpGet]
        public IActionResult Detalhes(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) 
                return Unauthorized();
            
            var atendimento = _repository.ObterAtendimentoPorId(id);

            if (atendimento == null || atendimento.IdCliente != clienteId) 
                return NotFound();
            
            return View(atendimento);

        }

        // =====================================================================
        // EDITAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Editar/{id}
        [HttpGet]
        public IActionResult Editar(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) 
                return Unauthorized();
            
            // 1. Busca o Atendimento (e seus dados de Cliente/Serviços)
            var atendimento = _repository.ObterAtendimentoPorId(id);

            if (atendimento == null || atendimento.IdCliente != clienteId) 
                return NotFound();

            // Validação de status (que adicionamos)
            if (atendimento.AtendStatus != 1) // 1 = Agendado
            {
                string statusTexto = atendimento.AtendStatus == 2 ? "Concluído" : "Cancelado";
                TempData["MensagemErro"] = $"Agendamentos com status '{statusTexto}' não podem ser editados.";
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }
            
            // 2. Busca o Horário atual deste atendimento
            var horarioAtual = _repository.ObterHorarioPorAtendimentoId(atendimento.AtendId);
            if (horarioAtual == null)
            {
                TempData["MensagemErro"] = "Erro: Não foi possível encontrar o slot de horário original.";
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }
            
            // 3. Pega o Serviço atual (assumindo um serviço por atendimento)
            var servicoAtual = atendimento.Servicos.FirstOrDefault();
            if (servicoAtual == null)
            {
                TempData["MensagemErro"] = "Erro: Não foi possível encontrar o serviço original.";
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }

            // 4. Monta o ViewModel
            var viewModel = new EditarAgendamentoViewModel
            {
                AtendimentoId = atendimento.AtendId,
                HorarioId = horarioAtual.HorarioId, // Pré-seleciona o horário
                ServicoId = servicoAtual.ServicoId, // Pré-seleciona o serviço
                
                // Dados para exibir na View
                AtendimentoAtual = atendimento,
                HorarioAtual = horarioAtual,
                TodosOsServicos = new SelectList(_repository.ObterTodosServicos(), "ServicoId", "ServicoDesc", servicoAtual.ServicoId)
            };
            
            return View(viewModel);
        }

        // POST: /Agendamento/Editar/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, EditarAgendamentoViewModel model)
        {
            // Valida se o ID da URL é o mesmo do formulário
            if (id != model.AtendimentoId) return NotFound();

            // CORREÇÃO: Repopula todos os campos necessários se o ModelState for inválido
            if (!ModelState.IsValid)
            {
                var atendimento = _repository.ObterAtendimentoPorId(model.AtendimentoId);
                var horarioAtual = _repository.ObterHorarioPorAtendimentoId(model.AtendimentoId);
                var servicoAtual = atendimento?.Servicos.FirstOrDefault(); // Adicionado '?' por segurança

                model.AtendimentoAtual = atendimento;
                model.HorarioAtual = horarioAtual;
                model.TodosOsServicos = new SelectList(
                    _repository.ObterTodosServicos(), 
                    "ServicoId", "ServicoDesc", 
                    servicoAtual?.ServicoId // Adicionado '?' por segurança
                );
                
                return View(model); // Retorna à View com os erros de validação
            }

            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                    return Unauthorized();

                // Chama o repositório com todos os novos dados
                _repository.AtualizarAtendimento(
                    model.AtendimentoId, 
                    model.ServicoId, 
                    model.HorarioId, 
                    clienteId
                );

                TempData["MensagemSucesso"] = "Agendamento atualizado com sucesso!";
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao Editar (POST): {ex.Message}");
                // CORREÇÃO: Envia o erro para a View via TempData
                TempData["MensagemErro"] = $"Erro ao atualizar: {ex.Message}";
                return RedirectToAction(nameof(Editar), new { id = model.AtendimentoId });
            }
        }
        // =====================================================================
        // EXCLUIR/CANCELAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Excluir/{id}
        [HttpGet]
        public IActionResult Excluir(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) 
                return Unauthorized();

            var atendimento = _repository.ObterAtendimentoPorId(id);

            if (atendimento == null || atendimento.IdCliente != clienteId) 
                return NotFound();
            
            if (atendimento.AtendStatus != 1) // 1 = Agendado
            {
                string statusTexto = atendimento.AtendStatus == 2 ? "Concluído" : "Cancelado";
                TempData["MensagemErro"] = $"Agendamentos com status '{statusTexto}' não podem ser cancelados.";
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }
            return View(atendimento);
        }

        // POST: /Agendamento/ExcluirConfirmado/{id}
        [HttpPost, ActionName("ExcluirConfirmado")]
        [ValidateAntiForgeryToken]
        public IActionResult ExcluirPost(int AtendId) 
        {
            try
            {
                // Obter o ID do Cliente logado
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                {
                    return Unauthorized();
                }

                //Chama o repositório
                bool sucesso = _repository.CancelarAtendimento(AtendId, clienteId);

                if (sucesso)
                {
                    TempData["MensagemSucesso"] = "Agendamento cancelado com sucesso!";
                }
                else
                {
                    TempData["MensagemErro"] = "Não foi possível cancelar o agendamento.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ExcluirPost: {ex.Message}");
                // Se o repo deu 'throw' (ex: não pertence ao usuário)
                TempData["MensagemErro"] = $"Erro ao cancelar: {ex.Message}";
            }
            
            // Volta pra lista de agendamentos
            return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
        }
    }
}