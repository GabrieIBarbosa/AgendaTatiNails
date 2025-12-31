using AgendaTatiNails.Models;
using System.Collections.Generic;
using System;

namespace AgendaTatiNails.Repositories.Interfaces
{
    public interface IAtendimentoRepository
    {
        // Altere essas duas linhas na sua Interface:
        Atendimento AdicionarAtendimento(int clienteId, int servicoId, int horarioId, string obs = null);
        
        IEnumerable<Atendimento> ObterAtendimentosPorCliente(int clienteId);
        IEnumerable<Atendimento> ObterTodosAtendimentos();
        Atendimento ObterAtendimentoPorId(int id);
        bool AtualizarAtendimento(int atendimentoId, int novoServicoId, int novoHorarioId, int clienteId, string obs = null); 
        
        bool CancelarAtendimento(int atendimentoId, int clienteId, string motivo); 
        
        bool MarcarAtendimentoConcluido(int atendimentoId);
        bool CancelarAtendimentoAdmin(int atendimentoId, string motivo); 
    }
}