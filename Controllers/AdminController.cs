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
        private readonly InMemoryDataService _dataService;

        public AdminController(InMemoryDataService dataService)
        {
            _dataService = dataService;
        }

        // GET: /Admin/Index (Dashboard "Hoje")
        public IActionResult Index()
        {
            var hoje = DateTime.Today;
            
            // 1. Busca TODOS os agendamentos e preenche os dados de serviço/cliente
            var todosAgendamentos = _dataService.ObterTodosAgendamentosComClienteEServico().ToList();

            // 2. Filtra para os agendamentos de HOJE (para a tabela)
            var agendamentosDeHoje = todosAgendamentos
                .Where(a => a.DataHora.Date == hoje)
                .OrderBy(a => a.DataHora)
                .ToList();

            // 3. Calcula os dados para os STAT CARDS
            
            // Card 1: Contagem de Agendamentos de Hoje (todos os status)
            ViewBag.CountHoje = agendamentosDeHoje.Count();

            // Card 2: Contagem de Agendamentos dos Próximos 7 Dias (excluindo hoje)
            ViewBag.CountProximos7Dias = todosAgendamentos
                .Count(a => a.DataHora.Date > hoje && a.DataHora.Date <= hoje.AddDays(7));
            
            // Card 3: Faturamento de Hoje (Apenas status "Concluído")
            decimal faturamentoHoje = agendamentosDeHoje
                .Where(a => a.Status == "Concluído" && a.Servico != null) // Filtra concluídos
                .Sum(a => a.Servico.Preco); // Soma o preço
            
            ViewBag.FaturamentoHoje = faturamentoHoje;

            // 4. Passa a lista de agendamentos DE HOJE para a tabela na View
            return View(agendamentosDeHoje); 
        }

        // GET: /Admin/Agendamentos?dataPesquisa=YYYY-MM-DD
        // (Ação "Ver Agendamentos", agora com busca por data)
        [HttpGet]
        public IActionResult Agendamentos(string? dataPesquisa) // Recebe a data da busca
        {
            var agendamentosDoDia = new List<Agendamento>();
            DateTime dataSelecionada;

            if (DateTime.TryParse(dataPesquisa, out dataSelecionada))
            {
                // Se uma data válida foi enviada, busca os dados
                agendamentosDoDia = _dataService.ObterTodosAgendamentosComClienteEServico()
                    .Where(a => a.DataHora.Date == dataSelecionada.Date)
                    .OrderBy(a => a.DataHora)
                    .ToList();
            }
            else
            {
                // Se nenhuma data foi enviada (primeiro carregamento), define a data de hoje
                dataSelecionada = DateTime.Today;
            }

            // Passa a data selecionada para a View (para preencher o input)
            ViewBag.DataPesquisada = dataSelecionada.ToString("yyyy-MM-dd");

            // Passa a lista (vazia ou preenchida) para a View
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
            var agendamento = _dataService.ObterAgendamentoPorId(id);
            if (agendamento == null)
            {
                TempData["MensagemErro"] = "Agendamento não encontrado.";
            }
            else
            {
                // [Validação Futura] Poderíamos verificar se a data já passou, etc.
                
                agendamento.Status = "Concluído";
                _dataService.AtualizarAgendamento(agendamento);

                TempData["MensagemSucesso"] = $"Agendamento (ID: {id}) foi marcado como Concluído!";
            }
            
            // Redireciona de volta para a página de onde veio (Dashboard ou Calendário)
            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? Url.Action("Index") : refererUrl);
        }

        // POST: /Admin/CancelarAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarAdmin(int id) // Nome da Ação bate com o <form>
        {
            var agendamento = _dataService.ObterAgendamentoPorId(id);
            if (agendamento == null)
            {
                TempData["MensagemErro"] = "Agendamento não encontrado.";
            }
            else
            {
                // [Validação Futura]

                agendamento.Status = "Cancelado";
                _dataService.AtualizarAgendamento(agendamento);

                TempData["MensagemSucesso"] = $"Agendamento (ID: {id}) foi Cancelado.";
            }

            string refererUrl = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refererUrl) ? Url.Action("Index") : refererUrl);
        }

        // =====================================================================
        // [AÇÃO ATUALIZADA] GET: /Admin/Financeiro
        // =====================================================================
        public IActionResult Financeiro()
        {
            // 1. Buscar todos os agendamentos e pré-carregar dados
            var todosAgendamentos = _dataService.ObterTodosAgendamentosComClienteEServico();

            // 2. Filtrar apenas os concluídos
            var agendamentosConcluidos = todosAgendamentos
                .Where(a => a.Status == "Concluído")
                .ToList();

            // 3. Calcular totais para os cards
            // Garante que o preço seja 0 se o serviço for nulo
            decimal faturamentoTotal = agendamentosConcluidos.Sum(a => a.Servico?.Preco ?? 0m);
            int totalServicosConcluidos = agendamentosConcluidos.Count();

            // 4. Calcular faturamento por tipo de serviço
            var faturamentoPorServico = agendamentosConcluidos
                .Where(a => a.Servico != null) // Garante que o serviço não é nulo
                .GroupBy(a => a.Servico.Nome) // Agrupa por "Mão", "Pé", etc.
                .Select(g => new ServicoFaturadoViewModel
                {
                    ServicoNome = g.Key,
                    Quantidade = g.Count(),
                    Total = g.Sum(a => a.Servico?.Preco ?? 0m)
                })
                .OrderByDescending(s => s.Total)
                .ToList();

            // 5. Enviar dados para a View
            ViewBag.FaturamentoTotal = faturamentoTotal;
            ViewBag.TotalServicosConcluidos = totalServicosConcluidos;
            ViewBag.FaturamentoPorServico = faturamentoPorServico; // Envia a lista de breakdown

            // Passa a lista detalhada de agendamentos concluídos como Model
            return View(agendamentosConcluidos.OrderByDescending(a => a.DataHora)); 
        }
    }
}