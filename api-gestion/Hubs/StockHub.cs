using Microsoft.AspNetCore.SignalR;

namespace api_gestion.Hubs;

/// <summary>
/// Hub temps réel utilisé pour notifier l'interface de Suivi (Gestion)
/// dès qu'un scan ou un ajustement modifie le stock.
/// </summary>
public class StockHub : Hub
{
}
