using System;
using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models
{
    public class Agendamento
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int ProfissionalId { get; set; }
        public int ServicoId { get; set; }

        [Display(Name = "Data e Hora")]
        [Required(ErrorMessage = "Data e Hora são obrigatórios.")] // Adicionado Required
        public DateTime DataHora { get; set; }

        [Required(ErrorMessage = "Status é obrigatório.")] // Adicionado Required
        [StringLength(50)] // Adicionado limite de tamanho
        public string Status { get; set; } = "Agendado"; // Valor padrão

        // *** CORREÇÃO NULLABILITY: Adicionado '?' ***
        public Cliente? Cliente { get; set; }
        public Profissional? Profissional { get; set; }
        public Servico? Servico { get; set; }
        // ********************************************
    }
}