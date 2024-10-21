using MinimalApi;
using MinimalApi.Infraestrutura.Db;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Dominio.Interfaces;
using MinimalApi.Dominio.Servicos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using MinimalApi.Dominio.ModelViews;
using MinimalApi.Dominio.Entidades;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using MinimalApi.Dominio.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MinimalApi.Dominio.Enuns;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
        key = Configuration.GetSection("Jwt").ToString() ?? "123456";
    }

    private string key = string.Empty;
    public IConfiguration Configuration { get; set; } = default!;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication(option =>
        {
            option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer("Bearer", option =>
        {
            option.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = false,
                ValidateAudience = false,
            };
            option.Audience = "https://localhost:5001";
        });

        services.AddAuthorization();

        services.AddScoped<IAdministradorServico, AdministradorServico>();
        services.AddScoped<IVeiculoServico, VeiculoServico>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Insira o token JWT aqui"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
            });
        });

        services.AddDbContext<DbContexto>(options =>
        {
            options.UseMySql(
                Configuration.GetConnectionString("MySql"),
                ServerVersion.AutoDetect(Configuration.GetConnectionString("MySql"))
            );
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endepoints =>
        {
            #region Home
            endepoints.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");
            #endregion

            #region Administradores
            string GerarToken(Administrador administrador)
            {
                if (string.IsNullOrEmpty(key))
                    return string.Empty;

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
                var crediantials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>(){
                    new Claim("Emai", administrador.Email),
                    new Claim("Perfil", administrador.Perfil),
                    new Claim(ClaimTypes.Role, administrador.Perfil)
                };
                var token = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: crediantials
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }

            endepoints.MapPost("/administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>
            {
                var adm = administradorServico.Login(loginDTO);
                if (adm != null)
                {
                    string token = GerarToken(adm);
                    return Results.Ok(new AdministradorLogado
                    {
                        Email = adm.Email,
                        Perfil = adm.Perfil,
                        Token = token
                    });
                }
                else
                    return Results.Unauthorized();
            }).AllowAnonymous().WithTags("Administradores");

            endepoints.MapGet("/administradores", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
            {
                var administradores = administradorServico.Todos(pagina);

                if (administradores == null)
                    return Results.NotFound();

                var adms = new List<AdministradorModelView>();
                foreach (var adm in administradores)
                {
                    adms.Add(new AdministradorModelView
                    {
                        Id = adm.Id,
                        Email = adm.Email,
                        Perfil = adm.Perfil
                    });
                }

                return Results.Ok(adms);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = Perfil.Adm.ToString() })
            .WithTags("Administradores");

            endepoints.MapGet("/administradores/{id}", ([FromRoute] int id, IAdministradorServico administradorServico) =>
            {
                var administrador = administradorServico.BuscaPorId(id);

                if (administrador == null)
                    return Results.NotFound();

                return Results.Ok(new AdministradorModelView
                {
                    Id = administrador.Id,
                    Email = administrador.Email,
                    Perfil = administrador.Perfil
                });
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = Perfil.Adm.ToString() })
            .WithTags("Administradores");

            endepoints.MapPost("/administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) =>
            {
                var validacao = new ErrosDeValidacao();

                if (string.IsNullOrEmpty(administradorDTO.Email))
                    validacao.Mensagens.Add("Email é obrigatório");
                if (string.IsNullOrEmpty(administradorDTO.Senha))
                    validacao.Mensagens.Add("Senha é obrigatória");
                if (administradorDTO.Perfil == null)
                    validacao.Mensagens.Add("Perfil é obrigatório");

                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                var administrador = new Administrador
                {
                    Email = administradorDTO.Email,
                    Senha = administradorDTO.Senha,
                    Perfil = administradorDTO.Perfil.ToString() ?? Perfil.Editor.ToString()
                };

                administradorServico.Incluir(administrador);

                return Results.Created($"/administradores/{administrador.Id}", new AdministradorModelView
                {
                    Id = administrador.Id,
                    Email = administrador.Email,
                    Perfil = administrador.Perfil
                });
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = Perfil.Adm.ToString() })
            .WithTags("Administradores");
            #endregion

            #region Veiculos
            ErrosDeValidacao validaDTO(VeiculoDTO veiculoDTO)
            {
                var validacao = new ErrosDeValidacao();

                if (string.IsNullOrEmpty(veiculoDTO.Nome))
                    validacao.Mensagens.Add("Nome é obrigatório");
                if (string.IsNullOrEmpty(veiculoDTO.Marca))
                    validacao.Mensagens.Add("Marca é obrigatória");
                if (veiculoDTO.Ano < 1950)
                    validacao.Mensagens.Add("Veículo muito antigo, aceito somente anos superiores a 1950");

                return validacao;
            }

            endepoints.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
            {
                var validacao = validaDTO(veiculoDTO);
                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                var veiculo = new Veiculo
                {
                    Nome = veiculoDTO.Nome,
                    Marca = veiculoDTO.Marca,
                    Ano = veiculoDTO.Ano
                };

                veiculoServico.Incluir(veiculo);

                return Results.Created($"/veiculos/{veiculo.Id}", veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Perfil.Adm.ToString()},{Perfil.Editor.ToString()}" })
            .WithTags("Veiculos");

            endepoints.MapGet("/veiculos", ([FromQuery] int? pagina, IVeiculoServico veiculoServico) =>
            {
                var veiculos = veiculoServico.Todos(pagina);

                return Results.Ok(veiculos);
            }).RequireAuthorization().WithTags("Veiculos");

            endepoints.MapGet("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
            {
                var veiculo = veiculoServico.BuscaPorId(id);

                if (veiculo == null)
                    return Results.NotFound();

                return Results.Ok(veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Perfil.Adm.ToString()},{Perfil.Editor.ToString()}" })
            .WithTags("Veiculos");

            endepoints.MapPut("/veiculos/{id}", ([FromRoute] int id, VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
            {
                var validacao = validaDTO(veiculoDTO);
                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                var veiculo = veiculoServico.BuscaPorId(id);

                if (veiculo == null)
                    return Results.NotFound();

                veiculo.Nome = veiculoDTO.Nome;
                veiculo.Marca = veiculoDTO.Marca;
                veiculo.Ano = veiculoDTO.Ano;

                veiculoServico.Atualizar(veiculo);

                return Results.Ok(veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = Perfil.Adm.ToString() })
            .WithTags("Veiculos");

            endepoints.MapDelete("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
            {
                var veiculo = veiculoServico.BuscaPorId(id);

                if (veiculo == null)
                    return Results.NotFound();

                veiculoServico.Apagar(veiculo);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = Perfil.Adm.ToString() })
            .WithTags("Veiculos");
            #endregion
        });
    }
}