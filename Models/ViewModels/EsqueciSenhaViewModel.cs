using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.ViewModels
{
    public class EsqueciSenhaViewModel
    {
        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Por favor, insira um email válido.")]
        [Display(Name = "Email de Cadastro")]
        public string Email { get; set; }
    }
}