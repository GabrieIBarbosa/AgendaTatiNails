namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Clientes e estende Usuario
    public class Cliente
    {
        public int ClienteId { get; set; } // É a mesma ID do Usuario
        public string ClienteTelefone { get; set; }

        // Propriedade de navegação (útil)
        public Usuario Usuario { get; set; } 
    }
}