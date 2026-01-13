namespace Tracking.Application.Mappers;

/// <summary>
/// Mapeia os códigos internos da TPL para mensagens amigáveis da timeline do frontend
/// </summary>
public static class StatusTimelineMapper
{
    /// <summary>
    /// Modelo de mapeamento para a timeline
    /// </summary>
    public record TimelineStatus(
        string CodTimeline,      // Código para o frontend
        string StatusTimeline,   // Título curto
        string DsTimeline        // Descrição detalhada
    );

    /// <summary>
    /// Dicionário com os mapeamentos: internalCode (TPL) → Status Timeline
    /// </summary>
    private static readonly Dictionary<int, TimelineStatus> _mappings = new()
    {
        // 0️⃣ Status especial: Em preparação (quando cd_rastreio é NULL)
        [0] = new("0", "Em preparação", "Estamos preparando seu pedido com carinho"),

        // 1️⃣ Pedido recebido
        [1] = new("1", "Pedido recebido", "Recebemos seu pedido e já estamos processando"),

        // 5️⃣ Aguardando picking / Em preparação
        [5] = new("0", "Em preparação", "Seu pedido está sendo separado no estoque"),

        // 🔟 Picking realizado
        [10] = new("2", "Separando pedido", "Seu pedido está sendo preparado para envio"),

        // 2️⃣0️⃣ Checkout
        [20] = new("2", "Separando pedido", "Seu pedido está sendo conferido"),

        // 2️⃣5️⃣ Nota fiscal emitida
        [25] = new("3", "Nota fiscal emitida", "A nota fiscal do seu pedido foi gerada"),

        // 3️⃣0️⃣ Pronto para despacho
        [30] = new("3", "Pronto para despacho", "Seu pedido está pronto para ser enviado"),

        // 5️⃣0️⃣ Despachado
        [50] = new("4", "Despachado", "Seu pedido foi enviado para a transportadora"),

        // 6️⃣0️⃣ Coletado pela transportadora
        [60] = new("4", "Coletado pela transportadora", "A transportadora já retirou seu pedido"),

        // 7️⃣0️⃣ Em trânsito
        [70] = new("5", "Em trânsito", "Seu pedido está a caminho"),

        // 7️⃣5️⃣ Saiu para entrega
        [75] = new("6", "Saiu para entrega", "Seu pedido saiu para entrega e chegará em breve"),

        // 9️⃣0️⃣ Entregue
        [90] = new("7", "Entregue", "Seu pedido foi entregue com sucesso!"),

        // ❌ Ocorrências
        [80] = new("8", "Ocorrência", "Houve uma ocorrência com seu pedido. Estamos resolvendo"),
        [100] = new("9", "Falha na entrega", "Não conseguimos entregar. Nova tentativa será feita"),
        [110] = new("9", "Pedido recusado", "O pedido foi recusado no endereço de entrega"),
        [1010] = new("8", "Endereço incorreto", "O endereço de entrega precisa ser corrigido"),
        [1020] = new("8", "Destinatário ausente", "Destinatário ausente. Nova tentativa será feita"),
        [1040] = new("8", "Aguardando retirada", "Seu pedido está disponível para retirada"),

        // 🔄 Outras situações
        [200] = new("10", "Pedido cancelado", "Este pedido foi cancelado"),
        [300] = new("10", "Devolvido", "O pedido foi devolvido ao remetente"),
        [400] = new("10", "Extraviado", "O pedido está sendo localizado"),
        [510] = new("4", "Registrado na transportadora", "A transportadora registrou seu pedido"),
        [1150] = new("3", "Volume preparado", "O volume do seu pedido foi preparado"),
    };

    /// <summary>
    /// Mapeia um internalCode da TPL para o status da timeline
    /// </summary>
    public static TimelineStatus? MapearPorInternalCode(int? internalCode)
    {
        if (internalCode is null) return null;

        return _mappings.TryGetValue(internalCode.Value, out var status) ? status : null;
    }

    /// <summary>
    /// Obtém o status de "Em preparação" (cod_timeline = "0")
    /// Usado quando cd_rastreio é NULL
    /// </summary>
    public static TimelineStatus ObterStatusPreparacao()
    {
        return _mappings[0]; // Garante que existe
    }

    /// <summary>
    /// Verifica se um internalCode está mapeado
    /// </summary>
    public static bool EstaMapeado(int? internalCode)
    {
        return internalCode.HasValue && _mappings.ContainsKey(internalCode.Value);
    }

    /// <summary>
    /// Obtém todos os mapeamentos (útil para documentação/testes)
    /// </summary>
    public static IReadOnlyDictionary<int, TimelineStatus> ObterTodosMapeamentos()
    {
        return _mappings;
    }
}