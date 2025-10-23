using AgendaTatiNails.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgendaTatiNails.Services
{
    public class InMemoryDataService
    {
        private static List<Cliente> _clientes = new List<Cliente>();
        private static List<Profissional> _profissionais = new List<Profissional>();
        private static List<Servico> _servicos = new List<Servico>();
        private static List<Agendamento> _agendamentos = new List<Agendamento>();
        private static int _proximoAgendamentoId = 1;
        private const int DuracaoSlotMinutos = 45;
        private static readonly object _lockClientes = new object();
        private static readonly object _lockAgendamentos = new object();
        private static bool _dadosIniciaisCarregados = false; 

       
        public IReadOnlyList<Cliente> Clientes => _clientes.AsReadOnly();
        public IReadOnlyList<Profissional> Profissionais => _profissionais.AsReadOnly();
        public IReadOnlyList<Servico> Servicos => _servicos.AsReadOnly();
        public IReadOnlyList<Agendamento> Agendamentos => _agendamentos.AsReadOnly();
 

        public InMemoryDataService()
        {
            // Carrega dados iniciais apenas uma vez de forma thread-safe
            lock (typeof(InMemoryDataService))
            {
                if (!_dadosIniciaisCarregados)
                {
                    CarregarDadosIniciais();
                    _dadosIniciaisCarregados = true;
                }
            }
        }

        private static void CarregarDadosIniciais()
        {
             if (!_clientes.Any()) {
                _clientes.AddRange(new List<Cliente> {
                    new Cliente { Id = 1, Nome = "Ana Silva", Email = "ana@email.com", Senha = "123", Telefone = "11988887777" },
                    new Cliente { Id = 2, Nome = "Beatriz Costa", Email = "bia@email.com", Senha = "123", Telefone = "11955554444" }
                });
            }
             if (!_profissionais.Any()) {
                _profissionais.AddRange(new List<Profissional> {
                    new Profissional { Id = 1, Nome = "Tati (Profissional)", Email = "tati@email.com", Senha = "admin123" }
                });
            }
             if (!_servicos.Any()) {
                _servicos.AddRange(new List<Servico> {
                    new Servico { Id = 1, Nome = "Mão", Descricao = "Manicure completa.", Preco = 45.00m, DuracaoEmMinutos = 40 },
                    new Servico { Id = 2, Nome = "Pé", Descricao = "Pedicure completo.", Preco = 55.00m, DuracaoEmMinutos = 45 },
                    new Servico { Id = 3, Nome = "Pé e Mão", Descricao = "Combo manicure e pedicure.", Preco = 95.00m, DuracaoEmMinutos = 80 }
                });
            }
             if (!_agendamentos.Any()) {
                _agendamentos.AddRange(new List<Agendamento> {
                    new Agendamento { Id = 1, ClienteId = 1, ProfissionalId = 1, ServicoId = 1, DataHora = DateTime.Now.AddDays(3).Date.AddHours(14), Status = "Agendado" },
                    new Agendamento { Id = 2, ClienteId = 1, ProfissionalId = 1, ServicoId = 3, DataHora = DateTime.Now.AddDays(10).Date.AddHours(10), Status = "Agendado" },
                    new Agendamento { Id = 3, ClienteId = 2, ProfissionalId = 1, ServicoId = 2, DataHora = DateTime.Now.AddDays(5).Date.AddHours(16), Status = "Agendado" }
                });
                _proximoAgendamentoId = _agendamentos.Max(a => a.Id) + 1; 
            }
        }


        // --- MÉTODOS CRUD CLIENTE ---
        public Cliente AdicionarCliente(Cliente novoCliente)
        {
             lock(_lockClientes)
             {
                novoCliente.Id = _clientes.Any() ? _clientes.Max(c => c.Id) + 1 : 1;
                _clientes.Add(novoCliente);
                Console.WriteLine($"[InMemory] Cliente adicionado: ID={novoCliente.Id}");
                return novoCliente;
             }
        }

        // --- MÉTODOS CRUD AGENDAMENTO ---
        public Agendamento AdicionarAgendamento(Agendamento novoAgendamento)
        {
             lock(_lockAgendamentos)
             {
                novoAgendamento.Id = _proximoAgendamentoId++;
                _agendamentos.Add(novoAgendamento);
                Console.WriteLine($"[InMemory] Agendamento adicionado: ID={novoAgendamento.Id}");
                return novoAgendamento;
             }
        }

        public IEnumerable<Agendamento> ObterAgendamentosPorCliente(int clienteId)
        {
            lock(_lockAgendamentos)
            {
                return _agendamentos
                    .Where(a => a.ClienteId == clienteId)
                    .Select(a => {
                        a.Servico = _servicos?.FirstOrDefault(s => s.Id == a.ServicoId);
                        return a;
                    })
                    .OrderBy(a => a.DataHora)
                    .ToList(); 
            }
        }

        public Agendamento? ObterAgendamentoPorId(int agendamentoId) 
        {
            lock(_lockAgendamentos)
            {
                 var agendamento = _agendamentos.FirstOrDefault(a => a.Id == agendamentoId);
                 if (agendamento != null) {
                     agendamento.Servico = _servicos?.FirstOrDefault(s => s.Id == agendamento.ServicoId);
                 }
                 return agendamento; 
            }
        }

        public bool AtualizarAgendamento(Agendamento agendamentoAtualizado)
        {
            lock (_lockAgendamentos)
            {
                var agendamentoExistente = _agendamentos.FirstOrDefault(a => a.Id == agendamentoAtualizado.Id);
                if (agendamentoExistente == null) return false;

                agendamentoExistente.DataHora = agendamentoAtualizado.DataHora;
                agendamentoExistente.ServicoId = agendamentoAtualizado.ServicoId;
                // agendamentoExistente.Status = agendamentoAtualizado.Status;
                Console.WriteLine($"[InMemory] Agendamento atualizado: ID={agendamentoExistente.Id}");
                return true;
            }
        }

        public bool RemoverAgendamento(int agendamentoId)
        {
             lock (_lockAgendamentos)
             {
                var agendamentoParaRemover = _agendamentos.FirstOrDefault(a => a.Id == agendamentoId);
                if (agendamentoParaRemover == null) return false;
                _agendamentos.Remove(agendamentoParaRemover);
                Console.WriteLine($"[InMemory] Agendamento removido: ID={agendamentoId}");
                return true;
             }
        }

        // --- MÉTODOS PARA VERIFICAR DISPONIBILIDADE ---
        public int ObterDuracaoServico(int servicoId)
        {
            return _servicos?.FirstOrDefault(s => s.Id == servicoId)?.DuracaoEmMinutos ?? 0;
        }

        // --- MÉTODOS PARA VERIFICAR DISPONIBILIDADE ---

        public Dictionary<DateTime, int> ObterSlotsOcupadosComIdAgendamento(DateTime data)
        {
            var slotsOcupados = new Dictionary<DateTime, int>();
            List<Agendamento> agendamentosDoDia;
            lock(_lockAgendamentos)
            {
                 agendamentosDoDia = _agendamentos
                    .Where(a => a.DataHora.Date == data.Date)
                    .ToList();
            }

            foreach (var agendamento in agendamentosDoDia)
            {
                int duracaoMinutos = ObterDuracaoServico(agendamento.ServicoId);
                if (duracaoMinutos <= 0) {
                     continue;
                }

                int slotsNecessarios = (int)Math.Ceiling((double)duracaoMinutos / DuracaoSlotMinutos);

                for (int i = 0; i < slotsNecessarios; i++)
                {
                    DateTime slotAtual = agendamento.DataHora.AddMinutes(i * DuracaoSlotMinutos);
                    slotsOcupados[slotAtual] = agendamento.Id;
                }
            }
            return slotsOcupados;
        }

        public bool VerificarDisponibilidadeSlot(DateTime dataHoraInicio, int duracaoMinutos, Dictionary<DateTime, int> slotsOcupados, int? agendamentoIdParaIgnorar = null)
        {
            int slotsNecessarios = (int)Math.Ceiling((double)duracaoMinutos / DuracaoSlotMinutos);

            for (int i = 0; i < slotsNecessarios; i++)
            {
                DateTime slotAtual = dataHoraInicio.AddMinutes(i * DuracaoSlotMinutos);

                if (slotsOcupados.TryGetValue(slotAtual, out int idAgendamentoOcupante))
                {

                    if (agendamentoIdParaIgnorar.HasValue && idAgendamentoOcupante == agendamentoIdParaIgnorar.Value)
                    {
                        continue; // Ocupado por ele mesmo, ignora este slot e verifica o próximo
                    }
                    else
                    {
                        return false; // Ocupado por outro
                    }
                } else {
                }
            }
            return true; 
        }
    }
}