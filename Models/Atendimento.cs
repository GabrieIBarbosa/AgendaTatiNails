using System;
using System.Collections.Generic;

namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Atendimentos (seu "Agendamento" real)
    public class Atendimento
    {
        public int AtendId { get; set; }
        public int AtendStatus { get; set; }
        public DateTime AtendDataAgend { get; set; }
        public DateTime AtendDataAtend { get; set; }
        public string AtendObs { get; set; }
        public decimal? AtendPrecoFinal { get; set; }
        public int IdCliente { get; set; }
        public int IdColab { get; set; }
        public DateTime PagData { get; set; }
        public int PagStatus { get; set; }

        // Propriedades de navegação (úteis)
        public Cliente Cliente { get; set; }
        public Colaborador Colaborador { get; set; }
        public List<Servico> Servicos { get; set; } = new List<Servico>();
    }
}