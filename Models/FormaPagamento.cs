namespace AgendaTatiNails.Models
{
    // Mapeia a tabela FormasPagamento
    public class FormaPagamento
    {
        public int FormaPagId { get; set; }
        public int FormaPagTipo { get; set; }
        public int IdAtendimento { get; set; }
    }
}