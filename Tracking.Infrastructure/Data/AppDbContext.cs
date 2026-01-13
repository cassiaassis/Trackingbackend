using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Microsoft.EntityFrameworkCore;
using Tracking.Domain;

namespace Tracking.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Campanha> Campanhas => Set<Campanha>();
    public DbSet<ResgateBrinde> Resgates => Set<ResgateBrinde>();
    public DbSet<RastreioResgate> Rastreios => Set<RastreioResgate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ============== TRKG_EMPRESA ==============
        b.Entity<Empresa>(e =>
        {
            e.ToTable("TRKG_EMPRESA");
            e.HasKey(x => x.IdEmpresa);

            e.Property(x => x.IdEmpresa).HasColumnName("id_empresa");
            e.Property(x => x.RazaoSocial).HasColumnName("razaosocial");
            e.Property(x => x.NomeFantasia).HasColumnName("nomefantasia");
            e.Property(x => x.Cnpj).HasColumnName("cnpj");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Responsavel).HasColumnName("responsavel");
            e.Property(x => x.Telefone).HasColumnName("telefone");
            e.Property(x => x.CdStatus).HasColumnName("cd_status");
            e.Property(x => x.DtRegistro).HasColumnName("dt_registro");
            e.Property(x => x.DtAtualizacao).HasColumnName("dt_atualizacao");
        });

        // ============== TRKG_CAMPANHA ==============
        b.Entity<Campanha>(e =>
        {
            e.ToTable("TRKG_CAMPANHA");
            e.HasKey(x => x.IdCampanha);

            e.Property(x => x.IdCampanha).HasColumnName("id_campanha");
            e.Property(x => x.IdEmpresa).HasColumnName("id_empresa");
            e.Property(x => x.DescCampanha).HasColumnName("desc_campanha");
            e.Property(x => x.DtInicio).HasColumnName("dt_inicio");
            e.Property(x => x.DtFim).HasColumnName("dt_fim");
            e.Property(x => x.CdStatus).HasColumnName("cd_status");
            e.Property(x => x.DtRegistro).HasColumnName("dt_registro");
            e.Property(x => x.DtAtualizacao).HasColumnName("dt_atualizacao");

            e.HasOne(x => x.Empresa)
             .WithMany(e2 => e2.Campanhas)
             .HasForeignKey(x => x.IdEmpresa);
        });

        // =========== TRKG_RESGATE_BRINDES ===========
        b.Entity<ResgateBrinde>(e =>
        {
            e.ToTable("TRKG_RESGATE_BRINDES");
            e.HasKey(x => x.IdResgate);

            e.Property(x => x.IdResgate).HasColumnName("id_resgate");
            e.Property(x => x.IdCampanha).HasColumnName("id_campanha");
            e.Property(x => x.DataResgate).HasColumnName("data_resgate");

            e.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
            e.Property(x => x.Telefone).HasColumnName("telefone");
            e.Property(x => x.Endereco).HasColumnName("endereco");
            e.Property(x => x.Numero).HasColumnName("numero");
            e.Property(x => x.Complemento).HasColumnName("complemento");
            e.Property(x => x.Bairro).HasColumnName("bairro");
            e.Property(x => x.Cep).HasColumnName("cep");
            e.Property(x => x.Uf).HasColumnName("uf");
            e.Property(x => x.Cidade).HasColumnName("cidade");
            e.Property(x => x.KitDescricao).HasColumnName("kit_descricao");
            e.Property(x => x.Lote).HasColumnName("lote");
            e.Property(x => x.Localidade).HasColumnName("localidade");
            e.Property(x => x.ValorNf).HasColumnName("valor_nf");

            e.Property(x => x.DtRegistro).HasColumnName("dt_registro");
            e.Property(x => x.DtAtualizacao).HasColumnName("dt_atualizacao");

            e.HasOne(x => x.Campanha)
             .WithMany(c => c.Resgates)
             .HasForeignKey(x => x.IdCampanha);

            e.HasMany(x => x.Rastreios)
             .WithOne(r => r.Resgate)
             .HasForeignKey(r => r.IdResgate);
        });

        // ========= TRKG_RASTREIO_RESGATE =========
        b.Entity<RastreioResgate>(e =>
        {
            e.ToTable("TRKG_RASTREIO_RESGATE");
            e.HasKey(x => x.IdRastreio);

            e.Property(x => x.IdRastreio).HasColumnName("id_rastreio");
            e.Property(x => x.IdResgate).HasColumnName("id_resgate");
            e.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(x => x.CodigoRastreio).HasColumnName("cd_rastreio").HasMaxLength(50);
            e.Property(x => x.DtRegistro).HasColumnName("dt_registro");
            e.Property(x => x.DtAtualizacao).HasColumnName("dt_atualizacao");
        });
    }
}
