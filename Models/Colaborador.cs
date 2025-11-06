namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Colaboradores
    public class Colaborador
    {
        public int ColabId { get; set; } // É a mesma ID do Usuario

        // Propriedade de navegação (útil)
        public Usuario Usuario { get; set; }
    }
}