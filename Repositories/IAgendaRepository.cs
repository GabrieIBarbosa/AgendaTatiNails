using AgendaTatiNails.Models;
using System;
using System.Collections.Generic;

namespace AgendaTatiNails.Repositories
{
    // Este é o novo contrato que usa os modelos corretos do banco de dados
    public interface IAgendaRepository
    {
        // Métodos de Usuário/Cliente
        Usuario ObterUsuarioPorEmail(string email);
        Cliente ObterClientePorId(int id);
        
        // TODO: Método para criar novos clientes.
        Cliente AdicionarNovoCliente(Cliente novoCliente);

        // Métodos de Serviço
        Servico ObterServicoPorId(int id);
        IEnumerable<Servico> ObterTodosServicos();

        // Métodos de Atendimento (o novo "Agendamento")
        Atendimento ObterAtendimentoPorId(int id);
        IEnumerable<Atendimento> ObterAtendimentosPorCliente(int clienteId);
        IEnumerable<Atendimento> ObterTodosAtendimentos();


        Horario ObterHorarioPorAtendimentoId(int atendimentoId); 
        IEnumerable<Horario> ObterHorariosDisponiveis(DateTime data, int duracaoTotal);


        Atendimento AdicionarAtendimento(int clienteId, int servicoId, int horarioId);
        bool CancelarAtendimento(int atendimentoId, int clienteId);
        bool AtualizarAtendimento(int atendimentoId, int novoServicoId, int novoHorarioId, int clienteId);
        bool MarcarAtendimentoConcluido(int atendimentoId); // Lado do Admin
        bool CancelarAtendimentoAdmin(int atendimentoId); // Lado do Admin
    }
}