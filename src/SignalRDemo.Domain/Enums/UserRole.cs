namespace SignalRDemo.Domain.Enums;

/// <summary>Papel do usuário no help desk. Controla autorização nos hubs e controllers.</summary>
public enum UserRole
{
    Customer = 0,
    Agent = 1,
    Manager = 2
}
