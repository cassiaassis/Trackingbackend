#nullable enable
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tracking.Application;
using Xunit;
using Xunit.Abstractions;

namespace Tracking.Api.Tests
{
    public class RastreioFluxoRegraGeralTests
    {
        private readonly string _connString;
        private readonly string _tplBaseUrl;
        private readonly string _tplApiKey;
        private readonly string _tplToken;
        private readonly string _tplEmail;
        private readonly ITestOutputHelper _output;

        // xUnit injeta ITestOutputHelper no construtor
        public RastreioFluxoRegraGeralTests(ITestOutputHelper output)
        {
            _output = output;

            // Carrega configuração do projeto Tracking.Api (User Secrets do projeto API)
            var config = BuildConfiguration();

            _connString =
                Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer") ??
                Environment.GetEnvironmentVariable("ConnectionStrings:SqlServer") ??
                config.GetConnectionString("SqlServer") ??
                throw new InvalidOperationException("ConnectionStrings:SqlServer não configurada.");

            _tplBaseUrl =
                Environment.GetEnvironmentVariable("Tpl__BaseUrl") ??
                Environment.GetEnvironmentVariable("Tpl:BaseUrl") ??
                config["Tpl:BaseUrl"] ??
                "https://oms.tpl.com.br/api";

            _tplApiKey =
                Environment.GetEnvironmentVariable("Tpl__ApiKey") ??
                Environment.GetEnvironmentVariable("Tpl:ApiKey") ??
                config["Tpl:ApiKey"] ??
                throw new InvalidOperationException("Tpl:ApiKey não configurada.");

            _tplToken =
                Environment.GetEnvironmentVariable("Tpl__Token") ??
                Environment.GetEnvironmentVariable("Tpl:Token") ??
                config["Tpl:Token"] ??
                throw new InvalidOperationException("Tpl:Token não configurada.");

            _tplEmail =
                Environment.GetEnvironmentVariable("Tpl__Email") ??
                Environment.GetEnvironmentVariable("Tpl:Email") ??
                config["Tpl:Email"] ??
                throw new InvalidOperationException("Tpl:Email não configurado.");
        }

        // ===== Helpers SQL =====
        private static async Task<(bool ok, int? id_resgate, string? cd_rastreio, string? outraChave)>
            ConsultarAsync(string connString, string coluna, string valor, bool isCpf)
        {
            var sql = $@"
                SELECT TOP (1) id_resgate, cd_rastreio, {(isCpf ? "email" : "cpf")} AS outra
                FROM TRKG_RASTREIO_RESGATE
                WHERE {coluna} = @valor
                ORDER BY ISNULL(dt_atualizacao, dt_registro) DESC, id_rastreio DESC;
            ";

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@valor", SqlDbType.VarChar, 200) { Value = valor });

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows) return (false, null, null, null);

            await reader.ReadAsync();
            var id = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var cd = reader.IsDBNull(1) ? null : reader.GetString(1);
            var outra = reader.IsDBNull(2) ? null : reader.GetString(2);
            return (true, id, cd, outra);
        }

        // ===== Helpers SQL =====
        private static async Task<List<(string cpf, string cd_rastreio)>> ConsultarRASTREIO_RESGATEAsync(string connString)

        {
            var sql = $@"
                SELECT CPF, CD_RASTREIO
                FROM TRKG_RASTREIO_RESGATE
                WHERE cd_rastreio IS NOT NULL
            ";

            var result = new List<(string cpf, string cd_rastreio)>(); 

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var cpf = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var cd = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                result.Add((cpf, cd));
            }

