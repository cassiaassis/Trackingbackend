namespace Tracking.Application.Mappers;

public static class StatusTranspMapper
{
    /// <summary>
    /// Mapeia o internalcode para a descrição conforme tb_status_transp
    /// </summary>
    public static string ObterDescricaoStatus(int? internalCode) =>
        internalCode switch
        {
            1 => "Pedido recebido",
            3 => "Aguardando WMS",
            5 => "Aguardando picking",
            7 => "Integrado WMS (obsoleto)",
            8 => "Aguardando nota",
            10 => "Picking digital realizado",
            13 => "Pedido cancelado",
            20 => "Checkout",
            25 => "Nota recebida",
            28 => "Rastreador recebido",
            30 => "Pedido separado para checkout",
            50 => "Despachado",
            60 => "Coletado pela transportadora",
            70 => "Em trânsito",
            75 => "Saiu para entrega",
            80 => "Houve alguma ocorrência",
            90 => "Entregue",
            100 => "Falha na entrega",
            110 => "Pedido recusado",
            200 => "Pedido cancelado",
            300 => "Pedido devolvido à origem",
            400 => "Pedido extraviado",
            411 => "Roubo de carga",
            500 => "Redespacho",
            510 => "Registros da transportadora",
            1002 => "Seriais definidos",
            1010 => "Endereço incorreto",
            1020 => "Destinatário ausente",
            1040 => "Objeto aguardando retirada",
            1100 => "Objeto não procurado",
            1150 => "Volume preparado",
            1199 => "Aguardando CTE",
            1200 => "CTE gerado",
            9999 => "Em tratativa com a transportadora",
            10002 => "Avaria",
            10003 => "Aviso de coleta enviado à transportadora",
            10004 => "Parado no posto fiscal",
            10006 => "Volumes ajustado via WMS",
            10007 => "Peso ajustado via WMS",
            10008 => "Detectado erro de endereço no pedido",
            _ => "Status não mapeado"
        };
}