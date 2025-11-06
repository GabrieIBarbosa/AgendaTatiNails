namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Usuarios
    public class Usuario
    {
        public int UsuarioId { get; set; }
        public string UsuarioEmail { get; set; }
        public string UsuarioSenha { get; set; }
        public string UsuarioNome { get; set; }
    }
}