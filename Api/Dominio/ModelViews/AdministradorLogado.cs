namespace MinimalApi.Dominio.ModelViews
{
    public record AdministradorLogado
    {
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}