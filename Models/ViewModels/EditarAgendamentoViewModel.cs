using AgendaTatiNails.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding; // Required for [BindNever]
using Microsoft.AspNetCore.Mvc.Rendering; // Required for SelectList
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models.ViewModels
{
    public class EditarAgendamentoViewModel
    {
        public int Id { get; set; } // ID do Agendamento a ser editado (vem do formulário hidden)

        [Required(ErrorMessage = "Selecione um serviço.")]
        [Display(Name = "Serviço")]
        public int ServicoId { get; set; } // Vem do select

        [Required(ErrorMessage = "Selecione a data.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Data")]
        public DateTime Data { get; set; } // Vem do input date

        [Required(ErrorMessage = "Selecione o horário.")]
        [Display(Name = "Horário")]
        public string Hora { get; set; } // Vem do select de hora

      
        [BindNever] // Impede que o ASP.NET Core tente validar esta propriedade no POST
        public SelectList? ServicosDisponiveis { get; set; } // Tornada nulável '?'

        [BindNever] // Impede que o ASP.NET Core tente validar esta propriedade no POST
        public SelectList? HorariosDisponiveis { get; set; } // Tornada nulável '?'

        [BindNever] // Impede que o ASP.NET Core tente validar esta propriedade no POST
        public string? Status { get; set; } // Tornada nulável '?'
    
    }
}