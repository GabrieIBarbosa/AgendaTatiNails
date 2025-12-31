using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Security.Claims;
using AgendaTatiNails.Models;
using AgendaTatiNails.Models.ViewModels; 
using AgendaTatiNails.Repositories.Interfaces;
using System.Text.Json;

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Cliente, Profissional")] 
    public class AgendamentoController : Controller
    {
        private readonly IAtendimentoRepository _atendimentoRepo;
        private readonly IHorarioRepository _horarioRepo;
        private readonly IServicoRepository _servicoRepo;

        public AgendamentoController(
            IAtendimentoRepository atendimentoRepo, 
            IHorarioRepository horarioRepo, 
            IServicoRepository servicoRepo)
        {
            _atendimentoRepo = atendimentoRepo;
            _horarioRepo = horarioRepo;
            _servicoRepo = servicoRepo;
        }

        // =====================================================================
        // CRIAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Index
        // GET: /Agendamento/Index
        [HttpGet]
        public IActionResult Index(int? serviceId) 
        {
            var todosServicos = _servicoRepo.ObterTodosServicos();
            
            // Passa o ID recebido para a View (se houver)
            ViewBag.PreSelectedServiceId = serviceId; 

            return View("~/Views/Agendamento/Index.cshtml", todosServicos);
        }

        // GET: /Agendamento/GetHorariosDisponiveis
        [HttpGet]
        public IActionResult GetHorariosDisponiveis(string data, int duracaoTotal)
        {
            if (!DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dataSelecionada))
                return BadRequest(new { message = "Formato de data inválido." });

            if (duracaoTotal <= 0)
                return BadRequest(new { message = "Duração do serviço inválida." });

            if (dataSelecionada.Date < DateTime.Now.Date)
                return Ok(new List<Horario>()); 

            try
            {
                var horarios = _horarioRepo.ObterHorariosDisponiveis(dataSelecionada, duracaoTotal);
                
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
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                {
                    return Unauthorized(new { message = "Sua sessão expirou." });
                }

                // ALTERAÇÃO: Agora passamos a Observação para o repositório
                Atendimento novoAtendimento = _atendimentoRepo.AdicionarAtendimento(
                    clienteId, 
                    model.ServicoId, 
                    model.HorarioId,
                    model.Observacao // <-- Passando a OBS
                );
                
                return Ok(new { message = "Agendamento confirmado!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao CriarAgendamento: {ex.Message}");
                
                if (ex.Message.StartsWith("Conflito"))
                {
                    return Conflict(new { message = "Desculpe, este horário foi ocupado. Por favor, escolha outro." });
                }
                
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
            
            var atendimento = _atendimentoRepo.ObterAtendimentoPorId(id);

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
        
        var atendimento = _atendimentoRepo.ObterAtendimentoPorId(id);

        if (atendimento == null || atendimento.IdCliente != clienteId) 
            return NotFound();

        if (atendimento.AtendStatus != 1) // 1 = Agendado
        {
            string statusTexto = atendimento.AtendStatus == 2 ? "Concluído" : "Cancelado";
            TempData["MensagemErro"] = $"Agendamentos com status '{statusTexto}' não podem ser editados.";
            return RedirectToAction("ListaServico", "Servico");
        }

        var horarioAtual = _horarioRepo.ObterHorarioPorAtendimentoId(atendimento.AtendId);
        if (horarioAtual == null)
        {
            TempData["MensagemErro"] = "Erro: Não foi possível encontrar o slot de horário original.";
            return RedirectToAction("ListaServico", "Servico");
        }
        
        var servicoAtual = atendimento.Servicos.FirstOrDefault();
        if (servicoAtual == null)
        {
            TempData["MensagemErro"] = "Erro: Não foi possível encontrar o serviço original.";
            return RedirectToAction("ListaServico", "Servico");
        }

        // --- MUDANÇA AQUI: Carregamos e serializamos as durações ---
        var servicos = _servicoRepo.ObterTodosServicos();
        ViewBag.ServicosDuracao = JsonSerializer.Serialize(servicos.Select(s => new { id = s.ServicoId, duracao = s.ServicoDuracao }));
        // -----------------------------------------------------------

        // Monta o ViewModel
        var viewModel = new EditarAgendamentoViewModel
        {
            AtendimentoId = atendimento.AtendId,
            HorarioId = horarioAtual.HorarioId,
            ServicoId = servicoAtual.ServicoId, 
            
            Observacao = atendimento.AtendObs, 

            AtendimentoAtual = atendimento,
            HorarioAtual = horarioAtual,
            TodosOsServicos = new SelectList(servicos, "ServicoId", "ServicoDesc", servicoAtual.ServicoId)
        };
        
        return View(viewModel);
    }

        // POST: /Agendamento/Editar/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(int id, EditarAgendamentoViewModel model)
    {
        if (id != model.AtendimentoId) return NotFound();

        // --- CORREÇÃO: Removemos a validação de campos que não vêm do formulário ---
        // Isso impede que o ModelState falhe porque esses objetos estão nulos
        ModelState.Remove("AtendimentoAtual");
        ModelState.Remove("HorarioAtual");
        ModelState.Remove("TodosOsServicos");
        ModelState.Remove("Observacao"); // Caso seja opcional e esteja dando erro
        // --------------------------------------------------------------------------

        if (!ModelState.IsValid)
        {
            // Se cair aqui, vamos descobrir POR QUE caiu
            // Isso vai listar os erros no Console do Visual Studio para você ver
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Erro no campo {state.Key}: {error.ErrorMessage}");
                }
            }

            // Repopula os dados para não quebrar a tela
            var atendimento = _atendimentoRepo.ObterAtendimentoPorId(model.AtendimentoId);
            var horarioAtual = _horarioRepo.ObterHorarioPorAtendimentoId(model.AtendimentoId);
            var servicoAtual = atendimento?.Servicos.FirstOrDefault(); 

            var servicos = _servicoRepo.ObterTodosServicos();
            ViewBag.ServicosDuracao = JsonSerializer.Serialize(servicos.Select(s => new { id = s.ServicoId, duracao = s.ServicoDuracao }));

            model.AtendimentoAtual = atendimento;
            model.HorarioAtual = horarioAtual;
            model.TodosOsServicos = new SelectList(
                servicos, 
                "ServicoId", "ServicoDesc", 
                servicoAtual?.ServicoId 
            );
            
            // Adiciona uma mensagem visual para você saber que falhou na validação
            TempData["MensagemErro"] = "Verifique os campos preenchidos. (Erro de Validação)";
            
            return View(model);
        }

        try
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                return Unauthorized();

            // Chama o repositório
            _atendimentoRepo.AtualizarAtendimento(
                model.AtendimentoId, 
                model.ServicoId, 
                model.HorarioId, 
                clienteId,
                model.Observacao 
            );

            TempData["MensagemSucesso"] = "Agendamento atualizado com sucesso!";
            return RedirectToAction("ListaServico", "Servico");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao Editar (POST): {ex.Message}");
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

            var atendimento = _atendimentoRepo.ObterAtendimentoPorId(id);

            if (atendimento == null || atendimento.IdCliente != clienteId) 
                return NotFound();
            
            if (atendimento.AtendStatus != 1) // 1 = Agendado
            {
                string statusTexto = atendimento.AtendStatus == 2 ? "Concluído" : "Cancelado";
                TempData["MensagemErro"] = $"Agendamentos com status '{statusTexto}' não podem ser cancelados.";
                return RedirectToAction("ListaServico", "Servico");
            }
            return View(atendimento);
        }

        // POST: /Agendamento/ExcluirConfirmado/{id}
        [HttpPost, ActionName("ExcluirConfirmado")]
        [ValidateAntiForgeryToken]
        public IActionResult ExcluirPost(int AtendId, string motivo) 
        {
            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId))
                {
                    return Unauthorized();
                }

                // Chama o repositório passando o motivo (obrigatório)
                bool sucesso = _atendimentoRepo.CancelarAtendimento(AtendId, clienteId, motivo);

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
                TempData["MensagemErro"] = $"Erro ao cancelar: {ex.Message}";
            }
            
            return RedirectToAction("ListaServico", "Servico");
        }
    }
}