namespace Tracking.Application
{
    public class TplEvento
    {
        public string? Id { get; set; }
        public DateTime Data { get; set; }
        public string? CodigoStatus { get; set; }
        public string? Titulo { get; set; }
        public string? Descricao { get; set; }
    }

    public class TplRastreioResult
    {
        public string CodigoRastreio { get; set; } = "";
        public List<TplEvento> Eventos { get; set; } = new();
        public string? TransportadoraApelido { get; set; }
        public string? TransportadoraTracker { get; set; }
        public string? TransportadoraUrl { get; set; }
        public string? TrackerUrl { get; set; }
        public int? StatusMacro { get; set; }
        public string? MensagemMacro { get; set; }
    }

    public interface ITplService
    {
        Task<TplRastreioResult> ObterDetalhePedidoAsync(
            string orderNumber,
            int? orderId,
            CancellationToken ct = default);

        Task<(TplOrderInfo? info, List<TplShippingEvent>? shippingevents, int code, string? message)> ObterDadosBrutosAsync(
            string orderNumber,
            int? orderId,
            CancellationToken ct = default);
    }
}