            return result;

        }


        private Task<(bool ok, int? id, string? cd, string? mail)> ConsultarPorCpfAsync(string cpf) =>
            ConsultarAsync(_connString, "cpf", cpf, isCpf: true);

        private Task<(bool ok, int? id, string? cd, string? cpf)> ConsultarPorEmailAsync(string email) =>
            ConsultarAsync(_connString, "email", email, isCpf: false);

        // ===== Helpers TPL =====
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private static string MaskSecret(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "<null>";
            if (s.Length <= 8) return new string('*', s.Length);
            return s.Substring(0, 4) + new string('*', s.Length - 8) + s.Substring(s.Length - 4);
        }

        private async Task<string> ObterAuthAsync(HttpClient http)
        {
            var bodyObj = new { apikey = _tplApiKey, token = _tplToken, email = _tplEmail };
            var bodyJson = JsonSerializer.Serialize(bodyObj);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_tplBaseUrl}/get/auth")
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };

            _output.WriteLine("=== TPL AUTH REQUEST ===");
            _output.WriteLine("URL: " + req.RequestUri);
            _output.WriteLine("Body (masked): " +
                JsonSerializer.Serialize(new { apikey = MaskSecret(_tplApiKey), token = MaskSecret(_tplToken), email = MaskSecret(_tplEmail) }));
            _output.WriteLine("========================");

            var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            _output.WriteLine("=== TPL AUTH RESPONSE ===");
            _output.WriteLine("StatusCode: " + (int)resp.StatusCode + " " + resp.ReasonPhrase);
            _output.WriteLine("Body: " + (string.IsNullOrEmpty(text) ? "<empty>" : text));
            _output.WriteLine("========================");

            resp.IsSuccessStatusCode.Should().BeTrue($"TPL AUTH falhou: HTTP {(int)resp.StatusCode} {text}");

            var authDoc = JsonSerializer.Deserialize<TplAuthResponse>(text, JsonOpts);
            authDoc.Should().NotBeNull("payload AUTH inválido");
            authDoc!.token.Should().NotBeNullOrWhiteSpace("token não retornado");  // ✅ Mudou de "auth" para "token"
            return authDoc.token!;  // ✅ Mudou de "auth" para "token"
        }

        private async Task<TplOrderDetailResponse> ObterOrderDetailAsync(HttpClient http, string auth, string orderNumber)
        {
            var bodyObj = new { auth, order = new { number = orderNumber } };
            var bodyJson = JsonSerializer.Serialize(bodyObj);

            // var _tplBeUrl = "https://oms.tpl.com.br/api";
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_tplBaseUrl}/get/orderdetail")
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };

            _output.WriteLine("=== TPL ORDERDETAIL REQUEST ===");
            _output.WriteLine("URL: " + req.RequestUri);
            _output.WriteLine("Body (masked): " +
                JsonSerializer.Serialize(new { auth = MaskSecret(auth), order = new { number = orderNumber } }));
            _output.WriteLine("========================");

            var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            _output.WriteLine("=== TPL ORDERDETAIL RESPONSE ===");
            _output.WriteLine("StatusCode: " + (int)resp.StatusCode + " " + resp.ReasonPhrase);
            _output.WriteLine("Body: " + (string.IsNullOrEmpty(text) ? "<empty>" : text));
            _output.WriteLine("========================");

            resp.IsSuccessStatusCode.Should().BeTrue($"TPL orderdetail falhou: HTTP {(int)resp.StatusCode} {text}");

            var dto = JsonSerializer.Deserialize<TplOrderDetailResponse>(text, JsonOpts);
            dto.Should().NotBeNull("payload orderdetail inválido");
            dto!.code.Should().Be(200, $"TPL retornou code={dto.code} message={dto.message}");
            dto.order.Should().NotBeNull("order não retornado");
            return dto!;
        }

        private async Task SalvarDadosTPLAsync(SqlConnection conn, string cpf, string cd_rastreio, TplOrderInfo info, List<Tracking.Application.TplShippingEvent> shippingevents)

        {
            // 1. Inserir info
            var insertInfoCmd = new SqlCommand(@"
        INSERT INTO TRKG_TPLOrderInfo (info, cd_rastreio, date, prediction)
        OUTPUT INSERTED.id
        VALUES (@info, @cd_rastreio, @date, @prediction)", conn);

            insertInfoCmd.Parameters.AddWithValue("@info", (object?)info.id ?? DBNull.Value);
            insertInfoCmd.Parameters.AddWithValue("@cd_rastreio", (object?)info.number ?? DBNull.Value);
            insertInfoCmd.Parameters.Add("@date", SqlDbType.Date).Value = DateTime.TryParse(info.date, out var dt) ? dt : DBNull.Value;
            insertInfoCmd.Parameters.Add("@prediction", SqlDbType.Date).Value = DateTime.TryParse(info.prediction, out var pred) ? pred : DBNull.Value;

            var result = await insertInfoCmd.ExecuteScalarAsync();
            if (result is null)
                throw new InvalidOperationException("Falha ao inserir TRKG_TPLOrderInfo: nenhum id retornado.");

            var info_id = result is null ? (int?)null : Convert.ToInt32(result);

            // 2. Inserir shippingevents
            foreach (var evt in shippingevents)
            {
                var insertEventCmd = new SqlCommand(@"
            INSERT INTO TRKG_TplShippingEvent (info_id, code, info, complement, date, final, volume, internalcode )
            VALUES (@info_id, @code, @info, @complement, @date, @final, @volume, @internalcode)", conn);

                insertEventCmd.Parameters.AddWithValue("@info_id", info_id);
                insertEventCmd.Parameters.AddWithValue("@code", (object?)evt.code ?? DBNull.Value);
                insertEventCmd.Parameters.AddWithValue("@info", (object?)evt.info ?? DBNull.Value);
                insertEventCmd.Parameters.AddWithValue("@complement", (object?)evt.complement ?? DBNull.Value);
                insertEventCmd.Parameters.Add("@date", SqlDbType.Date).Value = DateTime.TryParse(evt.date, out var dt_) ? dt : DBNull.Value;
                insertEventCmd.Parameters.Add("@final", SqlDbType.Date).Value = DateTime.TryParse(evt.final, out var dtFinal) ? dtFinal : DBNull.Value;
                insertEventCmd.Parameters.AddWithValue("@volume", (object?)evt.volume ?? DBNull.Value);
                insertEventCmd.Parameters.AddWithValue("@internalcode", (object?)evt.internalCode ?? DBNull.Value);

                await insertEventCmd.ExecuteNonQueryAsync();
            }
        }

        public class TplAuthResponse 
        { 
            public string? token { get; set; }  // ✅ Mudou de "auth" para "token"
            public int? id { get; set; } 
            public int? code { get; set; } 
        }
        public class TplOrderDetailResponse { public int code { get; set; } public string? message { get; set; } public TplOrder? order { get; set; } }
        public class TplOrder
        {
            public int? code { get; set; }
            public string? message { get; set; }
            public TplShipment? shippment { get; set; }
            public TplShippingEvent[]? shippingevents { get; set; }
        }
        public class TplShipment { public string? nick { get; set; } public string? tracker { get; set; } public string? trackerUrl { get; set; } public string? url { get; set; } }
        public class TplShippingEvent { public int? internalCode { get; set; } public string? code { get; set; } public string? info { get; set; } public string? date { get; set; } }

        // ===== Testes =====
        [Fact(DisplayName = "Buscar dado real na TPL: NR-NF 113-1")]
        public async Task BuscarDadoRealNaTpl_113_1()
        {
            // Arrange: configure o serviço manualmente ou via DI
            var identificador = "98148109591"; // ou CPF/email real para o teste
            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddScoped<Tracking.Infrastructure.Repositories.IRastreioRepository, Tracking.Infrastructure.Repositories.RastreioRepository>()
                .AddScoped<Tracking.Application.Services.ClienteService>()
                .AddDbContext<Tracking.Infrastructure.Data.AppDbContext>(options =>
                {
                    // Configure sua connection string de teste aqui
                    options.UseSqlServer(_connString);
                })
                .BuildServiceProvider();

            var clienteService = serviceProvider.GetRequiredService<Tracking.Application.Services.ClienteService>();

            // Act
            var result = await clienteService.ConsultarAsync(identificador);


            // Assert
            result.Should().NotBeNull("O serviço deve retornar um rastreio válido");
            result!.CdRastreio.Should().NotBeNullOrWhiteSpace();
            result.Eventos.Should().NotBeNull();
            result.Eventos.Count.Should().BeGreaterThan(0);

            // (Opcional) Exibir o JSON de retorno para debug
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine("=== JSON de retorno da aplicação ===");
            _output.WriteLine(json);
            _output.WriteLine("====================================");
        }


        [Fact(DisplayName = "ATUALIZAR EVENTOS", Skip = "Desabilitado temporariamente")]
        public async Task AtualizarEventosPedidos()
        {

            // Consulta todos os rastreios
            var rastreios = await ConsultarRASTREIO_RESGATEAsync(_connString);

            // Instancia o serviço TPL
            var httpClient = new HttpClient { BaseAddress = new Uri(_tplBaseUrl) };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tpl:ApiKey"] = _tplApiKey,
                    ["Tpl:Token"] = _tplToken,
                    ["Tpl:Email"] = _tplEmail
                })
                .Build();

            var tplService = new Tracking.Application.TplService(httpClient, config);

            foreach (var (cpf, cd_rastreio) in rastreios)
            {
                if (string.IsNullOrWhiteSpace(cd_rastreio))
                {
                    _output.WriteLine($"⚠️ cd_rastreio vazio para CPF {cpf}");
                    continue;
                }

                try
                {
                    var (info, shippingevents, code, message) = await tplService.ObterDadosBrutosAsync(cd_rastreio, null);

                    // Validação ANTES de tentar salvar/inserir
                    shippingevents.Should().NotBeNull();
                    shippingevents!.Count.Should().BeGreaterThan(0);

                    using var conn = new SqlConnection(_connString);
                    await conn.OpenAsync();

                    if (info != null && shippingevents != null)
                    {
                        await SalvarDadosTPLAsync(conn, cpf, cd_rastreio, info, shippingevents);
                    }

                    _output.WriteLine("=== INCLUIDO O RASTREIO ===");
                    _output.WriteLine(cd_rastreio);
                    _output.WriteLine("====================================");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Erro ao consultar cd_rastreio {cd_rastreio} (CPF {cpf}): {ex.Message}");
                }
            }
        }


        private static IConfiguration BuildConfiguration()
        {
            var basePath = Directory.GetCurrentDirectory();

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }
    }
}
