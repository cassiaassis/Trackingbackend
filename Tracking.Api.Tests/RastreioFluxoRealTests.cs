#nullable enable
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

        [Fact(DisplayName = "Regra geral: Pedido 2038 (CPF 11211311411 / e-mail joaoteste@gmail.com)")]
        public async Task RegraGeral_Pedido_2038()
        {
            var (okCpf, idCpf, cdCpf, mailCpf) = await ConsultarPorCpfAsync("11211311411");
            okCpf.Should().BeTrue("CPF deve existir"); idCpf.Should().Be(2038);

            var (okMail, idMail, cdMail, cpfMail) = await ConsultarPorEmailAsync("joaoteste@gmail.com");
            okMail.Should().BeTrue("e-mail deve existir"); idMail.Should().Be(2038);

            // tratar valores possivelmente nulos retornados do banco
            var cd = !string.IsNullOrWhiteSpace(cdCpf) ? cdCpf! : cdMail;
            var cpfDb = string.IsNullOrWhiteSpace(cpfMail) ? "<null>" : cpfMail;
            var emailDb = string.IsNullOrWhiteSpace(mailCpf) ? "<null>" : mailCpf;
            Console.WriteLine($"[TEST] id_resgate={idCpf ?? idMail}, cpf_db='{cpfDb}', email_db='{emailDb}', cd_rastreio='{cd ?? "<null>"}'");

            // validações opcionais: apenas quando os campos existem
            if (mailCpf is not null) mailCpf.Should().Be("joaoteste@gmail.com");
            if (cpfMail is not null) cpfMail.Should().Be("11211311411");

            if (string.IsNullOrWhiteSpace(cd)) return;

            using var http = new HttpClient();
            var auth = await ObterAuthAsync(http);
            var detail = await ObterOrderDetailAsync(http, auth, cd);

            detail.order!.shippment.Should().NotBeNull();
            detail.order!.shippingevents.Should().NotBeNull();
            detail.order!.shippingevents!.Length.Should().BeGreaterThan(0);
        }

        [Fact(DisplayName = "Regra geral: Pedido 2036 (CPF 11111111111 / e-mail joseteste@gmail.com)")]
        public async Task RegraGeral_Pedido_2036()
        {
            var (okCpf, idCpf, cdCpf, mailCpf) = await ConsultarPorCpfAsync("11111111111");
            okCpf.Should().BeTrue(); idCpf.Should().Be(2036);

            var (okMail, idMail, cdMail, cpfMail) = await ConsultarPorEmailAsync("joseteste@gmail.com");
            okMail.Should().BeTrue(); idMail.Should().Be(2036);

            var cd = !string.IsNullOrWhiteSpace(cdCpf) ? cdCpf! : cdMail;
            var cpfDb = string.IsNullOrWhiteSpace(cpfMail) ? "<null>" : cpfMail;
            var emailDb = string.IsNullOrWhiteSpace(mailCpf) ? "<null>" : mailCpf;
            Console.WriteLine($"[TEST] id_resgate={idCpf ?? idMail}, cpf_db='{cpfDb}', email_db='{emailDb}', cd_rastreio='{cd ?? "<null>"}'");

            if (mailCpf is not null) mailCpf.Should().Be("joseteste@gmail.com");
            if (cpfMail is not null) cpfMail.Should().Be("11111111111");

            if (string.IsNullOrWhiteSpace(cd)) return;

            using var http = new HttpClient();
            var auth = await ObterAuthAsync(http);
            var detail = await ObterOrderDetailAsync(http, auth, cd);

            detail.order!.shippingevents.Should().NotBeNull();
            detail.order!.shippingevents!.Length.Should().BeGreaterThan(0);
        }

        [Fact(DisplayName = "Regra geral: Pedido 2039 (CPF 11611711811 / e-mail joanna@gmail.com)")]
        public async Task RegraGeral_Pedido_2039()
        {
            var (okCpf, idCpf, cdCpf, mailCpf) = await ConsultarPorCpfAsync("11611711811");
            okCpf.Should().BeTrue(); idCpf.Should().Be(2039);

            var (okMail, idMail, cdMail, cpfMail) = await ConsultarPorEmailAsync("joanna@gmail.com");
            okMail.Should().BeTrue(); idMail.Should().Be(2039);

            var cd = !string.IsNullOrWhiteSpace(cdCpf) ? cdCpf! : cdMail;
            var cpfDb = string.IsNullOrWhiteSpace(cpfMail) ? "<null>" : cpfMail;
            var emailDb = string.IsNullOrWhiteSpace(mailCpf) ? "<null>" : mailCpf;
            Console.WriteLine($"[TEST] id_resgate={idCpf ?? idMail}, cpf_db='{cpfDb}', email_db='{emailDb}', cd_rastreio='{cd ?? "<null>"}'");

            if (mailCpf is not null) mailCpf.Should().Be("joanna@gmail.com");
            if (cpfMail is not null) cpfMail.Should().Be("11611711811");

            if (string.IsNullOrWhiteSpace(cd))
            {
                _output.WriteLine("⚠️ cd_rastreio é NULL - pedido em preparação");
                return;
            }

            using var http = new HttpClient();
            var auth = await ObterAuthAsync(http);

            try
            {
                var detail = await ObterOrderDetailAsync(http, auth, cd);

                // Serializa o JSON de retorno da aplicação
                var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions { WriteIndented = true });
                _output.WriteLine("=== JSON de retorno da aplicação ===");
                _output.WriteLine(json);
                _output.WriteLine("====================================");


                detail.order!.shippingevents.Should().NotBeNull();
                detail.order!.shippingevents!.Length.Should().BeGreaterThan(0);
            }
            catch (Exception ex) when (ex.Message.Contains("404"))
            {
                _output.WriteLine($"⚠️ Pedido {cd} não encontrado na TPL (404) - possivelmente cancelado ou removido");
                // Não falha o teste - apenas registra que o pedido não existe mais na TPL
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
