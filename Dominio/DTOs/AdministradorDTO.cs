using MinimalApi.Dominio.Enuns;

namespace MinimalApi.Dominio.DTOs
{
    public class AdministradorDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public Perfil? Perfil { get; set; }
    }
}