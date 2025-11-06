using Microsoft.AspNetCore.Mvc;
using AgendaTatiNails.Repositories;
using Microsoft.AspNetCore.Authorization;
using AgendaTatiNails.Models;
using AgendaTatiNails.Models.ViewModels; 
using System.Linq;
using System.Collections.Generic;
using System;

namespace AgendaTatiNails.Controllers
{
    [Authorize(Roles = "Profissional")]
    public class AdminController : Controller
    {
        private readonly IAgendaRepository _repository;

        public AdminController(IAgendaRepository repository)
        {
            _repository = repository;
        }

        // GET: /Admin/Index (Dashboard "Hoje")
        public IActionResult Index()
        {
            var hoje = DateTime.Today;
            
            // 1. Busca TODOS os atendimentos
            var todosAtendimentos = _repository.ObterTodosAtendimentos().ToList();

            // 2. Filtra para os agendamentos de HOJE (para a tabela)
            var agendamentosDeHoje = todosAtendimentos
                .Where(a => a.AtendDataAtend.Date == hoje)
                .OrderBy(a => a.AtendDataAtend)
                .ToList();

            // 3. Calcula os dados para os STAT CARDS
            
            // Card 1: Contagem de Hoje (Apenas status 1 = "Agendado")
            ViewBag.CountHoje = agendamentosDeHoje
                .Count(a => a.AtendStatus == 1); 

            // Card 2: Contagem Próximos 7 Dias (Apenas status 1 = "Agendado")
            ViewBag.CountProximos7Dias = todosAtendimentos
                .Count(a => a.AtendDataAtend.Date > hoje && 
                            a.AtendDataAtend.Date <= hoje.AddDays(7) &&
                            a.AtendStatus == 1); 
            
            // Card 3: Faturamento de Hoje (Correto: Apenas status 2 = "Concluído")
            decimal faturamentoHoje = agendamentosDeHoje
                .Where(a => a.AtendStatus == 2) 
                .Sum(a => a.AtendPrecoFinal ?? 0m); 
            
            ViewBag.FaturamentoHoje = faturamentoHoje;

            // 4. Passa a lista de agendamentos DE HOJE (TODOS os status) para a tabela na View
            return View(agendamentosDeHoje); 
        }

        // GET: /Admin/Agendamentos?dataPesquisa=YYYY-MM-DD
        [HttpGet]
        public IActionResult Agendamentos(string? dataPesquisa) 
        {
            var agendamentosDoDia = new List<Atendimento>();
            DateTime dataSelecionada;

            if (DateTime.TryParse(dataPesquisa, out dataSelecionada))
            {
                agendamentosDoDia = _repository.ObterTodosAtendimentos()
                    .Where(a => a.AtendDataAtend.Date == dataSelecionada.Date)
                    .OrderBy(a => a.AtendDataAtend)
                    .ToList();
            }
            else
            {
                dataSelecionada = DateTime.Today;
            }

            ViewBag.DataPesquisada = dataSelecionada.ToString("yyyy-MM-dd");

            // TODO: A View 'Agendamentos.cshtml' precisa ser atualizada
            // para exibir @model List<Atendimento>
            return View(agendamentosDoDia);
        }

        // =====================================================================
        // ADMIN - MUDANÇA DE STATUS (AÇÕES POST)
        // =====================================================================

        // POST: /Admin/MarcarConcluido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarcarConcluido(int id)
        {
            try
            {
                _repository.MarcarAtendimentoConcluido(id);
                TempData["MensagemSucesso"] = $"Agendamento (ID: {id}) foi marcado como Concluído!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao MarcarConcluido: {ex.Message}");
                TempData["MensagemErro"] = $"Erro ao marcar como concluído: {ex.Message}";
            }
            
            // Redireciona de volta para a página de onde veio (Dashboard ou Agendamentos)
            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? Url.Action("Index") : refererUrl);
        }

        // POST: /Admin/CancelarAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarAdmin(int id) 
        {
            try
            {
                _repository.CancelarAtendimentoAdmin(id);
                TempData["MensagemSucesso"] = $"Agendamento (ID: {id}) foi Cancelado.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao CancelarAdmin: {ex.Message}");
                TempData["MensagemErro"] = $"Erro ao cancelar: {ex.Message}";
            }

            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? Url.Action("Index") : refererUrl);
        }

        // =====================================================================
        // [AÇÃO ATUALIZADA] GET: /Admin/Financeiro
        // =====================================================================
        public IActionResult Financeiro()
        {
            var todosAtendimentos = _repository.ObterTodosAtendimentos();

            // Filtrar apenas os concluídos (Assumindo 2 = Concluído)
            var agendamentosConcluidos = todosAtendimentos
                .Where(a => a.AtendStatus == 2) 
                .ToList();

            // Calcular totais para os cards
            // Usamos o AtendPrecoFinal, que é muito mais preciso!
            decimal faturamentoTotal = agendamentosConcluidos.Sum(a => a.AtendPrecoFinal ?? 0m);
            int totalServicosConcluidos = agendamentosConcluidos.Count();

            // REMOVIDO: Faturamento por Tipo de Serviço
            // Com o novo banco, um Atendimento tem MÚLTIPLOS serviços
            // e um ÚNICO preço final. Não é mais possível agrupar por nome de serviço.
            
            // Enviar dados para a View
            ViewBag.FaturamentoTotal = faturamentoTotal;
            ViewBag.TotalServicosConcluidos = totalServicosConcluidos;
            ViewBag.FaturamentoPorServico = new List<ServicoFaturadoViewModel>(); // Envia lista vazia

            return View(agendamentosConcluidos.OrderByDescending(a => a.AtendDataAtend)); 
        }
    }
}