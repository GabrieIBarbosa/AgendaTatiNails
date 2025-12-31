using System;
using System.Collections.Generic;
using AgendaTatiNails.Models;

namespace AgendaTatiNails.Repositories.Interfaces
{
    public interface IHorarioRepository
    {
        // Métodos de leitura para o Cliente/Agendamento
        Horario ObterHorarioPorAtendimentoId(int atendimentoId);
        IEnumerable<Horario> ObterHorariosDisponiveis(DateTime data, int duracaoTotal);

        // --- MÉTODOS ADICIONADOS PARA O ADMIN ---
        
        // Exibe a agenda completa (Livres, Agendados e Bloqueados)
        IEnumerable<Horario> ObterTodosHorariosDoDia(DateTime data); 

        // Ações de Bloqueio
        void BloquearHorario(int horarioId);
        void BloquearDiaInteiro(DateTime data);
        void DesbloquearHorario(int horarioId);
    }
}