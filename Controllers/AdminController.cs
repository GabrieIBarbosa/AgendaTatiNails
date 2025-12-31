using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; 
using AgendaTatiNails.Repositories.Interfaces; 
using Microsoft.AspNetCore.Authorization;
using AgendaTatiNails.Models;
using AgendaTatiNails.Models.ViewModels; 
using System.Linq;
using System.Collections.Generic;
using System;
using System.Text.Json; 

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Profissional")] // Certifique-se que o usuário logado tem essa Role
    public class AdminController : Controller
    {
        private readonly IAtendimentoRepository _atendimentoRepository;
        private readonly IServicoRepository _servicoRepository; 
        private readonly IHorarioRepository _horarioRepository; 

        public AdminController(
            IAtendimentoRepository atendimentoRepository,
            IServicoRepository servicoRepository,
            IHorarioRepository horarioRepository)
        {
            _atendimentoRepository = atendimentoRepository;
            _servicoRepository = servicoRepository;
            _horarioRepository = horarioRepository;
        }

        // =====================================================================
        // DASHBOARD E LISTAGEM
        // =====================================================================

        // GET: /Admin/Index (Dashboard "Hoje")
        public IActionResult Index()
        {
            var hoje = DateTime.Today;
            var todosAtendimentos = _atendimentoRepository.ObterTodosAtendimentos().ToList();

            // Filtra para os agendamentos de HOJE
            var agendamentosDeHoje = todosAtendimentos
                .Where(a => a.AtendDataAtend.Date == hoje)
                .OrderBy(a => a.AtendDataAtend)
                .ToList();

            // Estatísticas
            ViewBag.CountHoje = agendamentosDeHoje.Count(a => a.AtendStatus == 1); 
            ViewBag.CountProximos7Dias = todosAtendimentos
                .Count(a => a.AtendDataAtend.Date > hoje && 
                            a.AtendDataAtend.Date <= hoje.AddDays(7) &&
                            a.AtendStatus == 1); 
            
            decimal faturamentoHoje = agendamentosDeHoje
                .Where(a => a.AtendStatus == 2) 
                .Sum(a => a.AtendPrecoFinal ?? 0m); 
            ViewBag.FaturamentoHoje = faturamentoHoje;

            return View(agendamentosDeHoje); 
        }

        // =====================================================================
        // LISTAGEM DE AGENDAMENTOS (COM FILTRO DE DATA INÍCIO/FIM)
        // =====================================================================
        [HttpGet]
        public IActionResult Agendamentos(DateTime? dataInicio, DateTime? dataFim) 
        {
            // 1. Definição do Período
            // Padrão: Se não vier data, usa HOJE para ambos (mostra só o dia atual)
            DateTime inicio = dataInicio ?? DateTime.Today;
            DateTime fim = dataFim ?? DateTime.Today;

            // Segurança: Se inverterem as datas (Início maior que Fim)
            if (inicio > fim) 
            {
                var temp = inicio;
                inicio = fim;
                fim = temp;
            }

            // 2. Busca TUDO do banco
            var todosAtendimentos = _atendimentoRepository.ObterTodosAtendimentos();

            // 3. Filtra na Memória
            var agendamentosFiltrados = todosAtendimentos
                .Where(a => a.AtendDataAtend.Date >= inicio.Date && 
                            a.AtendDataAtend.Date <= fim.Date)
                .OrderBy(a => a.AtendDataAtend) // Ordena por data/hora
                .ToList();

            // 4. Devolve as datas para a View manter os campos preenchidos
            ViewBag.DataInicio = inicio;
            ViewBag.DataFim = fim;

            return View(agendamentosFiltrados);
        }

        // =====================================================================
        // AÇÕES DE STATUS (CONCLUIR E CANCELAR)
        // =====================================================================

        // POST: /Admin/MarcarConcluido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarcarConcluido(int id)
        {
            try
            {
                _atendimentoRepository.MarcarAtendimentoConcluido(id);
                TempData["MensagemSucesso"] = $"Agendamento (ID: {id}) foi marcado como Concluído!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao MarcarConcluido: {ex.Message}");
                TempData["MensagemErro"] = $"Erro ao marcar como concluído: {ex.Message}";
            }
            
            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? Url.Action("Index") : refererUrl);
        }

        // =====================================================================
        // NOVA LÓGICA DE CANCELAMENTO (COM MOTIVO)
        // =====================================================================

        // GET: /Admin/Cancelar/{id} -> Abre a tela para digitar o motivo
        [HttpGet]
        public IActionResult Cancelar(int id)
        {
            var atendimento = _atendimentoRepository.ObterAtendimentoPorId(id);
            if (atendimento == null) return NotFound();

            if (atendimento.AtendStatus != 1)
            {
                TempData["MensagemErro"] = "Este agendamento não pode ser cancelado (já foi concluído ou cancelado).";
                return RedirectToAction(nameof(Index));
            }

            return View(atendimento);
        }

        // POST: /Admin/CancelarConfirmado -> Processa o cancelamento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarConfirmado(int AtendId, string motivo)
        {
            try
            {
                // Chama o método novo do repositório que exige motivo
                _atendimentoRepository.CancelarAtendimentoAdmin(AtendId, motivo);
                TempData["MensagemSucesso"] = $"Agendamento (ID: {AtendId}) cancelado com sucesso!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao CancelarAdmin: {ex.Message}");
                TempData["MensagemErro"] = $"Erro ao cancelar: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // DETALHES E EDIÇÃO
        // =====================================================================

        [HttpGet]
        public IActionResult Detalhes(int id)
        {
            var atendimento = _atendimentoRepository.ObterAtendimentoPorId(id);
            if (atendimento == null) return NotFound();
            return View(atendimento);
        }

        // GET: /Admin/Editar/{id}
        [HttpGet]
        public IActionResult Editar(int id)
        {
            // Admin não precisa verificar se é o dono do agendamento
            var atendimento = _atendimentoRepository.ObterAtendimentoPorId(id);

            if (atendimento == null) 
                return NotFound();

            // Opcional: Se quiser impedir o Admin de editar cancelados
            if (atendimento.AtendStatus != 1) 
            {
                TempData["MensagemErro"] = "Apenas agendamentos ativos podem ser editados.";
                return RedirectToAction(nameof(Index));
            }

            var horarioAtual = _horarioRepository.ObterHorarioPorAtendimentoId(atendimento.AtendId);
            var servicoAtual = atendimento.Servicos.FirstOrDefault();

            if (horarioAtual == null || servicoAtual == null)
            {
                TempData["MensagemErro"] = "Erro: Dados inconsistentes no agendamento.";
                return RedirectToAction(nameof(Index));
            }

            // --- JSON de Duração (Igual ao do Cliente) ---
            var servicos = _servicoRepository.ObterTodosServicos();
            ViewBag.ServicosDuracao = JsonSerializer.Serialize(servicos.Select(s => new { id = s.ServicoId, duracao = s.ServicoDuracao }));
            // ---------------------------------------------

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

        // POST: /Admin/Editar/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, EditarAgendamentoViewModel model)
        {
            if (id != model.AtendimentoId) return NotFound();

            // Remove validações de objetos que vêm nulos do form
            ModelState.Remove("AtendimentoAtual");
            ModelState.Remove("HorarioAtual");
            ModelState.Remove("TodosOsServicos");

            if (!ModelState.IsValid)
            {
                // Recarrega dados em caso de erro
                var atendimento = _atendimentoRepository.ObterAtendimentoPorId(model.AtendimentoId);
                var horarioAtual = _horarioRepository.ObterHorarioPorAtendimentoId(model.AtendimentoId);
                var servicoAtual = atendimento?.Servicos.FirstOrDefault(); 

                var servicos = _servicoRepository.ObterTodosServicos();
                ViewBag.ServicosDuracao = JsonSerializer.Serialize(servicos.Select(s => new { id = s.ServicoId, duracao = s.ServicoDuracao }));

                model.AtendimentoAtual = atendimento;
                model.HorarioAtual = horarioAtual;
                model.TodosOsServicos = new SelectList(servicos, "ServicoId", "ServicoDesc", servicoAtual?.ServicoId);
                
                return View(model);
            }

            try
            {
                // --- DIFERENÇA IMPORTANTE DO ADMIN ---
                // Precisamos descobrir quem é o dono desse agendamento para passar pro Repositório
                var agendamentoOriginal = _atendimentoRepository.ObterAtendimentoPorId(model.AtendimentoId);
                int idDoClienteDono = agendamentoOriginal.IdCliente;
                // -------------------------------------

                _atendimentoRepository.AtualizarAtendimento(
                    model.AtendimentoId, 
                    model.ServicoId, 
                    model.HorarioId, 
                    idDoClienteDono, // Passamos o ID do cliente dono, não o do Admin logado
                    model.Observacao 
                );

                TempData["MensagemSucesso"] = "Agendamento atualizado com sucesso!";
                return RedirectToAction(nameof(Index)); // Volta para o Painel Admin
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao atualizar: {ex.Message}";
                return RedirectToAction(nameof(Editar), new { id = model.AtendimentoId });
            }
        }

        // =====================================================================
        // FINANCEIRO (COM FILTRO DE DATA)
        // =====================================================================
        [HttpGet]
        public IActionResult Financeiro(DateTime? dataInicio, DateTime? dataFim)
        {
            // 1. Definição do Período
            // Padrão: Se não vier data, pega do dia 1 do mês atual até hoje.
            DateTime inicio = dataInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fim = dataFim ?? DateTime.Today;

            // Segurança: Se inverterem as datas
            if (inicio > fim) { var temp = inicio; inicio = fim; fim = temp; }

            // 2. Busca todos os dados
            var todosAtendimentos = _atendimentoRepository.ObterTodosAtendimentos();

            // 3. APLICA O FILTRO:
            // - Deve estar Concluído (Status == 2)
            // - Data do atendimento deve estar dentro do período selecionado
            var agendamentosFiltrados = todosAtendimentos
                .Where(a => a.AtendStatus == 2 && 
                            a.AtendDataAtend.Date >= inicio.Date && 
                            a.AtendDataAtend.Date <= fim.Date)
                .ToList();

            // 4. Recalcula os Totais (Baseado apenas no FILTRADO)
            ViewBag.FaturamentoTotal = agendamentosFiltrados.Sum(a => a.AtendPrecoFinal ?? 0m);
            ViewBag.TotalServicosConcluidos = agendamentosFiltrados.Count();

            // 5. Recalcula o Resumo por Serviço (Baseado apenas no FILTRADO)
            var resumoServicos = agendamentosFiltrados
                .SelectMany(a => a.Servicos)
                .GroupBy(s => s.ServicoDesc)
                .Select(g => new ServicoFaturadoViewModel
                {
                    NomeServico = g.Key,
                    Quantidade = g.Count(),
                    ValorTotal = g.Sum(s => s.ServicoPreco)
                })
                .OrderByDescending(x => x.ValorTotal)
                .ToList();

            ViewBag.FaturamentoPorServico = resumoServicos;

            // 6. Passa as datas de volta para a View (para preencher os inputs)
            ViewBag.DataInicio = inicio;
            ViewBag.DataFim = fim;

            return View(agendamentosFiltrados.OrderByDescending(a => a.AtendDataAtend)); 
        }

        // =====================================================================
        // GERENCIAMENTO DE AGENDA (BLOQUEIOS)
        // =====================================================================

        // GET: /Admin/GerenciarAgenda
        public IActionResult GerenciarAgenda(DateTime? data)
        {
            // Se não passar data, usa hoje
            var dataFiltro = data ?? DateTime.Today;
            
            // IMPORTANTE: Certifique-se de ter adicionado o método 'ObterTodosHorariosDoDia'
            // na sua interface IHorarioRepository e na classe SqlHorarioRepository
            var horarios = _horarioRepository.ObterTodosHorariosDoDia(dataFiltro);
            
            ViewBag.DataSelecionada = dataFiltro;
            return View(horarios);
        }

        // POST: /Admin/BloquearDia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BloquearDia(DateTime data)
        {
            try 
            {
                _horarioRepository.BloquearDiaInteiro(data);
                TempData["MensagemSucesso"] = "Dia bloqueado com sucesso! Clientes não poderão agendar nesta data.";
            }
            catch(Exception ex)
            {
                TempData["MensagemErro"] = "Erro ao bloquear dia: " + ex.Message;
            }
            
            return RedirectToAction("GerenciarAgenda", new { data = data });
        }

        // POST: /Admin/AlternarBloqueioSlot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AlternarBloqueioSlot(int horarioId, int statusAtual, DateTime dataRedirecionamento)
        {
            try 
            {
                if (statusAtual == 1) // Se está livre, bloqueia (Status vira 3)
                {
                    _horarioRepository.BloquearHorario(horarioId);
                    TempData["MensagemSucesso"] = "Horário bloqueado.";
                }
                else if (statusAtual == 3) // Se está bloqueado, libera (Status vira 1)
                {
                    _horarioRepository.DesbloquearHorario(horarioId);
                    TempData["MensagemSucesso"] = "Horário liberado.";
                }
                // Se for 2 (Agendado), não faz nada aqui, precisa cancelar o atendimento primeiro
            }
            catch(Exception ex)
            {
                TempData["MensagemErro"] = "Erro ao alterar status do horário: " + ex.Message;
            }

            return RedirectToAction("GerenciarAgenda", new { data = dataRedirecionamento });
        }

        // =====================================================================
        // API AUXILIAR PARA O JAVASCRIPT (BUSCAR HORÁRIOS NO EDITAR)
        // =====================================================================
        [HttpGet]
        public IActionResult GetHorariosDisponiveis(string data, int duracaoTotal)
        {
            // Validações básicas para evitar erros
            if (!DateTime.TryParse(data, out DateTime dataSelecionada))
                return BadRequest(new { message = "Data inválida." });

            if (duracaoTotal <= 0)
                return BadRequest(new { message = "Duração inválida." });

            try
            {
                // Usa o repositório existente para buscar os horários
                // A lógica é a mesma do cliente: só mostra o que não está bloqueado
                var horarios = _horarioRepository.ObterHorariosDisponiveis(dataSelecionada, duracaoTotal);
                
                // Formata para JSON simples
                var horariosFormatados = horarios.Select(h => new 
                {
                    horarioId = h.HorarioId,
                    horario = h.HorarioPeriodo.ToString(@"hh\:mm")
                });

                return Ok(horariosFormatados); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro Admin GetHorarios: {ex.Message}");
                return StatusCode(500, new { message = "Erro interno ao buscar horários."});
            }
        }
    }
}