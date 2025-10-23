using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks; // Mantido para async Task<IActionResult>
using AgendaTatiNails.Models;
using AgendaTatiNails.Models.ViewModels;
using AgendaTatiNails.Repositories;

namespace AgendaTatiNails.Controllers
{
    [Authorize]
    public class AgendamentoController : Controller
    {
        private readonly InMemoryDataService _dataService;
        private const int DuracaoSlotMinutos = 45;

        public AgendamentoController(InMemoryDataService dataService)
        {
            _dataService = dataService;
        }

        // =====================================================================
        // CRIAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento?serviceId=X
        [HttpGet]
        public IActionResult Index(string serviceId)
        {
            ViewBag.ServiceId = serviceId;
            return View("~/Views/Agendamento/Index.cshtml");
        }

        // GET: /Agendamento/GetHorariosDisponiveis?data=YYYY-MM-DD&servicoId=X[&agendamentoIdSendoEditado=Y]
        [HttpGet]
        public IActionResult GetHorariosDisponiveis(string data, int servicoId, int? agendamentoIdSendoEditado = null)
        {
            // Validações de Input
            if (!DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dataSelecionada))
                return BadRequest(new { message = "Formato de data inválido." });

            int duracaoServico = _dataService.ObterDuracaoServico(servicoId);
            if (duracaoServico <= 0)
                return BadRequest(new { message = "Serviço inválido." });

            int slotsNecessarios = (int)Math.Ceiling((double)duracaoServico / DuracaoSlotMinutos);

            // Geração e Filtro Inicial de Horários
            var horariosPotenciais = GerarHorariosPotenciais();
            var agora = DateTime.Now;
            if (dataSelecionada.Date == agora.Date) {
                horariosPotenciais = agora.Hour >= 12
                    ? new List<TimeSpan>()
                    : horariosPotenciais.Where(h => h >= new TimeSpan(13, 30, 0)).ToList();
            } else if (dataSelecionada.Date < agora.Date) {
                 horariosPotenciais = new List<TimeSpan>();
            }

            // *** USA O MÉTODO CORRETO QUE RETORNA DICIONÁRIO ***
            var slotsOcupados = _dataService.ObterSlotsOcupadosComIdAgendamento(dataSelecionada.Date);
            // **************************************************

            // Filtrar Disponíveis
            var horariosDisponiveis = new List<string>();
            var ultimoSlotManha = new TimeSpan(11, 45, 0);
            var ultimoSlotTarde = new TimeSpan(17, 15, 0);

            foreach (var horarioInicio in horariosPotenciais)
            {
                DateTime dataHoraInicio = dataSelecionada.Date + horarioInicio;
                if (slotsNecessarios > 1 && (horarioInicio == ultimoSlotManha || horarioInicio == ultimoSlotTarde))
                    continue;

                // *** USA O MÉTODO CORRETO QUE RECEBE DICIONÁRIO E ID ***
                if (_dataService.VerificarDisponibilidadeSlot(dataHoraInicio, duracaoServico, slotsOcupados, agendamentoIdSendoEditado))
                // ******************************************************
                {
                    horariosDisponiveis.Add(horarioInicio.ToString(@"hh\:mm"));
                }
            }
            return Ok(horariosDisponiveis);
        }

