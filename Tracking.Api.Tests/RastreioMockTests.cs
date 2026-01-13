using FluentAssertions;
using Moq;
using System.Text.Json;
using System.Text.Encodings.Web;
using Tracking.Application;
using Tracking.Application.Dto;
using Tracking.Application.Services;
using Tracking.Infrastructure.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace Tracking.Api.Tests
{
    public class RastreioMockTests
    {
        private readonly ITestOutputHelper _output;
        private readonly JsonSerializerOptions _jsonOptions;

        public RastreioMockTests(ITestOutputHelper output)
        {
            _output = output;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        [Fact(DisplayName = "Mock CPF 32676652800 - Deve retornar pedido entregue com eventos mapeados")]
        public async Task Mock_PedidoEntregue_DeveRetornarEstruturaCorreta()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "32676652800";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Exibir JSON formatado
            var json = JsonSerializer.Serialize(result, _jsonOptions);
            _output.WriteLine("=== JSON RETORNADO ===");
            _output.WriteLine(json);
            _output.WriteLine("======================");

            // Assert
            result.Should().NotBeNull();
            result!.code.Should().Be(200);
            result.message.Should().BeOneOf("OK", "OK (Mock Fallback)");

            result.info.Should().NotBeNull();
            result.info!.id.Should().NotBeNullOrEmpty();
            result.info.number.Should().NotBeNullOrEmpty();

            result.shippingevents.Should().NotBeNull();
            result.shippingevents.Should().HaveCountGreaterThan(0);

            var primeiroEvento = result.shippingevents[0];
            primeiroEvento.code.Should().NotBeNull();
            primeiroEvento.dscode.Should().NotBeNullOrEmpty();
            primeiroEvento.message.Should().NotBeNullOrEmpty();
            primeiroEvento.dtshipping.Should().NotBeNullOrEmpty();
            primeiroEvento.internalcode.Should().HaveValue();

            // ✅ Validar que eventos foram mapeados (sem duplicados)
            var internalCodes = result.shippingevents
                .Select(e => e.internalcode)
                .Where(c => c.HasValue)
                .ToList();
            internalCodes.Should().OnlyHaveUniqueItems("não deve haver eventos duplicados");

            _output.WriteLine($"✅ Eventos únicos retornados: {result.shippingevents.Count}");
            _output.WriteLine($"✅ Primeiro status: {primeiroEvento.dscode} (internalcode: {primeiroEvento.internalcode})");

            // ✅ Validar que o mapeamento foi aplicado
            primeiroEvento.code.Should().NotBe("5", "deve usar o código mapeado da timeline");
        }

        [Fact(DisplayName = "Mock CPF 12676652800 - Deve retornar 'Em preparação' com texto da timeline")]
        public async Task Mock_EmPreparacao_DeveRetornarStatusCorreto()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "12676652800";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Exibir JSON
            var json = JsonSerializer.Serialize(result, _jsonOptions);
            _output.WriteLine("=== JSON RETORNADO ===");
            _output.WriteLine(json);
            _output.WriteLine("======================");

            // Assert
            result.Should().NotBeNull();
            result!.code.Should().Be(200);
            result.message.Should().Be("OK");

            result.shippingevents.Should().HaveCount(1);

            // ✅ Validar valores do StatusTimelineMapper (SEM emojis)
            var evento = result.shippingevents[0];
            evento.code.Should().Be("0", "cod_timeline para 'Em preparação' é '0'");
            evento.dscode.Should().Be("Em preparação", "deve usar o texto mapeado da timeline");
            evento.message.Should().Be("Estamos preparando seu pedido com carinho", "deve usar a mensagem amigável");
            evento.internalcode.Should().Be(5, "internalcode da TPL deve ser mantido");

            _output.WriteLine($"✅ Status mapeado: {evento.dscode}");
            _output.WriteLine($"✅ Mensagem: {evento.message}");
        }

        [Fact(DisplayName = "Mock CPF 22676652801 - Deve retornar 404 'não localizado'")]
        public async Task Mock_NaoEncontrado_DeveRetornarMensagemCorreta()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "22676652801";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Exibir JSON
            var json = JsonSerializer.Serialize(result, _jsonOptions);
            _output.WriteLine("=== JSON RETORNADO ===");
            _output.WriteLine(json);
            _output.WriteLine("======================");

            // Assert
            result.Should().NotBeNull();
            result!.code.Should().Be(404);
            result.message.Should().Be("CPF ou e-mail não localizado.");

            result.shippingevents.Should().HaveCount(1);
            result.shippingevents[0].dscode.Should().BeEmpty();
            result.shippingevents[0].message.Should().BeEmpty();
        }

        [Theory(DisplayName = "Deve mapear internalCode para código da timeline corretamente")]
        [InlineData("32676652800", 90, "7", "Entregue")]
        [InlineData("12676652800", 5, "0", "Em preparação")]
        public async Task Mock_DeveMappearInternalCodeParaTimeline(
            string cpf,
            int expectedInternalCode,
            string expectedTimelineCode,
            string expectedTimelineStatus)
        {
            // Arrange
            var service = CreateClienteService();

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Assert
            result.Should().NotBeNull();

            var primeiroEvento = result!.shippingevents[0];
            primeiroEvento.internalcode.Should().Be(expectedInternalCode, "internalcode da TPL deve ser mantido");
            primeiroEvento.code.Should().Be(expectedTimelineCode, "code deve ser o cod_timeline mapeado");
            primeiroEvento.dscode.Should().Be(expectedTimelineStatus, "dscode deve ser o status_timeline mapeado");

            _output.WriteLine($"✅ InternalCode {expectedInternalCode} → Timeline Code '{expectedTimelineCode}'");
            _output.WriteLine($"✅ Status mapeado: {primeiroEvento.dscode}");
        }

        [Fact(DisplayName = "Deve remover eventos duplicados por internalCode")]
        public async Task Mock_DeveRemoverEventosDuplicados()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "32676652800";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Assert
            result.Should().NotBeNull();
            result!.shippingevents.Should().NotBeNull();

            // ✅ Verificar que não há internalCodes duplicados
            var internalCodes = result.shippingevents
                .Where(e => e.internalcode.HasValue)
                .Select(e => e.internalcode!.Value)
                .ToList();

            var duplicados = internalCodes
                .GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            duplicados.Should().BeEmpty("não deve haver eventos com o mesmo internalCode");

            _output.WriteLine($"✅ Total de eventos únicos: {result.shippingevents.Count}");
            _output.WriteLine($"✅ InternalCodes: {string.Join(", ", internalCodes)}");
        }

        [Fact(DisplayName = "Deve aplicar mensagens amigáveis da timeline para todos os eventos")]
        public async Task Mock_DeveAplicarMensagensAmigaveis()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "32676652800";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Assert
            result.Should().NotBeNull();

            foreach (var evento in result!.shippingevents)
            {
                if (evento.internalcode.HasValue)
                {
                    // ✅ Validar que mensagens são amigáveis (contêm texto personalizado)
                    var temMensagemAmigavel =
                        evento.message.Contains("seu pedido", StringComparison.OrdinalIgnoreCase) ||
                        evento.message.Contains("estamos", StringComparison.OrdinalIgnoreCase) ||
                        evento.message.Contains("foi", StringComparison.OrdinalIgnoreCase) ||
                        evento.message.Contains("nota fiscal", StringComparison.OrdinalIgnoreCase) ||
                        evento.message.Contains("transportadora", StringComparison.OrdinalIgnoreCase);

                    _output.WriteLine($"InternalCode {evento.internalcode}: {evento.dscode}");
                    _output.WriteLine($"  Mensagem: {evento.message}");

                    temMensagemAmigavel.Should().BeTrue(
                        $"Evento com internalCode {evento.internalcode} deve ter mensagem amigável. " +
                        $"Recebido: '{evento.message}'");
                }
            }
        }

        [Fact(DisplayName = "Deve filtrar eventos não mapeados")]
        public async Task Mock_DeveFiltrarEventosNaoMapeados()
        {
            // Arrange
            var service = CreateClienteService();
            var cpf = "32676652800";

            // Act
            var result = await service.ConsultarTplAsync(cpf);

            // Assert
            result.Should().NotBeNull();

            // ✅ Todos os eventos retornados devem ter internalcode mapeado
            foreach (var evento in result!.shippingevents)
            {
                evento.internalcode.Should().HaveValue("todos os eventos devem ter internalcode");

                // Validar que o internalcode está na lista de códigos mapeados
                var codigosMapeados = new[] { 0, 1, 5, 10, 20, 25, 30, 50, 60, 70, 75, 90, 80, 100, 110, 1010, 1020, 1040, 200, 300, 400, 510, 1150 };
                codigosMapeados.Should().Contain(evento.internalcode!.Value,
                    $"internalcode {evento.internalcode} deve estar mapeado no StatusTimelineMapper");
            }

            _output.WriteLine($"✅ Todos os {result.shippingevents.Count} eventos estão mapeados");
        }

        private static IClienteService CreateClienteService()
        {
            var mockRepo = new Mock<IRastreioRepository>();
            var mockTpl = new Mock<ITplService>();

            return new ClienteService(mockRepo.Object, mockTpl.Object);
        }
    }
}