        // POST: /Agendamento/CriarAgendamento
        [HttpPost]
        public IActionResult CriarAgendamento([FromBody] AgendamentoViewModel model)
        {
             if (!ModelState.IsValid) return BadRequest(new { message = "Dados inválidos." });
             if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) return Unauthorized(/*...*/);
             if (!int.TryParse(model.ServicoId, out int servicoId) || !DateTime.TryParseExact($"{model.Data} {model.Hora}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dataHoraAgendamento)) return BadRequest(/*...*/);
             int duracaoServico = _dataService.ObterDuracaoServico(servicoId);
             if (duracaoServico <= 0) return BadRequest(/*...*/);
             string erroRegra = ValidarRegrasDeHorario(dataHoraAgendamento);
             if (erroRegra != null) return BadRequest(new { message = erroRegra });

            // *** USA O MÉTODO CORRETO QUE RETORNA DICIONÁRIO ***
            var slotsOcupados = _dataService.ObterSlotsOcupadosComIdAgendamento(dataHoraAgendamento.Date);
            // *** USA O MÉTODO CORRETO QUE RECEBE DICIONÁRIO ***
            if (!_dataService.VerificarDisponibilidadeSlot(dataHoraAgendamento, duracaoServico, slotsOcupados)) // Sem ID a ignorar
            // ************************************************
                return Conflict(new { message = "Desculpe, este horário foi agendado. Escolha outro." });

            var novoAgendamento = new Agendamento
            {
                ClienteId = clienteId,
                ServicoId = servicoId,
                DataHora = dataHoraAgendamento,
                ProfissionalId = 1,
                Status = "Agendado"
            };

            try {
                _dataService.AdicionarAgendamento(novoAgendamento);
                return Ok(new { message = "Agendamento confirmado!", redirectToUrl = Url.Action("ListaServico", "Servico") });
            } catch (Exception ex) {
                 Console.WriteLine($"Erro ao salvar agendamento: {ex.Message}");
                 return StatusCode(500, new { message = "Erro interno ao salvar." });
            }
        }

        // =====================================================================
        // DETALHES DO AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Detalhes/{id}
        [HttpGet]
        public IActionResult Detalhes(int id)
        {
            var agendamento = _dataService.ObterAgendamentoPorId(id);
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) return Unauthorized();
            if (agendamento == null || agendamento.ClienteId != clienteId) return NotFound();
            return View(agendamento);
        }

        // =====================================================================
        // EDITAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Editar/{id}
        [HttpGet]
        public IActionResult Editar(int id)
        {
            var agendamento = _dataService.ObterAgendamentoPorId(id);
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) return Unauthorized();
            if (agendamento == null || agendamento.ClienteId != clienteId) return NotFound();

            string erroPreEdicao = ValidarPossibilidadeDeAlteracao(agendamento);
            if (erroPreEdicao != null) {
                TempData["MensagemErro"] = erroPreEdicao;
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }

            var viewModel = new EditarAgendamentoViewModel
            {
                Id = agendamento.Id,
                ServicoId = agendamento.ServicoId,
                Data = agendamento.DataHora.Date,
                Hora = agendamento.DataHora.ToString("HH:mm"),
                Status = agendamento.Status,
                ServicosDisponiveis = new SelectList(_dataService.Servicos, "Id", "Nome", agendamento.ServicoId)
            };
            return View(viewModel);
        }

        // POST: /Agendamento/Editar/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, EditarAgendamentoViewModel model)
        {
            Console.WriteLine($"POST Editar ID={id}. Recebido: Serv={model.ServicoId}, Data={model.Data}, Hora={model.Hora}"); // LOG 1

            if (id != model.Id) {
                 Console.WriteLine("Editar Erro: ID da URL não confere com ID do modelo.");
                 return NotFound();
            }

            var agendamentoOriginal = _dataService.ObterAgendamentoPorId(model.Id);
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) {
                 Console.WriteLine("Editar Erro: Não foi possível obter ID do cliente.");
                 return Unauthorized(); // Ou Forbid() dependendo da sua política
            }
            if (agendamentoOriginal == null || agendamentoOriginal.ClienteId != clienteId) {
                Console.WriteLine($"Editar Erro: Agendamento ID={id} não encontrado ou não pertence ao cliente ID={clienteId}.");
                return Forbid();
            }

            // Valida se ainda pode ser alterado ANTES de verificar o ModelState
            string erroPreEdicao = ValidarPossibilidadeDeAlteracao(agendamentoOriginal);
            if (erroPreEdicao != null) {
                 Console.WriteLine($"Editar Erro Pre-Validação: {erroPreEdicao}");
                 ModelState.AddModelError(string.Empty, erroPreEdicao);
                 // Não retorna ainda, deixa o ModelState ser verificado
            } else {
                 Console.WriteLine("Editar Info: Passou na validação de possibilidade de alteração.");
            }


            if (ModelState.IsValid) // Verifica se o modelo básico E a regra acima passaram
            {
                 Console.WriteLine("Editar Info: ModelState é Válido. Prosseguindo com validações...");
                // Conversão e Validação dos Novos Dados
                if (!DateTime.TryParseExact($"{model.Data:yyyy-MM-dd} {model.Hora}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime novaDataHora)) {
                     Console.WriteLine("Editar Erro: Falha ao converter Data/Hora.");
                     ModelState.AddModelError("Hora", "Formato de data ou hora inválido.");
                     RepopularViewModelParaEdicao(model); return View(model);
                }
                 Console.WriteLine($"Editar Info: Nova DataHora = {novaDataHora}");

                int novaDuracao = _dataService.ObterDuracaoServico(model.ServicoId);
                if (novaDuracao <= 0) {
                     Console.WriteLine($"Editar Erro: Serviço ID={model.ServicoId} inválido.");
                     ModelState.AddModelError("ServicoId", "Serviço inválido.");
                     RepopularViewModelParaEdicao(model); return View(model);
                }

                string? erroRegra = ValidarRegrasDeHorario(novaDataHora);
                if (erroRegra != null) {
                    Console.WriteLine($"Editar Erro Regra Horário: {erroRegra}");
                    ModelState.AddModelError(string.Empty, erroRegra);
                    RepopularViewModelParaEdicao(model); return View(model);
                }

                // Validação de Disponibilidade (Ignorando o Próprio Agendamento)
                var slotsOcupadosNaData = _dataService.ObterSlotsOcupadosComIdAgendamento(novaDataHora.Date);
                if (!_dataService.VerificarDisponibilidadeSlot(novaDataHora, novaDuracao, slotsOcupadosNaData, model.Id)) {
                    Console.WriteLine("Editar Erro: Conflito de horário detectado.");
                    ModelState.AddModelError(string.Empty, "O horário selecionado conflita com outro agendamento.");
                    RepopularViewModelParaEdicao(model); return View(model);
                }

                // Atualização e Salvamento
                agendamentoOriginal.ServicoId = model.ServicoId;
                agendamentoOriginal.DataHora = novaDataHora;
                 Console.WriteLine($"Editar Info: Atualizando Agendamento ID={id} para Serv={model.ServicoId}, DataHora={novaDataHora}");
                try {
                    bool sucesso = _dataService.AtualizarAgendamento(agendamentoOriginal);
                     Console.WriteLine($"Editar Info: Resultado de AtualizarAgendamento = {sucesso}");
                    if (sucesso) {
                         Console.WriteLine("Editar Info: Atualização BEM SUCEDIDA. Redirecionando...");
                         TempData["MensagemSucesso"] = "Agendamento atualizado!";
                         return RedirectToAction(nameof(ServicoController.ListaServico), "Servico"); // Redireciona!
                    } else {
                         Console.WriteLine("Editar Erro: AtualizarAgendamento retornou false.");
                         ModelState.AddModelError(string.Empty, "Não foi possível salvar as alterações (erro interno no serviço).");
                    }
                } catch (Exception ex) {
                     Console.WriteLine($"ERRO FATAL ao atualizar agendamento ID {id}: {ex}");
                     ModelState.AddModelError(string.Empty, "Erro interno ao salvar.");
                }
            }
            else 
            {
                 Console.WriteLine("Editar Erro: ModelState INVÁLIDO.");
                 foreach(var state in ModelState) {
                     if (state.Value.Errors.Any()) {
                          Console.WriteLine($"- Erro Campo '{state.Key}': {string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                     }
                 }
            }

            // Se chegou aqui, houve erro (ModelState inválido ou falha ao salvar)
            Console.WriteLine("Editar Info: Retornando para a View de Edição com erros.");
            RepopularViewModelParaEdicao(model); 
            return View(model);
        }

        // =====================================================================
        // EXCLUIR/CANCELAR AGENDAMENTO
        // =====================================================================

        // GET: /Agendamento/Excluir/{id}
        [HttpGet]
        public IActionResult Excluir(int id)
        {
            var agendamento = _dataService.ObterAgendamentoPorId(id);
             if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) return Unauthorized();
             if (agendamento == null || agendamento.ClienteId != clienteId) return NotFound();

            string erroPreExclusao = ValidarPossibilidadeDeAlteracao(agendamento);
            if (erroPreExclusao != null) {
                TempData["MensagemErro"] = erroPreExclusao;
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }
            return View(agendamento);
        }

        // POST: /Agendamento/ExcluirConfirmado/{id}
        [HttpPost, ActionName("ExcluirConfirmado")]
        [ValidateAntiForgeryToken]
        public IActionResult ExcluirPost(int id)
        {
            var agendamento = _dataService.ObterAgendamentoPorId(id);
             if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int clienteId)) return Unauthorized();
             if (agendamento == null || agendamento.ClienteId != clienteId) return Forbid();

            string erroPreExclusao = ValidarPossibilidadeDeAlteracao(agendamento);
            if (erroPreExclusao != null) {
                TempData["MensagemErro"] = erroPreExclusao;
                return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
            }

            try {
                if (_dataService.RemoverAgendamento(id)) {
                    TempData["MensagemSucesso"] = "Agendamento cancelado!";
                } else { TempData["MensagemErro"] = "Agendamento não encontrado."; }
            } catch (Exception ex) {
                 Console.WriteLine($"Erro ao remover agendamento ID {id}: {ex.Message}");
                 TempData["MensagemErro"] = "Erro interno ao cancelar.";
            }
            return RedirectToAction(nameof(ServicoController.ListaServico), "Servico");
        }


        // =====================================================================
        // MÉTODOS AUXILIARES PRIVADOS
        // =====================================================================

        private List<TimeSpan> GerarHorariosPotenciais()
        {
            var horarios = new List<TimeSpan>();
            for (TimeSpan time = new TimeSpan(8, 0, 0); time <= new TimeSpan(11, 45, 0); time = time.Add(TimeSpan.FromMinutes(DuracaoSlotMinutos))) { horarios.Add(time); }
            for (TimeSpan time = new TimeSpan(13, 30, 0); time <= new TimeSpan(17, 15, 0); time = time.Add(TimeSpan.FromMinutes(DuracaoSlotMinutos))) { horarios.Add(time); }
            return horarios;
        }

        private string? ValidarRegrasDeHorario(DateTime dataHoraAgendamento)
        {
             var agora = DateTime.Now;
             if (dataHoraAgendamento < agora.AddMinutes(-5)) return "Não é possível agendar/reagendar para horários passados.";
             if (dataHoraAgendamento.Date == agora.Date) {
                 if (agora.Hour >= 12) return "Não é possível agendar/reagendar para hoje após o meio-dia.";
                 if (agora.Hour < 12 && dataHoraAgendamento.Hour < 13) return "Agendamentos/reagendamentos para hoje só podem ser feitos à tarde.";
             }
             return null;
        }

        private string? ValidarPossibilidadeDeAlteracao(Agendamento agendamento)
        {
            if (agendamento == null) return "Agendamento inválido.";
            if (agendamento.DataHora < DateTime.Now.AddMinutes(-5)) return "Agendamento passado não pode ser alterado/cancelado.";
            if (agendamento.Status == "Concluído" || agendamento.Status == "Cancelado") return $"Agendamento '{agendamento.Status}' não pode ser alterado/cancelado.";
            return null;
        }

        private void RepopularViewModelParaEdicao(EditarAgendamentoViewModel model)
        {
             model.ServicosDisponiveis = new SelectList(_dataService.Servicos, "Id", "Nome", model.ServicoId);
        }

    } // Fim da Classe
} // Fim do Namespace